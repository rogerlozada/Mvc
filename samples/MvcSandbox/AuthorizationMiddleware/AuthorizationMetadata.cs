// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcSandbox.AuthorizationMiddleware
{
    public class AuthorizationMetadata
    {
        public AuthorizationMetadata(IEnumerable<string> roles)
        {
            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            Roles = roles.ToArray();
        }

        public IReadOnlyList<string> Roles { get; }
    }
}