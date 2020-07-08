// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.NET.TestFramework
{
    public class WindowsOnlyRequiresMSBuildVersionTheoryAttribute : TheoryAttribute
    {
        public WindowsOnlyRequiresMSBuildVersionTheoryAttribute(string version)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.Skip = "This test requires Windows to run";
            }

            if (TestContext.Current.ToolsetUnderTest.ShouldUseFullFrameworkMSBuild)
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
}
