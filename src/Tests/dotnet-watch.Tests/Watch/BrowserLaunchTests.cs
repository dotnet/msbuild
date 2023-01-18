// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tests
{
    public class BrowserLaunchTests : DotNetWatchTestBase
    {
        private const string AppName = "WatchBrowserLaunchApp";

        public BrowserLaunchTests(ITestOutputHelper logger)
            : base(logger)
        {
        }

        [Fact]
        public async Task LaunchesBrowserOnStart()
        {
            var expected = "dotnet watch ⌚ Launching browser: https://localhost:5001/";
            var testAsset = TestAssets.CopyTestAsset(AppName)
                .WithSource()
                .Path;

            App.DotnetWatchArgs.Add("--verbose");

            await App.StartWatcherAsync(testAsset);

            // Verify we launched the browser.
            await App.AssertOutputLineStartsWith(expected);
        }

        [Fact]
        public async Task UsesBrowserSpecifiedInEnvironment()
        {
            var launchBrowserMessage = "dotnet watch ⌚ Launching browser: mycustombrowser.bat https://localhost:5001/";
            var testAsset = TestAssets.CopyTestAsset(AppName)
                .WithSource()
                .Path;

            App.EnvironmentVariables.Add("DOTNET_WATCH_BROWSER_PATH", "mycustombrowser.bat");

            App.DotnetWatchArgs.Add("--verbose");

            await App.StartWatcherAsync(testAsset);

            // Verify we launched the browser.
            await App.AssertOutputLineStartsWith(launchBrowserMessage);
        }
    }
}
