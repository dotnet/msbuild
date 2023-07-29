// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    public class CoreMSBuildAndWindowsOnlyFactAttribute : FactAttribute
    {
        public CoreMSBuildAndWindowsOnlyFactAttribute()
        {
            if (TestContext.Current.ToolsetUnderTest.ShouldUseFullFrameworkMSBuild || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                this.Skip = "This test requires Core MSBuild and Windows to run";
            }
        }
    }
}
