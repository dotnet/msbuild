// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAFrameworkDependentApp : SdkTest
    {
        private const string TestProjectName = "HelloWorld";

        public GivenThatWeWantToPublishAFrameworkDependentApp(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData(null, "netcoreapp2.1")]
        [InlineData("true", "netcoreapp2.1")]
        [InlineData("false", "netcoreapp2.1")]
        [InlineData(null, "netcoreapp2.2")]
        [InlineData("true", "netcoreapp2.2")]
        [InlineData("false", "netcoreapp2.2")]
        [InlineData(null, "netcoreapp3.0")]
        [InlineData("true", "netcoreapp3.0")]
        [InlineData("false", "netcoreapp3.0")]
        public void It_publishes_with_or_without_apphost(string useAppHost, string targetFramework)
        {
            var runtimeIdentifier = RuntimeEnvironment.GetRuntimeIdentifier();
            var appHostName = $"{TestProjectName}{Constants.ExeSuffix}";

            var testAsset = _testAssetsManager
                .CopyTestAsset(TestProjectName, $"It_publishes_with_or_without_apphost_{(useAppHost ?? "null")}_{targetFramework}")
                .WithSource();

            var msbuildArgs = new List<string>()
            {
                "/restore",
                $"/p:RuntimeIdentifier={runtimeIdentifier}",
                $"/p:TestRuntimeIdentifier={runtimeIdentifier}",
                "/p:SelfContained=false",
                $"/p:TargetFramework={targetFramework}"
            };

            if (useAppHost != null)
            {
                msbuildArgs.Add($"/p:UseAppHost={useAppHost}");
            }

            var publishCommand = new PublishCommand(Log, testAsset.TestRoot);
            publishCommand
                .Execute(msbuildArgs.ToArray())
                .Should()
                .Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: runtimeIdentifier);

            var expectedFiles = new List<string>()
            {
                $"{TestProjectName}.dll",
                $"{TestProjectName}.pdb",
                $"{TestProjectName}.deps.json",
                $"{TestProjectName}.runtimeconfig.json",
            };

            if (useAppHost != "false")
            {
                expectedFiles.Add(appHostName);
            }

            publishDirectory.Should().NotHaveSubDirectories();
            publishDirectory.Should().OnlyHaveFiles(expectedFiles);

            // Run the apphost if one was generated
            if (useAppHost != "false")
            {
                Command.Create(Path.Combine(publishDirectory.FullName, appHostName), Enumerable.Empty<string>())
                    .EnvironmentVariable(
                        Environment.Is64BitProcess ? "DOTNET_ROOT" : "DOTNET_ROOT(x86)",
                        Path.GetDirectoryName(TestContext.Current.ToolsetUnderTest.DotNetHostPath))
                    .CaptureStdOut()
                    .CaptureStdErr()
                    .Execute()
                    .Should()
                    .Pass()
                    .And
                    .HaveStdOutContaining("Hello World!");
            }
        }

        [Fact]
        public void It_errors_when_using_app_host_with_older_target_framework()
        {
            var runtimeIdentifier = RuntimeEnvironment.GetRuntimeIdentifier();

            var testAsset = _testAssetsManager
                .CopyTestAsset(TestProjectName)
                .WithSource();

            var publishCommand = new PublishCommand(Log, testAsset.TestRoot);
            publishCommand
                .Execute(
                    "/p:SelfContained=false",
                    "/p:UseAppHost=true",
                    $"/p:RuntimeIdentifier={runtimeIdentifier}")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.FrameworkDependentAppHostRequiresVersion21);
        }
    }
}
