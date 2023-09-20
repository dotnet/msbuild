// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    public class FullMSBuildOnlyTheoryAttribute : TheoryAttribute
    {
        public FullMSBuildOnlyTheoryAttribute()
        {
            if (!TestContext.Current.ToolsetUnderTest.ShouldUseFullFrameworkMSBuild)
            {
                Skip = "This test requires Full MSBuild to run";
            }
        }
    }
}
