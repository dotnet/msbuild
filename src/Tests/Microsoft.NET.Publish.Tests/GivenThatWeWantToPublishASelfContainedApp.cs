// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishASelfContainedApp : SdkTest
    {
        private const string TestProjectName = "HelloWorld";
        private const string TargetFramework = "netcoreapp2.1";

        public GivenThatWeWantToPublishASelfContainedApp(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_errors_when_publishing_self_contained_app_without_rid()
        {
             var testAsset = _testAssetsManager
                .CopyTestAsset(TestProjectName)
                .WithSource();

            var publishCommand = new PublishCommand(Log, testAsset.TestRoot);
            publishCommand
                .Execute(
                    "/p:SelfContained=true",
                    $"/p:TargetFramework={TargetFramework}")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotHaveSelfContainedWithoutRuntimeIdentifier);
        }

        [Fact]
        public void It_errors_when_publishing_self_contained_without_apphost()
        {
            var runtimeIdentifier = RuntimeEnvironment.GetRuntimeIdentifier();

            var testAsset = _testAssetsManager
                .CopyTestAsset(TestProjectName)
                .WithSource();

            var publishCommand = new PublishCommand(Log, testAsset.TestRoot);
            publishCommand
                .Execute(
                    "/p:SelfContained=true",
                    "/p:UseAppHost=false",
                    $"/p:TargetFramework={TargetFramework}",
                    $"/p:RuntimeIdentifier={runtimeIdentifier}")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotUseSelfContainedWithoutAppHost);
        }

        // repro https://github.com/dotnet/sdk/issues/2466
        [Fact]
        public void It_does_not_fail_publishing_a_self_twice()
        {
            var runtimeIdentifier = RuntimeEnvironment.GetRuntimeIdentifier();

            var testAsset = _testAssetsManager
                .CopyTestAsset(TestProjectName)
                .WithSource();

            var msbuidArgs = new string[] { "/p:SelfContained=true",
                    $"/p:TargetFramework={TargetFramework}",
                    $"/p:RuntimeIdentifier={runtimeIdentifier}"};

            var restoreCommand = new RestoreCommand(Log, testAsset.TestRoot);

            restoreCommand.Execute(msbuidArgs);

            var publishCommand = new PublishCommand(Log, testAsset.TestRoot);
            publishCommand
                .Execute(msbuidArgs)
                .Should().Pass();

            publishCommand
                .Execute(msbuidArgs)
                .Should().Pass().And.NotHaveStdOutContaining("HelloWorld.exe' already exists");
        }
    }
}
