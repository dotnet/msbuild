// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETCOREAPP

using Xunit;

namespace Microsoft.NET.TestFramework
{
    public class RequiresSpecificFrameworkFactAttribute : FactAttribute
    {
        public RequiresSpecificFrameworkFactAttribute(string framework)
        {
            if (!EnvironmentInfo.SupportsTargetFramework(framework))
            {
                Skip = $"This test requires a shared framework that isn't present: {framework}";
            }
        }
    }
}

#endif
