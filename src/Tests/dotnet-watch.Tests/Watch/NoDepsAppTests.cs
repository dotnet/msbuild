// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher.Tests
{
    public class NoDepsAppTests : DotNetWatchTestBase
    {
        private const string AppName = "WatchNoDepsApp";

        public NoDepsAppTests(ITestOutputHelper logger)
            : base(logger)
        {
        }

        [Fact]
        public async Task RestartProcessOnFileChange()
        {
            var testAsset = TestAssets.CopyTestAsset(AppName)
                .WithSource();

            await App.StartWatcherAsync(testAsset, applicationArguments: ["--no-hot-reload", "--no-exit"]);
            var processIdentifier = await App.AssertOutputLineStartsWith("Process identifier =");

            // Then wait for it to restart when we change a file
            var fileToChange = Path.Combine(testAsset.Path, "Program.cs");
            var programCs = File.ReadAllText(fileToChange);
            File.WriteAllText(fileToChange, programCs);

            await App.AssertRestarted();
            Assert.DoesNotContain(App.Process.Output, l => l.StartsWith("Exited with error code"));

            var processIdentifier2 = await App.AssertOutputLineStartsWith("Process identifier =");
            Assert.NotEqual(processIdentifier, processIdentifier2);
        }

        [Fact(Skip = "https://github.com/dotnet/sdk/issues/29046")]
        public async Task RestartProcessThatTerminatesAfterFileChange()
        {
            var testAsset = TestAssets.CopyTestAsset(AppName)
                .WithSource();

            await App.StartWatcherAsync(testAsset);
            var processIdentifier = await App.AssertOutputLineStartsWith("Process identifier =");
            await App.AssertExited(); // process should exit after run
            await App.AssertWaitingForFileChange();

            var fileToChange = Path.Combine(testAsset.Path, "Program.cs");

            try
            {
                File.SetLastWriteTime(fileToChange, DateTime.Now);
                await App.AssertRestarted();
            }
            catch
            {
                // retry
                File.SetLastWriteTime(fileToChange, DateTime.Now);
                await App.AssertRestarted();
            }

            var processIdentifier2 = await App.AssertOutputLineStartsWith("Process identifier =");
            Assert.NotEqual(processIdentifier, processIdentifier2);
            await App.AssertExited(); // process should exit after run
        }
    }
}
