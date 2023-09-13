// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Watcher.Tools;

namespace Microsoft.DotNet.Watcher.Tests
{
    internal sealed class WatchableApp : IDisposable
    {
        private const string StartedMessage = "Started";
        private const string ExitingMessage = "Exiting";
        private const string WatchStartedMessage = "dotnet watch 🚀 Started";
        private const string WatchExitedMessage = "dotnet watch ⌚ Exited";
        private const string WatchErrorOutputEmoji = "❌";
        private const string WaitingForFileChangeMessage = "dotnet watch ⏳ Waiting for a file to change";
        private const string WatchFileChanged = "dotnet watch ⌚ File changed:";
        private const string HotReloadSessionStarted = "dotnet watch 🔥 Hot Reload session started.";

        public readonly ITestOutputHelper Logger;
        private bool _prepared;

        public WatchableApp(ITestOutputHelper logger)
        {
            Logger = logger;
        }

        public AwaitableProcess Process { get; private set; }

        public List<string> DotnetWatchArgs { get; } = new() { "--verbose" };

        public Dictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>();

        public bool UsePollingWatcher { get; set; }

        /// <summary>
        /// Asserts that the watched process outputs a line starting with <paramref name="expectedPrefix"/> and returns the remainder of that line.
        /// </summary>
        public async Task<string> AssertOutputLineStartsWith(string expectedPrefix, Predicate<string> failure = null)
        {
            var line = await Process.GetOutputLineAsync(
                success: line => line.StartsWith(expectedPrefix, StringComparison.Ordinal),
                failure: failure ?? new Predicate<string>(line => line.Contains(WatchErrorOutputEmoji, StringComparison.Ordinal)));

            if (line == null && failure != null)
            {
                Assert.Fail($"Failed to find expected text: '{expectedPrefix}'");
            }

            Assert.StartsWith(expectedPrefix, line, StringComparison.Ordinal);

            return line.Substring(expectedPrefix.Length);
        }

        public async Task AssertOutputLineEquals(string expectedLine)
            => Assert.Equal("", await AssertOutputLineStartsWith(expectedLine));

        public Task AssertRestarted()
            => AssertOutputLineEquals(StartedMessage);

        /// <summary>
        ///  Must be called before updating any source files.
        ///  Document content is captured at session start and if any source files are updated before that
        ///  changes will not be detected since the captured file content won't match the PDB checksums.
        /// </summary>
        public Task WaitForSessionStarted()
            => AssertOutputLineStartsWith(HotReloadSessionStarted);

        public Task AssertWaitingForFileChange()
            => AssertOutputLineStartsWith(WaitingForFileChangeMessage);

        public Task AssertFileChanged()
            => AssertOutputLineStartsWith(WatchFileChanged);

        public async Task AssertExited()
        {
            await AssertOutputLineStartsWith(ExitingMessage);
            await AssertOutputLineStartsWith(WatchExitedMessage);
        }

        private void Prepare(string projectDirectory)
        {
            if (_prepared)
            {
                return;
            }

            var buildCommand = new BuildCommand(Logger, projectDirectory);
            buildCommand.Execute().Should().Pass();

            _prepared = true;
        }

        public void Start(TestAsset asset, IEnumerable<string> arguments, string relativeProjectDirectory = null, string workingDirectory = null, TestFlags testFlags = TestFlags.RunningAsTest)
        {
            var projectDirectory = (relativeProjectDirectory != null) ? Path.Combine(asset.Path, relativeProjectDirectory) : asset.Path;

            Prepare(projectDirectory);

            var args = new List<string>
            {
                "watch",
            };
            args.AddRange(DotnetWatchArgs);
            args.AddRange(arguments);

            var commandSpec = new DotnetCommand(Logger, args.ToArray())
            {
                WorkingDirectory = workingDirectory ?? projectDirectory,
            };

            commandSpec.WithEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "true");
            commandSpec.WithEnvironmentVariable("__DOTNET_WATCH_TEST_FLAGS", testFlags.ToString());

            var encLogPath = Environment.GetEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT") is { } ciOutputRoot
                ? Path.Combine(ciOutputRoot, ".hotreload", asset.Name)
                : Path.Combine(asset.Path, ".hotreload");

            commandSpec.WithEnvironmentVariable("Microsoft_CodeAnalysis_EditAndContinue_LogDir", encLogPath);

            foreach (var env in EnvironmentVariables)
            {
                commandSpec.WithEnvironmentVariable(env.Key, env.Value);
            }

            Process = new AwaitableProcess(commandSpec, Logger);
            Process.Start();
        }

        public async Task StartWatcherAsync(
            TestAsset asset,
            string relativeProjectDirectory = null,
            IEnumerable<string> applicationArguments = null,
            string workingDirectory = null,
            TestFlags testFlags = TestFlags.RunningAsTest)
        {
            var args = new[] { "run", "--" };
            if (applicationArguments != null)
            {
                args = args.Concat(applicationArguments).ToArray();
            }

            Start(asset, args, relativeProjectDirectory, workingDirectory, testFlags);

            await AssertOutputLineStartsWith(WatchStartedMessage);
        }

        public void Dispose()
        {
            Process?.Dispose();
        }
    }
}
