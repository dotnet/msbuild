// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Execution;

using Shouldly;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class EndToEndCondition_Tests
    {
        private readonly TestContext _output;

        public EndToEndCondition_Tests(TestContext output)
        {
            _output = output;
        }

        [MSBuildTestMethod]
        [DataRow("'$(MSBuildToolsVersion)' == 'Current'")] // shim doesn't apply to string-equal
        [DataRow("'$(MSBuildToolsVersion)' &gt;= '15.0'")]
        [DataRow("'$(MSBuildToolsVersion)' &gt;= '14.0.0.0'")]
        [DataRow("'$(MSBuildToolsVersion)' &gt; '15.0'")]
        [DataRow("'15.0' &lt; '$(MSBuildToolsVersion)'")]
        [DataRow("'14.0.0.0' &lt; '$(MSBuildToolsVersion)'")]
        [DataRow("'15.0' &lt;= '$(MSBuildToolsVersion)'")]
        [DataRow("'$(MSBuildToolsVersion)' == '$(VisualStudioVersion)'")]
        public void TrueComparisonsInvolvingMSBuildToolsVersion(string condition)
        {
            MockLogger logger = new MockLogger(_output, profileEvaluation: false, printEventsToStdout: false);
            BuildResult result = Helpers.BuildProjectContentUsingBuildManager($@"<Project>
 <Target Name=""Print"">
  <Message Importance=""High""
           Text=""Condition evaluated true: '{condition}'""
           Condition=""{condition}"" />
 </Target>
</Project>", logger);

            logger.AssertLogContains("Condition evaluated true");

            result.OverallResult.ShouldBe(BuildResultCode.Success);
        }

        [MSBuildTestMethod]
        [DataRow(" '$(MSBuildToolsVersion)' == '' OR '$(MSBuildToolsVersion)' &lt; '4.0' ")] // WiX check
        [DataRow("$(MSBuildToolsVersion) &gt; 20")]
        [DataRow("'$(MSBuildToolsVersion)' == ''")]
        [DataRow("'$(MSBuildToolsVersion)' == 'Incorrect'")]
        [DataRow("'14.3' &gt; '$(MSBuildToolsVersion)'")]
        [DataRow("'Current' == '$(VisualStudioVersion)'")] // comparing the string representation of MSBuildToolsVersion directly doesn't match
        public void FalseComparisonsInvolvingMSBuildToolsVersion(string condition)
        {
            MockLogger logger = new MockLogger(_output, profileEvaluation: false, printEventsToStdout: false);
            BuildResult result = Helpers.BuildProjectContentUsingBuildManager($@"<Project>
 <Target Name=""Print"">
  <Message Importance=""High""
           Text=""Condition evaluated false: '{condition}'""
           Condition=""!({condition})"" />
 </Target>
</Project>", logger);

            logger.AssertLogContains("Condition evaluated false");

            result.OverallResult.ShouldBe(BuildResultCode.Success);
        }
    }
}
