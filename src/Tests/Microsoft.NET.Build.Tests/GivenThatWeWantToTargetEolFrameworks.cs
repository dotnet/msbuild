// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToTargetEolFrameworks : SdkTest
    {
        public GivenThatWeWantToTargetEolFrameworks(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netcoreapp1.0")]
        [InlineData("netcoreapp3.0")]
        [InlineData("netcoreapp2.1")]
        public void It_warns_that_framework_is_out_of_support(string targetFrameworks)
        {
            var testProject = new TestProject()
            {
                Name = $"Eol{targetFrameworks}",
                TargetFrameworks = targetFrameworks,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            var result = buildCommand
                .Execute();

            result
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("NETSDK1138");
        }

        [Fact]
        public void It_only_checks_for_netcoreapp_eol_frameworks()
        {
            var testProject = new TestProject()
            {
                Name = $"EolOnlyNetCore",
                TargetFrameworks = $"netcoreapp1.0;{ToolsetInfo.CurrentTargetFramework};net472",
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            var result = buildCommand
                .Execute();

            var lines = (result.StdOut.Split(Environment.NewLine)).Where(line => line.Contains("NETSDK1138"));

            Assert.NotNull(lines.FirstOrDefault(line => line.IndexOf("netcoreapp1.0") >= 0));
            Assert.All(lines, line => Assert.DoesNotContain(ToolsetInfo.CurrentTargetFramework, line));
            Assert.All(lines, line => Assert.DoesNotContain("net472", line));
        }

        [Fact]
        public void It_does_not_warn_when_deactivating_check()
        {
            var testProject = new TestProject()
            {
                Name = $"EolNoWarning",
                TargetFrameworks = "netcoreapp1.0",

                IsExe = true
            };

            testProject.AdditionalProperties["CheckEolTargetFramework"] = "false";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            var result = buildCommand
                .Execute();

            result
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("NETSDK1138");
        }
    }
}
