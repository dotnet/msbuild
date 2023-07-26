// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    public class CoreMSBuildOnlyTheoryAttribute : TheoryAttribute
    {
        public CoreMSBuildOnlyTheoryAttribute()
        {
            if (TestContext.Current.ToolsetUnderTest.ShouldUseFullFrameworkMSBuild)
            {
                this.Skip = "This test requires Core MSBuild to run";
            }
        }
    }
}
