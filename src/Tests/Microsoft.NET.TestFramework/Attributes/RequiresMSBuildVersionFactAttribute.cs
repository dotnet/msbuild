// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit;

namespace Microsoft.NET.TestFramework
{
    public class RequiresMSBuildVersionFactAttribute : FactAttribute
    {
        public RequiresMSBuildVersionFactAttribute(string version)
        {
            if (!Version.TryParse(TestContext.Current.ToolsetUnderTest.MSBuildVersion, out Version msbuildVersion))
            {
                this.Skip = $"Failed to determine the version of MSBuild ({ TestContext.Current.ToolsetUnderTest.MSBuildVersion }).";
                return;
            }
            if (!Version.TryParse(version, out Version requiredVersion))
            {
                this.Skip = $"Failed to determine the version required by this test ({ version }).";
                return;
            }
            if (requiredVersion > msbuildVersion)
            {
                this.Skip = $"This test requires MSBuild version { version } to run (using { TestContext.Current.ToolsetUnderTest.MSBuildVersion }).";
            }
        }
    }
}
