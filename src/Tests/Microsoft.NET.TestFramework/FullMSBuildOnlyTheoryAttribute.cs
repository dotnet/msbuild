using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Microsoft.NET.TestFramework
{
    public class FullMSBuildOnlyTheoryAttribute : TheoryAttribute
    {
        public FullMSBuildOnlyTheoryAttribute()
        {
            if (!TestContext.Current.ToolsetUnderTest.ShouldUseFullFrameworkMSBuild)
            {
                this.Skip = "This test requires Full MSBuild to run";
            }
        }
    }
}
