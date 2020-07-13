// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit;

namespace Microsoft.NET.TestFramework
{
    public class RequiresMSBuildVersionTheoryAttribute : TheoryAttribute
    {
        public RequiresMSBuildVersionTheoryAttribute(string version)
        {
            CheckForRequiredMSBuildVersion(this, version);
        }

        public static void CheckForRequiredMSBuildVersion(FactAttribute attribute, string version)
        {
            if (TestContext.Current.ToolsetUnderTest.ShouldUseFullFrameworkMSBuild)
            {
                if (!Version.TryParse(TestContext.Current.ToolsetUnderTest.MSBuildVersion, out Version msbuildVersion))
                {
                    attribute.Skip = $"Failed to determine the version of MSBuild ({ TestContext.Current.ToolsetUnderTest.MSBuildVersion }).";
                    return;
                }
                if (!Version.TryParse(version, out Version requiredVersion))
                {
                    attribute.Skip = $"Failed to determine the version required by this test ({ version }).";
                    return;
                }
                if (requiredVersion > msbuildVersion)
                {
                    attribute.Skip = $"This test requires MSBuild version { version } to run (using { TestContext.Current.ToolsetUnderTest.MSBuildVersion }).";
                }
            }
        }
    }
}
