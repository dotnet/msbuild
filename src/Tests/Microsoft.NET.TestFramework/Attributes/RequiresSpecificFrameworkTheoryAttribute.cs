// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETCOREAPP

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.NET.TestFramework
{
    public class RequiresSpecificFrameworkTheoryAttribute : TheoryAttribute
    {
        public RequiresSpecificFrameworkTheoryAttribute(string framework)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(framework))
            {
                Skip = $"This test requires a shared framework that isn't present: {framework}";
            }
        }
    }
}

#endif
