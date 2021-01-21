// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class NoDepsAppTests
    {
        private const string AppName = "WatchNoDepsApp";
        private readonly TestAssetsManager _testAssetsManager;
        private readonly ITestOutputHelper _output;

        public NoDepsAppTests(ITestOutputHelper logger)
        {
            _testAssetsManager = new TestAssetsManager(_output);
            _output = logger;
        }

        [Fact]
        public async Task RestartProcessOnFileChange()
        {
            var testAsset = _testAssetsManager.CopyTestAsset(AppName)
                .WithSource()
                .Path;

            using var app = new WatchableApp(testAsset, _output);

            await app.StartWatcherAsync(new[] { "--no-exit" });
            var processIdentifier = await app.GetProcessIdentifier();

            // Then wait for it to restart when we change a file
            var fileToChange = Path.Combine(app.SourceDirectory, "Program.cs");
            var programCs = File.ReadAllText(fileToChange);
            File.WriteAllText(fileToChange, programCs);

            await app.HasRestarted();
            Assert.DoesNotContain(app.Process.Output, l => l.StartsWith("Exited with error code"));

            var processIdentifier2 = await app.GetProcessIdentifier();
            Assert.NotEqual(processIdentifier, processIdentifier2);
        }

        [Fact]
        public async Task RestartProcessThatTerminatesAfterFileChange()
        {
            var testAsset = _testAssetsManager.CopyTestAsset(AppName)
                .WithSource()
                .Path;

            using var app = new WatchableApp(testAsset, _output);

            await app.StartWatcherAsync();
            var processIdentifier = await app.GetProcessIdentifier();
            await app.HasExited(); // process should exit after run
            await app.IsWaitingForFileChange();

            var fileToChange = Path.Combine(app.SourceDirectory, "Program.cs");

            try
            {
                File.SetLastWriteTime(fileToChange, DateTime.Now);
                await app.HasRestarted();
            }
            catch
            {
                // retry
                File.SetLastWriteTime(fileToChange, DateTime.Now);
                await app.HasRestarted();
            }

            var processIdentifier2 = await app.GetProcessIdentifier();
            Assert.NotEqual(processIdentifier, processIdentifier2);
            await app.HasExited(); // process should exit after run
        }
    }
}
