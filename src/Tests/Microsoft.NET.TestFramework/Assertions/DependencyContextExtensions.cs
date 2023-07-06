// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.DependencyModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.NET.TestFramework.Assertions
{
    public static class DependencyContextExtensions
    {
        public static DependencyContextAssertions Should(this DependencyContext dependencyContext)
        {
            return new DependencyContextAssertions(dependencyContext);
        }
    }
}
