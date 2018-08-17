// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Mvc.Routing
{
    /// <summary>
    /// An implementation of <see cref="IUrlHelper"/> that uses <see cref="LinkGenerator"/> to build URLs 
    /// for ASP.NET MVC within an application.
    /// </summary>
    internal class EndpointRoutingUrlHelper : UrlHelperBase
    {
        private readonly ILogger<EndpointRoutingUrlHelper> _logger;
        private readonly LinkGenerator _linkGenerator;

        /// <summary>
        /// Initializes a new instance of the <see cref="EndpointRoutingUrlHelper"/> class using the specified
        /// <paramref name="actionContext"/>.
        /// </summary>
        /// <param name="actionContext">The <see cref="Mvc.ActionContext"/> for the current request.</param>
        /// <param name="linkGenerator">The <see cref="LinkGenerator"/> used to generate the link.</param>
        /// <param name="logger">The <see cref="ILogger"/>.</param>
        public EndpointRoutingUrlHelper(
            ActionContext actionContext,
            LinkGenerator linkGenerator,
            ILogger<EndpointRoutingUrlHelper> logger)
            : base(actionContext)
        {
            if (linkGenerator == null)
            {
                throw new ArgumentNullException(nameof(linkGenerator));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _linkGenerator = linkGenerator;
            _logger = logger;
        }

        public override bool TryGenerateAction(UrlActionContext actionContext, out string url)
        {
            if (actionContext == null)
            {
                throw new ArgumentNullException(nameof(actionContext));
            }

            var valuesDictionary = GetValuesDictionary(actionContext.Values);

            if (actionContext.Action == null)
            {
                if (!valuesDictionary.ContainsKey("action") &&
                    AmbientValues.TryGetValue("action", out var action))
                {
                    valuesDictionary["action"] = action;
                }
            }
            else
            {
                valuesDictionary["action"] = actionContext.Action;
            }

            if (actionContext.Controller == null)
            {
                if (!valuesDictionary.ContainsKey("controller") &&
                    AmbientValues.TryGetValue("controller", out var controller))
                {
                    valuesDictionary["controller"] = controller;
                }
            }
            else
            {
                valuesDictionary["controller"] = actionContext.Controller;
            }


            var successfullyGeneratedLink = _linkGenerator.TryGetLink(
                ActionContext.HttpContext,
                valuesDictionary,
                out var link);

            if (!successfullyGeneratedLink)
            {
                url = null;
                return false;
            }

            url = GenerateUrl(actionContext.Protocol, actionContext.Host, link, actionContext.Fragment);
            return true;
        }

        /// <inheritdoc />
        public override string Action(UrlActionContext urlActionContext)
        {
            TryGenerateAction(urlActionContext, out var url);
            return url;
        }

        /// <inheritdoc />
        public override string RouteUrl(UrlRouteContext routeContext)
        {
            TryGenerateRouteUrl(routeContext, out var url);
            return url;
        }

        public override bool TryGenerateRouteUrl(UrlRouteContext routeContext, out string url)
        {
            if (routeContext == null)
            {
                throw new ArgumentNullException(nameof(routeContext));
            }

            var valuesDictionary = routeContext.Values as RouteValueDictionary ?? GetValuesDictionary(routeContext.Values);

            var successfullyGeneratedLink = _linkGenerator.TryGetLink(
                ActionContext.HttpContext,
                routeContext.RouteName,
                valuesDictionary,
                out var link);

            if (!successfullyGeneratedLink)
            {
                url = null;
                return false;
            }

            url = GenerateUrl(routeContext.Protocol, routeContext.Host, link, routeContext.Fragment);
            return true;
        }
    }
}