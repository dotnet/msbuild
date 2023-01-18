// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.Tools.Internal;
using Microsoft.NET.TestFramework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tests
{
    public class ProgramTests : DotNetWatchTestBase
    {
        private const string AppName = "WatchKitchenSink";

        public ProgramTests(ITestOutputHelper logger)
            : base(logger)
        {
        }

        [PlatformSpecificFact(TestPlatforms.Windows | TestPlatforms.Linux, Skip = "https://github.com/dotnet/aspnetcore/issues/23394")]
        public async Task ConsoleCancelKey()
        {
            var console = new TestConsole(Logger);
            var testAsset = TestAssets.CopyTestAsset(AppName)
                .WithSource()
                .Path;

            using var app = new Program(console, testAsset, "");

            var run = app.RunAsync(new[] { "run" });

            await console.CancelKeyPressSubscribed.TimeoutAfter(TimeSpan.FromSeconds(30));
            console.ConsoleCancelKey();

            var exitCode = await run.TimeoutAfter(TimeSpan.FromSeconds(30));

            Assert.Contains("Shutdown requested. Press Ctrl+C again to force exit.", console.GetOutput());
            Assert.Equal(0, exitCode);
        }
    }
}
