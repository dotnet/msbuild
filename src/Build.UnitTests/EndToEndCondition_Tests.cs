// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests;

using Shouldly;
using Xunit;
using Xunit.Abstractions;

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
