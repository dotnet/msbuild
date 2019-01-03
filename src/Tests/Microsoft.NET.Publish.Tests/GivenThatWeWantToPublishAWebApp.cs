// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAWebApp : SdkTest
    {
        public GivenThatWeWantToPublishAWebApp(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_publishes_as_framework_dependent_by_default()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("WebApp")
                .WithSource();

            var args = new[]
            {
                "-p:Configuration=Release"
            };

            var restoreCommand = new RestoreCommand(Log, testAsset.TestRoot);
            restoreCommand
                .Execute(args)
                .Should()
                .Pass();

            var command = new PublishCommand(Log, testAsset.TestRoot);

            command
                .Execute(args)
                .Should()
                .Pass();

            var publishDirectory =
                command.GetOutputDirectory(targetFramework: "netcoreapp2.0", configuration: "Release");

            publishDirectory.Should().NotHaveSubDirectories();
            publishDirectory.Should().OnlyHaveFiles(new[] {
                "web.config",
                "web.deps.json",
                "web.dll",
                "web.pdb",
                "web.PrecompiledViews.dll",
                "web.PrecompiledViews.pdb",
                "web.runtimeconfig.json",
            });
        }
    }
}
