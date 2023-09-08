// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;

namespace Microsoft.DotNet.Watcher.Tests
{
    public class DotNetWatcherTests : DotNetWatchTestBase
    {
        private const string AppName = "WatchKitchenSink";

        public DotNetWatcherTests(ITestOutputHelper logger)
            : base(logger)
        {
        }

        [Fact]
        public async Task RunsWithDotnetWatchEnvVariable()
        {
            Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_WATCH")), "DOTNET_WATCH cannot be set already when this test is running");

            var testAsset = TestAssets.CopyTestAsset(AppName)
                .WithSource()
                .Path;

            await App.StartWatcherAsync(testAsset);
            Assert.Equal("1", await App.AssertOutputLineStartsWith("DOTNET_WATCH = "));
        }

        [Fact]
        public async Task RunsWithDotnetLaunchProfileEnvVariableWhenNotExplicitlySpecified()
        {
            Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_LAUNCH_PROFILE")), "DOTNET_LAUNCH_PROFILE cannot be set already when this test is running");

            var testAsset = TestAssets.CopyTestAsset(AppName)
                .WithSource()
                .Path;

            await App.StartWatcherAsync(testAsset);
            Assert.Equal("<<<First>>>", await App.AssertOutputLineStartsWith("DOTNET_LAUNCH_PROFILE = "));
        }

        [Fact]
        public async Task RunsWithDotnetLaunchProfileEnvVariableWhenExplicitlySpecified()
        {
            Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_LAUNCH_PROFILE")), "DOTNET_LAUNCH_PROFILE cannot be set already when this test is running");

            var testAsset = TestAssets.CopyTestAsset(AppName)
                .WithSource()
                .Path;

            await App.StartWatcherAsync(testAsset, [ "--launch-profile", "Second"]);
            Assert.Equal("<<<Second>>>", await App.AssertOutputLineStartsWith("DOTNET_LAUNCH_PROFILE = "));
        }

        [Fact]
        public async Task RunsWithDotnetLaunchProfileEnvVariableWhenExplicitlySpecifiedButNotPresentIsEmpty()
        {
            Assert.True(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_LAUNCH_PROFILE")), "DOTNET_LAUNCH_PROFILE cannot be set already when this test is running");

            var testAsset = TestAssets.CopyTestAsset(AppName)
                .WithSource()
                .Path;

            await App.StartWatcherAsync(testAsset, ["--launch-profile", "Third"]);
            Assert.Equal("<<<First>>>", await App.AssertOutputLineStartsWith("DOTNET_LAUNCH_PROFILE = "));
        }

        [Fact]
        public async Task RunsWithIterationEnvVariable()
        {
            var testAsset = TestAssets.CopyTestAsset(AppName)
                .WithSource()
                .Path;

            await App.StartWatcherAsync(testAsset);
            var source = Path.Combine(testAsset, "Program.cs");
            var contents = File.ReadAllText(source);
            const string messagePrefix = "DOTNET_WATCH_ITERATION = ";

            var value = await App.AssertOutputLineStartsWith(messagePrefix);
            Assert.Equal(1, int.Parse(value, CultureInfo.InvariantCulture));

            await App.AssertWaitingForFileChange();

            File.SetLastWriteTime(source, DateTime.Now);
            await App.AssertRestarted();

            value = await App.AssertOutputLineStartsWith(messagePrefix);
            Assert.Equal(2, int.Parse(value, CultureInfo.InvariantCulture));
        }

        [Fact]
        public async Task Run_WithHotReloadEnabled_ReadsLaunchSettings()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppWithLaunchSettings")
                .WithSource()
                .Path;

            App.DotnetWatchArgs.Add("--verbose");

            await App.StartWatcherAsync(testAsset);

            await App.AssertOutputLineEquals("Environment: Development");
        }

        [Fact]
        public async Task Run_WithHotReloadEnabled_ReadsLaunchSettings_WhenUsingProjectOption()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppWithLaunchSettings")
                .WithSource()
                .Path;

            var directoryInfo = new DirectoryInfo(testAsset);

            App.DotnetWatchArgs.Add("--verbose");
            App.DotnetWatchArgs.Add("--project");
            App.DotnetWatchArgs.Add(Path.Combine(directoryInfo.Name, "WatchAppWithLaunchSettings.csproj"));

            // Configure the working directory to be one level above the test app directory.
            await App.StartWatcherAsync(testAsset, workingDirectory: Path.GetFullPath(directoryInfo.Parent.FullName));

            await App.AssertOutputLineEquals("Environment: Development");
        }

        [CoreMSBuildOnlyFact(Skip = "https://github.com/dotnet/sdk/issues/29047")]
        public async Task Run_WithHotReloadEnabled_DoesNotReadConsoleIn_InNonInteractiveMode()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppWithLaunchSettings")
                .WithSource()
                .Path;

            App.EnvironmentVariables.Add("READ_INPUT", "true");
            App.DotnetWatchArgs.Add("--verbose");
            App.DotnetWatchArgs.Add("--non-interactive");

            await App.StartWatcherAsync(testAsset);

            var standardInput = App.Process.Process.StandardInput;
            var inputString = "This is a test input";

            await standardInput.WriteLineAsync(inputString);
            Assert.Equal(inputString, await App.AssertOutputLineStartsWith("Echo: "));
        }
    }
}
