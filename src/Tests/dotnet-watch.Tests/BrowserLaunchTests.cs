// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class BrowserLaunchTests
    {
        private const string AppName = "WatchBrowserLaunchApp";

        private readonly TestAssetsManager _testAssetsManager;
        private readonly ITestOutputHelper _logger;

        public BrowserLaunchTests(ITestOutputHelper logger)
        {
            _testAssetsManager = new TestAssetsManager(logger);
            _logger = logger;
        }

        [Fact]
        public async Task LaunchesBrowserOnStart()
        {
            var expected = "watch : Launching browser: https://localhost:5001/";
            var testAsset = _testAssetsManager.CopyTestAsset(AppName)
                .WithSource()
                .Path;

            using var app = new WatchableApp(testAsset, _logger);

            app.DotnetWatchArgs.Add("--verbose");

            await app.StartWatcherAsync();

            // Verify we launched the browser.
            await app.Process.GetOutputLineStartsWithAsync(expected, TimeSpan.FromMinutes(2));
        }

        [Fact]
        public async Task UsesBrowserSpecifiedInEnvironment()
        {
            var launchBrowserMessage = "watch : Launching browser: mycustombrowser.bat https://localhost:5001/";
            var testAsset = _testAssetsManager.CopyTestAsset(AppName)
                .WithSource()
                .Path;

            using var app = new WatchableApp(testAsset, _logger);

            app.EnvironmentVariables.Add("DOTNET_WATCH_BROWSER_PATH", "mycustombrowser.bat");

            app.DotnetWatchArgs.Add("--verbose");

            await app.StartWatcherAsync();

            // Verify we launched the browser.
            await app.Process.GetOutputLineStartsWithAsync(launchBrowserMessage, TimeSpan.FromMinutes(2));
        }
    }
}
