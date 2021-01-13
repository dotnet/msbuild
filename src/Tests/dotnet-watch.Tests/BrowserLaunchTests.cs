// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.NET.TestFramework;
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

        [PlatformSpecificFact(Xunit.TestPlatforms.Windows | Xunit.TestPlatforms.OSX)]
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

        [PlatformSpecificFact(Xunit.TestPlatforms.Windows | Xunit.TestPlatforms.OSX)]
        public async Task RefreshesBrowserOnChange()
        {
            var launchBrowserMessage = "watch : Launching browser: https://localhost:5001/";
            var refreshBrowserMessage = "watch : Reloading browser";

            var testAsset = _testAssetsManager.CopyTestAsset(AppName)
                .WithSource()
                .Path;

            using var app = new WatchableApp(testAsset, _logger);
            app.DotnetWatchArgs.Add("--verbose");
            var source = Path.Combine(app.SourceDirectory, "Program.cs");

            await app.StartWatcherAsync();

            // Verify we launched the browser.
            await app.Process.GetOutputLineStartsWithAsync(launchBrowserMessage, TimeSpan.FromMinutes(2));

            // Make a file change and verify we reloaded the browser.
            File.SetLastWriteTime(source, DateTime.Now);
            await app.Process.GetOutputLineStartsWithAsync(refreshBrowserMessage, TimeSpan.FromMinutes(2));
        }

        [PlatformSpecificFact(Xunit.TestPlatforms.Windows | Xunit.TestPlatforms.OSX)]
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
