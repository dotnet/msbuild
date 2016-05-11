// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Run.Tests
{
    public class RunTests : TestBase
    {
        private const string PortableAppsTestBase = "PortableTests";
        private const string RunTestsBase = "RunTestsApps";

        [WindowsOnlyFact]
        public void RunsSingleTarget()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance(Path.Combine(RunTestsBase, "TestAppFullClr"))
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();
            new RunCommand(instance.TestRoot).ExecuteWithCapturedOutput().Should().Pass().And.HaveStdOutContaining("NET451, ARGS: 0");
        }

        [Fact]
        public void RunsDefaultWhenPresent()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance(Path.Combine(RunTestsBase, "TestAppMultiTarget"))
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();
            new RunCommand(instance.TestRoot).Execute().Should().Pass();
        }

        [Fact]
        public void FailsWithMultipleTargetAndNoDefault()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance(Path.Combine(RunTestsBase, "TestAppMultiTargetNoCoreClr"))
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();
            new RunCommand(instance.TestRoot).Execute().Should().Fail();
        }

        [Fact]
        public void ItRunsPortableApps()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance(Path.Combine(PortableAppsTestBase, "PortableApp"))
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();
            new RunCommand(instance.TestRoot).Execute().Should().Pass();
        }

        [Fact(Skip = "https://github.com/dotnet/cli/issues/1940")]
        public void ItRunsPortableAppsWithNative()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance(Path.Combine(PortableAppsTestBase, "PortableAppWithNative"))
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();
            new RunCommand(instance.TestRoot).Execute().Should().Pass();
        }

        [Fact]
        public void ItRunsStandaloneApps()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance(Path.Combine(PortableAppsTestBase, "StandaloneApp"))
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();
            new RunCommand(instance.TestRoot).Execute().Should().Pass();
        }

        [Fact]
        public void ItRunsWithLocalProjectJsonArg()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppSimple")
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();
            new RunCommand("project.json")
                .WithWorkingDirectory(instance.TestRoot)
                .ExecuteWithCapturedOutput()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        [Fact]
        public void ItRunsAppsThatOutputUnicodeCharacters()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppWithUnicodéPath")
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();
            new RunCommand(instance.TestRoot)
                .ExecuteWithCapturedOutput()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hélló Wórld!");
        }

        [Fact]
        public void ItPassesArgumentsToTheApp()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppWithArgs")
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();
            new RunCommand(instance.TestRoot)
                .ExecuteWithCapturedOutput("one --two -three")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(
                    JoinWithNewlines(
                        "Hello World!",
                        "I was passed 3 args:",
                        "arg: [one]",
                        "arg: [--two]",
                        "arg: [-three]"));
        }

        [Fact]
        public void ItPassesAllArgsAfterUnexpectedArg()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppWithArgs")
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();
            new RunCommand(instance.TestRoot)
                .ExecuteWithCapturedOutput("Hello -f")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(
                    JoinWithNewlines(
                        "Hello World!",
                        "I was passed 2 args:",
                        "arg: [Hello]",
                        "arg: [-f]"));
        }

        [Fact]
        public void ItHandlesArgSeparatorCorrectly()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppWithArgs")
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();
            new RunCommand(instance.TestRoot)
                .ExecuteWithCapturedOutput("-- one two")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(
                    JoinWithNewlines(
                        "Hello World!",
                        "I was passed 2 args:",
                        "arg: [one]",
                        "arg: [two]"));
        }

        [Fact]
        public void ItHandlesUnrestoredProjectFileCorrectly()
        {
            // NOTE: we don't say "WithLockFiles", so the project is "unrestored"
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppSimple");

            new RunCommand(instance.TestRoot)
                .ExecuteWithCapturedOutput()
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining("NU1009")
                .And
                .HaveStdErrContaining("dotnet restore");
        }

        [Fact]
        public void ItHandlesUnknownProjectFileCorrectly()
        {
            new RunCommand("bad path")
                .ExecuteWithCapturedOutput()
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining("DOTNET1017")
                .And
                .HaveStdErrContaining("bad path");
        }

        private static string JoinWithNewlines(params string[] values)
        {
            return string.Join(Environment.NewLine, values);
        }
    }
}
