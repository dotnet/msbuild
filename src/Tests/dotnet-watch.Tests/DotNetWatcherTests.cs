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
            const string messagePrefix = "DOTNET_WATCH = ";
            var message = await app.Process.GetOutputLineStartsWithAsync(messagePrefix, TimeSpan.FromMinutes(2));
            var envValue = message.Substring(messagePrefix.Length);
            Assert.Equal("1", envValue);
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

            var message = await app.Process.GetOutputLineStartsWithAsync(messagePrefix, TimeSpan.FromMinutes(2));
            var count = int.Parse(message.Substring(messagePrefix.Length), CultureInfo.InvariantCulture);
            Assert.Equal(1, count);

            await app.IsWaitingForFileChange();

            File.SetLastWriteTime(source, DateTime.Now);
            await app.HasRestarted(TimeSpan.FromMinutes(1));

            message = await app.Process.GetOutputLineStartsWithAsync(messagePrefix, TimeSpan.FromMinutes(2));
            count = int.Parse(message.Substring(messagePrefix.Length), CultureInfo.InvariantCulture);
            Assert.Equal(2, count);
        }

        [Fact(Skip = "https://github.com/dotnet/aspnetcore/issues/23854")]
        public async Task RunsWithNoRestoreOnOrdinaryFileChanges()
        {
            var testAsset = _testAssetsManager.CopyTestAsset(AppName)
                .WithSource()
                .Path;

            using var app = new WatchableApp(testAsset, _logger);

            app.DotnetWatchArgs.Add("--verbose");

            await app.StartWatcherAsync(arguments: new[] { "wait" });
            var source = Path.Combine(app.SourceDirectory, "Program.cs");
            const string messagePrefix = "dotnet watch ⌚ Running dotnet with the following arguments: run";

            // Verify that the first run does not use --no-restore
            Assert.Contains(app.Process.Output, p => string.Equals(messagePrefix + " -- wait", p.Trim()));

            for (var i = 0; i < 3; i++)
            {
                File.SetLastWriteTime(source, DateTime.Now);
                var message = await app.Process.GetOutputLineStartsWithAsync(messagePrefix, TimeSpan.FromMinutes(2));

                Assert.Equal(messagePrefix + " --no-restore -- wait", message.Trim());

                await app.HasRestarted();
            }
        }

        [Fact(Skip = "https://github.com/dotnet/aspnetcore/issues/23854")]
        public async Task RunsWithRestoreIfCsprojChanges()
        {
            var testAsset = _testAssetsManager.CopyTestAsset(AppName)
                .WithSource()
                .Path;

            using var app = new WatchableApp(testAsset, _logger);

            app.DotnetWatchArgs.Add("--verbose");

            await app.StartWatcherAsync(arguments: new[] { "wait" });
            var source = Path.Combine(app.SourceDirectory, "KitchenSink.csproj");
            const string messagePrefix = "dotnet watch ⌚ Running dotnet with the following arguments: run";

            // Verify that the first run does not use --no-restore
            Assert.Contains(app.Process.Output, p => string.Equals(messagePrefix + " -- wait", p.Trim()));

            File.SetLastWriteTime(source, DateTime.Now);
            var message = await app.Process.GetOutputLineStartsWithAsync(messagePrefix, TimeSpan.FromMinutes(2));

            // csproj changed. Do not expect a --no-restore
            Assert.Equal(messagePrefix + " -- wait", message.Trim());

            await app.HasRestarted();

            // regular file changed after csproj changes. Should use --no-restore
            File.SetLastWriteTime(Path.Combine(app.SourceDirectory, "Program.cs"), DateTime.Now);
            message = await app.Process.GetOutputLineStartsWithAsync(messagePrefix, TimeSpan.FromMinutes(2));
            Assert.Equal(messagePrefix + " --no-restore -- wait", message.Trim());
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/24406")]
        public async Task Run_WithHotReloadEnabled_ReadsLaunchSettings()
        {
            var testAsset = _testAssetsManager.CopyTestAsset("WatchAppWithLaunchSettings")
                .WithSource()
                .Path;

            using var app = new WatchableApp(testAsset, _logger);

            app.DotnetWatchArgs.Add("--verbose");

            await app.StartWatcherAsync();

            await app.Process.GetOutputLineAsyncWithConsoleHistoryAsync("Environment: Development", TimeSpan.FromSeconds(10));
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/24406")]
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

            await app.Process.GetOutputLineAsyncWithConsoleHistoryAsync("Environment: Development", TimeSpan.FromSeconds(10));
        }

        [CoreMSBuildOnlyFact]
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

            await standardInput.WriteLineAsync(inputString).WaitAsync(TimeSpan.FromSeconds(10));
            await app.Process.GetOutputLineAsync($"Echo: {inputString}", TimeSpan.FromSeconds(10));
        }
    }
}
