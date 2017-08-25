// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Cli.Run.Tests
{
    public class GivenThatWeCanPassNonProjectFilesToDotnetRun : TestBase
    {
        [Fact]
        public void ItFailsWithAnAppropriateErrorMessage()
        {
            var projectDirectory = TestAssets
                .Get("SlnFileWithNoProjectReferences")
                .CreateInstance()
                .WithSourceFiles()
                .Root
                .FullName;

            var slnFullPath = Path.Combine(projectDirectory, "SlnFileWithNoProjectReferences.sln");

            new RunCommand()
                .ExecuteWithCapturedOutput($"-p {slnFullPath}")
                .Should().Fail()
                .And.HaveStdErrContaining(
                    string.Format(
                        Microsoft.DotNet.Tools.Run.LocalizableStrings.RunCommandSpecifiecFileIsNotAValidProject,
                        slnFullPath));
        }
    }
}