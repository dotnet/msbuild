// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Cli.MSBuild.Tests
{
    public class GivenDotnetMSBuildBuildsProjects : TestBase
    {
        [Fact]
        public void ItRunsSpecifiedTargetsWithPropertiesCorrectly()
        {
            var testInstance = TestAssetsManager
                .CreateTestInstance("MSBuildBareBonesProject");

            var testProjectDirectory = testInstance.TestRoot;

            new MSBuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("/t:SayHello")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello, from MSBuild!");

            new MSBuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("/t:SayGoodbye")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Goodbye, from MSBuild. :'(");

            new MSBuildCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("/t:SayThis /p:This=GreatScott")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("You want me to say 'GreatScott'");
        }
    }
}
