// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class DotNetWatcherTests
    {
        private const string AppName = "WatchKitchenSink";
        private readonly TestAssetsManager _testAssetsManager;
        private readonly ITestOutputHelper _logger;

        public DotNetWatcherTests(ITestOutputHelper logger)
        {
            _testAssetsManager = new TestAssetsManager(logger);
            _logger = logger;
        }

        [Fact]
        public async Task RunsWithDotnetWatchEnvVariable()
        {
            Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_WATCH")), "DOTNET_WATCH cannot be set already when this test is running");

            var testAsset = _testAssetsManager.CopyTestAsset(AppName)
                .WithSource()
                .Path;

            using var app = new WatchableApp(testAsset, _logger);

            await app.StartWatcherAsync();
            Assert.Equal("1", await app.AssertOutputLineStartsWith("DOTNET_WATCH = "));
        }

        [Fact]
        public async Task RunsWithIterationEnvVariable()
        {
            var testAsset = _testAssetsManager.CopyTestAsset(AppName)
                .WithSource()
                .Path;

            using var app = new WatchableApp(testAsset, _logger);

            await app.StartWatcherAsync();
            var source = Path.Combine(app.SourceDirectory, "Program.cs");
            var contents = File.ReadAllText(source);
            const string messagePrefix = "DOTNET_WATCH_ITERATION = ";

            var value = await app.AssertOutputLineStartsWith(messagePrefix);
            Assert.Equal(1, int.Parse(value, CultureInfo.InvariantCulture));

            await app.AssertWaitingForFileChange();

            File.SetLastWriteTime(source, DateTime.Now);
            await app.AssertRestarted();

            value = await app.AssertOutputLineStartsWith(messagePrefix);
            Assert.Equal(2, int.Parse(value, CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task Run_WithHotReloadEnabled_ReadsLaunchSettings()
        {
            var testAsset = _testAssetsManager.CopyTestAsset("WatchAppWithLaunchSettings")
                .WithSource()
                .Path;

            using var app = new WatchableApp(testAsset, _logger);

            app.DotnetWatchArgs.Add("--verbose");

            await app.StartWatcherAsync();

            await app.AssertOutputLineEquals("Environment: Development");
        }

        [Fact]
        public async Task Run_WithHotReloadEnabled_ReadsLaunchSettings_WhenUsingProjectOption()
        {
            var testAsset = _testAssetsManager.CopyTestAsset("WatchAppWithLaunchSettings")
                .WithSource()
                .Path;

            var directoryInfo = new DirectoryInfo(testAsset);
            using var app = new WatchableApp(testAsset, _logger)
            {
                // Configure the working directory to be one level above the test app directory.
                WorkingDirectory = Path.GetFullPath(directoryInfo.Parent.FullName),
            };

            app.DotnetWatchArgs.Add("--verbose");
            app.DotnetWatchArgs.Add("--project");
            app.DotnetWatchArgs.Add(Path.Combine(directoryInfo.Name, "WatchAppWithLaunchSettings.csproj"));

            await app.StartWatcherAsync();

            await app.AssertOutputLineEquals("Environment: Development");
        }

        [CoreMSBuildOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/29047")]
        public async Task Run_WithHotReloadEnabled_DoesNotReadConsoleIn_InNonInteractiveMode()
        {
            var testAsset = _testAssetsManager.CopyTestAsset("WatchAppWithLaunchSettings")
                .WithSource()
                .Path;

            using var app = new WatchableApp(testAsset, _logger)
            {
                EnvironmentVariables =
                {
                    ["READ_INPUT"] = "true",
                },
            };

            app.DotnetWatchArgs.Add("--verbose");
            app.DotnetWatchArgs.Add("--non-interactive");

            await app.StartWatcherAsync();

            var standardInput = app.Process.Process.StandardInput;
            var inputString = "This is a test input";

            await standardInput.WriteLineAsync(inputString);
            Assert.Equal(inputString, await app.AssertOutputLineStartsWith("Echo: "));
        }
    }
}
