// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Internal;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder
{
    public static class MvcEndpointDataSourceBuilderExtensions
    {
        public static IApplyEndpointBuilder MapMvcRoute(
            this EndpointDataSourcesBuilder routeBuilder,
            string name,
            string template)
        {
            return MapMvcRoute(routeBuilder, name, template, defaults: null);
        }

        public static IApplyEndpointBuilder MapMvcRoute(
            this EndpointDataSourcesBuilder routeBuilder,
            string name,
            string template,
            object defaults)
        {
            return MapMvcRoute(routeBuilder, name, template, defaults, constraints: null);
        }

        public static IApplyEndpointBuilder MapMvcRoute(
            this EndpointDataSourcesBuilder routeBuilder,
            string name,
            string template,
            object defaults,
            object constraints)
        {
            return MapMvcRoute(routeBuilder, name, template, defaults, constraints, dataTokens: null);
        }

        public static IApplyEndpointBuilder MapMvcRoute(
            this EndpointDataSourcesBuilder routeBuilder,
            string name,
            string template,
            object defaults,
            object constraints,
            object dataTokens)
        {
            var endpointDataSources = routeBuilder.ServiceProvider.GetServices<EndpointDataSource>();
            var mvcEndpointDataSource = endpointDataSources.OfType<MvcEndpointDataSource>().Single();

            var endpointInfo = new MvcEndpointInfo(
                name,
                template,
                new RouteValueDictionary(defaults),
                new RouteValueDictionary(constraints),
                new RouteValueDictionary(dataTokens),
                routeBuilder.ServiceProvider.GetRequiredService<ParameterPolicyFactory>());

            mvcEndpointDataSource.ConventionalEndpointInfos.Add(endpointInfo);

            return endpointInfo;
        }
    }
}
