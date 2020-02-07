// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Run.Tests
{
    public class GivenThatWeCanPassNonProjectFilesToDotnetRun : SdkTest
    {
        public GivenThatWeCanPassNonProjectFilesToDotnetRun(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItFailsWithAnAppropriateErrorMessage()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("SlnFileWithNoProjectReferences")
                .WithSource()
                .Path;

            var slnFullPath = Path.Combine(projectDirectory, "SlnFileWithNoProjectReferences.sln");

            new DotnetCommand(Log, "run")
                .Execute($"-p", slnFullPath)
                .Should().Fail()
                .And.HaveStdErrContaining(
                    string.Format(
                        Microsoft.DotNet.Tools.Run.LocalizableStrings.RunCommandSpecifiecFileIsNotAValidProject,
                        slnFullPath));
        }
    }
}
