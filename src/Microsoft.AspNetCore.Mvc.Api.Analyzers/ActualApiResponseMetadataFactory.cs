// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.AspNetCore.Mvc.Api.Analyzers
{
    public static class ActualApiResponseMetadataFactory
    {
        private static readonly Func<SyntaxNode, bool> _shouldDescendIntoChildren = ShouldDescendIntoChildren;

        internal static bool TryGetActualResponseMetadata(
            in ApiControllerSymbolCache symbolCache,
            SemanticModel semanticModel,
            MethodDeclarationSyntax methodSyntax,
            CancellationToken cancellationToken,
            out IList<ActualApiResponseMetadata> actualResponseMetadata)
        {
            actualResponseMetadata = new List<ActualApiResponseMetadata>();

            var allReturnStatementsReadable = true;

            foreach (var returnStatementSyntax in methodSyntax.DescendantNodes(_shouldDescendIntoChildren).OfType<ReturnStatementSyntax>())
            {
                if (returnStatementSyntax.IsMissing || returnStatementSyntax.Expression.IsMissing)
                {
                    // Ignore malformed return statements.
                    continue;
                }

                var responseMetadata = InspectReturnStatementSyntax(
                    symbolCache,
                    semanticModel,
                    returnStatementSyntax,
                    cancellationToken);

                if (responseMetadata != null)
                {
                    actualResponseMetadata.Add(responseMetadata.Value);
                }
                else
                {
                    allReturnStatementsReadable = false;
                }
            }

            return allReturnStatementsReadable;
        }

        internal static ActualApiResponseMetadata? InspectReturnStatementSyntax(
            in ApiControllerSymbolCache symbolCache,
            SemanticModel semanticModel,
            ReturnStatementSyntax returnStatementSyntax,
            CancellationToken cancellationToken)
        {
            var returnExpression = returnStatementSyntax.Expression;
            var typeInfo = semanticModel.GetTypeInfo(returnExpression, cancellationToken);
            if (typeInfo.Type.TypeKind == TypeKind.Error)
            {
                return null;
            }

            var statementReturnType = typeInfo.Type;

            var defaultStatusCodeAttribute = statementReturnType
                .GetAttributes(symbolCache.DefaultStatusCodeAttribute, inherit: true)
                .FirstOrDefault();

            if (defaultStatusCodeAttribute != null)
            {
                var defaultStatusCode = GetDefaultStatusCode(defaultStatusCodeAttribute);
                if (defaultStatusCode == null)
                {
                    // Unable to read the status code even though the attribute exists.
                    return null;
                }

                return new ActualApiResponseMetadata(returnStatementSyntax, defaultStatusCode.Value);
            }

            if (!symbolCache.IActionResult.IsAssignableFrom(statementReturnType))
            {
                // Return expression does not have a DefaultStatusCodeAttribute and it is not
                // an instance of IActionResult. Must be returning the "model".
                return new ActualApiResponseMetadata(returnStatementSyntax);
            }

            int statusCode;
            switch (returnExpression)
            {
                case InvocationExpressionSyntax invocation:
                    // Covers the 'return StatusCode(200)' case.
                    if (TryInspectMethodArguments(symbolCache, semanticModel, invocation.Expression, invocation.ArgumentList, cancellationToken, out statusCode))
                    {
                        return new ActualApiResponseMetadata(returnStatementSyntax, statusCode);
                    }
                    break;

                case ObjectCreationExpressionSyntax creation:
                    // Covers the 'return new ObjectResult(...) { StatusCode = 200 }' case.
                    if (TryInspectInitializers(symbolCache, semanticModel, creation.Initializer, cancellationToken, out statusCode))
                    {
                        return new ActualApiResponseMetadata(returnStatementSyntax, statusCode);
                    }

                    // Covers the 'return new StatusCodeResult(200) case.
                    if (TryInspectMethodArguments(symbolCache, semanticModel, creation, creation.ArgumentList, cancellationToken, out statusCode))
                    {
                        return new ActualApiResponseMetadata(returnStatementSyntax, statusCode);
                    }
                    break;
            }

            return null;
        }

        private static bool TryInspectInitializers(
            in ApiControllerSymbolCache symbolCache,
            SemanticModel semanticModel,
            InitializerExpressionSyntax initializer,
            CancellationToken cancellationToken,
            out (int statusCode, ITypeSymbol returnType) result)
        {
            var success = false;
            result = default;
            
            if (initializer == null)
            {
                result = default;
                return success;
            }

            for (var i = 0; i < initializer.Expressions.Count; i++)
            {
                if (!(initializer.Expressions[i] is AssignmentExpressionSyntax assignment))
                {
                    continue;
                }

                var statusCode = 0;
                ITypeSymbol typeSymbol = null;

                if (assignment.Left is IdentifierNameSyntax identifier)
                {
                    var symbolInfo = semanticModel.GetSymbolInfo(identifier, cancellationToken);
                    if (symbolInfo.Symbol is IPropertySymbol property)
                    {
                        if (IsInterfaceImplementation(property, symbolCache.StatusCodeActionResultStatusProperty))
                        {
                            success |= TryGetExpressionStatusCode(semanticModel, assignment.Right, cancellationToken, out statusCode);
                        }
                        else if (HasAttributeNamed(property, ApiSymbolNames.ActionResultObjectValueAttribute))
                        {
                            TryGetExpressionObjectType(semanticModel, assignment.Right, cancellationToken, out typeSymbol);
                        }
                    }
                }

                result = (statusCode, typeSymbol);
            }
            return success;
        }

        private static bool TryInspectMethodArguments(
            in ApiControllerSymbolCache symbolCache,
            SemanticModel semanticModel,
            ExpressionSyntax expression,
            BaseArgumentListSyntax argumentList,
            CancellationToken cancellationToken,
            out (int statusCode, ITypeSymbol returnType) result)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(expression, cancellationToken);

            if (!(symbolInfo.Symbol is IMethodSymbol method))
            {
                result = default;
                return false;
            }

            var success = false;
            var statusCode = 0;
            ITypeSymbol typeSymbol = null;
            for (var i = 0; i < method.Parameters.Length; i++)
            {
                var parameter = method.Parameters[i];
                if (HasAttributeNamed(parameter, ApiSymbolNames.ActionResultStatusCodeAttribute))
                {
                    var argument = argumentList.Arguments[parameter.Ordinal];
                    success |= TryGetExpressionStatusCode(semanticModel, argument.Expression, cancellationToken, out statusCode);
                }

                if (HasAttributeNamed(parameter, ApiSymbolNames.ActionResultObjectValueParameterAttribute))
                {
                    var argument = argumentList.Arguments[parameter.Ordinal];
                    TryGetExpressionObjectType(semanticModel, argument.Expression, cancellationToken, out typeSymbol);
                }
            }

            result = (statusCode, typeSymbol);
            return success;
        }

        private static bool TryGetExpressionObjectType(SemanticModel semanticModel, ExpressionSyntax expression, CancellationToken cancellationToken, out ITypeSymbol typeSymbol)
        {
            var symbolInfo = semanticModel.GetDeclaredSymbol(expression, cancellationToken);
            typeSymbol = symbolInfo as ITypeSymbol;
            return typeSymbol != null;
        }

        private static bool TryGetExpressionStatusCode(
            SemanticModel semanticModel,
            ExpressionSyntax expression,
            CancellationToken cancellationToken,
            out int statusCode)
        {
            if (expression is LiteralExpressionSyntax literal && literal.Token.Value is int literalStatusCode)
            {
                // Covers the 'return StatusCode(200)' case.
                statusCode = literalStatusCode;
                return true;
            }

            if (expression is IdentifierNameSyntax || expression is MemberAccessExpressionSyntax)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(expression, cancellationToken);

                if (symbolInfo.Symbol is IFieldSymbol field && field.HasConstantValue && field.ConstantValue is int constantStatusCode)
                {
                    // Covers the 'return StatusCode(StatusCodes.Status200OK)' case.
                    // It also covers the 'return StatusCode(StatusCode)' case, where 'StatusCode' is a constant field.
                    statusCode = constantStatusCode;
                    return true;
                }

                if (symbolInfo.Symbol is ILocalSymbol local && local.HasConstantValue && local.ConstantValue is int localStatusCode)
                {
                    // Covers the 'return StatusCode(statusCode)' case, where 'statusCode' is a local constant.
                    statusCode = localStatusCode;
                    return true;
                }
            }

            statusCode = default;
            return false;
        }

        private static bool ShouldDescendIntoChildren(SyntaxNode syntaxNode)
        {
            return !syntaxNode.IsKind(SyntaxKind.LocalFunctionStatement) &&
                !syntaxNode.IsKind(SyntaxKind.ParenthesizedLambdaExpression) &&
                !syntaxNode.IsKind(SyntaxKind.SimpleLambdaExpression) &&
                !syntaxNode.IsKind(SyntaxKind.AnonymousMethodExpression);
        }

        internal static int? GetDefaultStatusCode(AttributeData attribute)
        {
            if (attribute != null &&
                attribute.ConstructorArguments.Length == 1 &&
                attribute.ConstructorArguments[0].Kind == TypedConstantKind.Primitive &&
                attribute.ConstructorArguments[0].Value is int statusCode)
            {
                return statusCode;
            }

            return null;
        }

        private static bool IsInterfaceImplementation(IPropertySymbol property, IPropertySymbol statusCodeActionResultStatusProperty)
        {
            if (property.Name != statusCodeActionResultStatusProperty.Name)
            {
                return false;
            }

            for (var i = 0; i < property.ExplicitInterfaceImplementations.Length; i++)
            {
                if (property.ExplicitInterfaceImplementations[i] == statusCodeActionResultStatusProperty)
                {
                    return true;
                }
            }

            var implementedProperty = property.ContainingType.FindImplementationForInterfaceMember(statusCodeActionResultStatusProperty);
            return implementedProperty == property;
        }

        private static bool HasAttributeNamed(ISymbol symbol, string attributeName)
        {
            var attributes = symbol.GetAttributes();
            var length = attributes.Length;
            for (var i = 0; i < length; i++)
            {
                if (attributes[i].AttributeClass.Name == attributeName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
