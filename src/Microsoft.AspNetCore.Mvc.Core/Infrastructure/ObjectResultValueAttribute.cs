// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Mvc.Infrastructure
{
    /// <summary>
    /// Parameter that turns in to the value for ObjectResult.
    /// e.g. new BadResult([ObjectResultValue] object value)
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class ObjectResultValueAttribute : Attribute
    {
    }
}
