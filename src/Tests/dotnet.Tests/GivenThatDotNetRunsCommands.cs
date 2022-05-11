// Copyright (c) .NET Foundation and contributors. All rights reserved. 
// Licensed under the MIT license. See LICENSE file in the project root for full license information. 

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using LocalizableStrings = Microsoft.DotNet.Cli.Utils.LocalizableStrings;

namespace Microsoft.DotNet.Tests
{
    public class GivenThatDotNetRunsCommands : SdkTest
    {
        public GivenThatDotNetRunsCommands(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void UnresolvedPlatformReferencesFailAsExpected()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestProjectWithUnresolvedPlatformDependency", testAssetSubdirectory: "NonRestoredTestProjects")
                            .WithSource();

            new RestoreCommand(testInstance)
                .Execute("/p:SkipInvalidConfigurations=true")
                .Should()
                .Fail();

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("crash")
                .Should().Fail()
                     .And.HaveStdErrContaining(string.Format(LocalizableStrings.NoExecutableFoundMatchingCommand, "dotnet-crash"));
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void GivenAMissingHomeVariableItExecutesHelpCommandSuccessfully(string value)
        {
            new DotnetCommand(Log)
                .WithEnvironmentVariable(CliFolderPathCalculator.PlatformHomeVariableName, value)
                .WithEnvironmentVariable(CliFolderPathCalculator.DotnetHomeVariableName, "")
                .Execute("--help")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(LocalizableStrings.DotNetSdkInfo);
        }

        [Fact]
        public void GivenASpecifiedDotnetCliHomeVariableItPrintsUsageMessage()
        {
            var home = _testAssetsManager.CreateTestDirectory(identifier: "DOTNET_HOME").Path;

            new DotnetCommand(Log)
                .WithEnvironmentVariable(CliFolderPathCalculator.DotnetHomeVariableName, home)
                .Execute("-d", "help")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(
                    string.Format(
                        LocalizableStrings.DotnetCliHomeUsed,
                        home,
                        CliFolderPathCalculator.DotnetHomeVariableName));
        }
    }
}
