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
        public ProgramTests(ITestOutputHelper logger)
            : base(logger)
        {
        }

        [PlatformSpecificFact(TestPlatforms.Windows | TestPlatforms.Linux, Skip = "https://github.com/dotnet/aspnetcore/issues/23394")]
        public async Task ConsoleCancelKey()
        {
            var console = new TestConsole(Logger);
            var testAsset = TestAssets.CopyTestAsset("WatchKitchenSink")
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

        [Theory]
        [InlineData(new[] { "--no-hot-reload", "run" }, "")]
        [InlineData(new[] { "--no-hot-reload", "run", "args" }, "args")]
        [InlineData(new[] { "--no-hot-reload", "--", "run", "args" }, "args")]
        [InlineData(new[] { "--no-hot-reload" }, "")]
        [InlineData(new string[] {}, "")]
        [InlineData(new[] { "run" }, "")]
        [InlineData(new[] { "run", "args" }, "args")]
        [InlineData(new[] { "--", "run", "args" }, "args")]
        public async Task Arguments(string[] arguments, string expectedApplicationArgs)
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp", identifier: string.Join(",", arguments))
                .WithSource()
                .Path;

            App.Start(testAsset, arguments);

            Assert.Equal(expectedApplicationArgs, await App.AssertOutputLineStartsWith("Arguments = "));
        }

        [Fact]
        public async Task OnlyRunSupportsHotReload()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp")
                .WithSource()
                .Path;

            App.Start(testAsset, new[] { "--verbose", "abc" });

           await App.AssertOutputLineStartsWith("dotnet watch ❌ Only 'run' command supports Hot Reload");
        }

        [Fact]
        public async Task RunArguments()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadAppMultiTfm")
                .WithSource()
                .Path;

            App.Start(testAsset, arguments: new[]
            {
                "run",
                "-f",         // dotnet watch does not recognize this arg -> dotnet run arg
                "net6.0",     
                "--",         // the following args are not dotnet watch args
                "-v",         // dotnet run arg
                "minimal",
                "--",         // the following args are not dotnet run args
                "-v",         // application arg
            });

            Assert.Equal("-v", await App.AssertOutputLineStartsWith("Arguments = "));
            Assert.Equal(".NETCoreApp,Version=v6.0", await App.AssertOutputLineStartsWith("TFM = "));

            // expected output from build (-v minimal):
            Assert.Contains(App.Process.Output, l => l.Contains("Determining projects to restore..."));

            // not expected to find verbose output of dotnet watch
            Assert.DoesNotContain(App.Process.Output, l => l.Contains("Running dotnet with the following arguments"));
        }
    }
}
