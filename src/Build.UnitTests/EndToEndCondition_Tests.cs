﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Execution;

using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class EndToEndCondition_Tests
    {
        private readonly ITestOutputHelper _output;

        public EndToEndCondition_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("'$(MSBuildToolsVersion)' == 'Current'")] // shim doesn't apply to string-equal
        [InlineData("'$(MSBuildToolsVersion)' &gt;= '15.0'")]
        [InlineData("'$(MSBuildToolsVersion)' &gt;= '14.0.0.0'")]
        [InlineData("'$(MSBuildToolsVersion)' &gt; '15.0'")]
        [InlineData("'15.0' &lt; '$(MSBuildToolsVersion)'")]
        [InlineData("'14.0.0.0' &lt; '$(MSBuildToolsVersion)'")]
        [InlineData("'15.0' &lt;= '$(MSBuildToolsVersion)'")]
        [InlineData("'$(MSBuildToolsVersion)' == '$(VisualStudioVersion)'")]
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

        [Theory]
        [InlineData(" '$(MSBuildToolsVersion)' == '' OR '$(MSBuildToolsVersion)' &lt; '4.0' ")] // WiX check
        [InlineData("$(MSBuildToolsVersion) &gt; 20")]
        [InlineData("'$(MSBuildToolsVersion)' == ''")]
        [InlineData("'$(MSBuildToolsVersion)' == 'Incorrect'")]
        [InlineData("'14.3' &gt; '$(MSBuildToolsVersion)'")]
        [InlineData("'Current' == '$(VisualStudioVersion)'")] // comparing the string representation of MSBuildToolsVersion directly doesn't match
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
