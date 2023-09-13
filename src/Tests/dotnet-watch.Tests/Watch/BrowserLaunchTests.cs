// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            var testAsset = TestAssets.CopyTestAsset(AppName)
                .WithSource();

            await App.StartWatcherAsync(testAsset, testFlags: TestFlags.BrowserRequired);

            // Verify we launched the browser.
            await App.AssertOutputLineStartsWith("dotnet watch ⌚ Launching browser: https://localhost:5001/");
        }

        [Fact]
        public async Task UsesBrowserSpecifiedInEnvironment()
        {
            var testAsset = TestAssets.CopyTestAsset(AppName)
                .WithSource();

            App.EnvironmentVariables.Add("DOTNET_WATCH_BROWSER_PATH", "mycustombrowser.bat");

            await App.StartWatcherAsync(testAsset, testFlags: TestFlags.BrowserRequired);

            // Verify we launched the browser.
            await App.AssertOutputLineStartsWith("dotnet watch ⌚ Launching browser: mycustombrowser.bat https://localhost:5001/");
        }
    }
}
