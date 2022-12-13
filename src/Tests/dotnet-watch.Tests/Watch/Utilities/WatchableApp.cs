// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;

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

        public readonly ITestOutputHelper Logger;
        private bool _prepared;

        public WatchableApp(ITestOutputHelper logger)
        {
            Logger = logger;
        }

        public AwaitableProcess Process { get; private set; }

        public List<string> DotnetWatchArgs { get; } = new List<string>();

        public Dictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>();

        public bool UsePollingWatcher { get; set; }

        /// <summary>
        /// Asserts that the watched process outputs a line starting with <paramref name="expectedPrefix"/> and returns the remainder of that line.
        /// </summary>
        public async Task<string> AssertOutputLineStartsWith(string expectedPrefix)
        {
            var line = await Process.GetOutputLineAsync(
                success: line => line.StartsWith(expectedPrefix, StringComparison.Ordinal),
                failure: line => line.Contains(WatchErrorOutputEmoji, StringComparison.Ordinal));

            Assert.StartsWith(expectedPrefix, line, StringComparison.Ordinal);

            return line.Substring(expectedPrefix.Length);
        }

        public async Task AssertOutputLineEquals(string expectedLine)
            => Assert.Equal("", await AssertOutputLineStartsWith(expectedLine));

        public Task AssertRestarted()
            => AssertOutputLineEquals(StartedMessage);

        public Task AssertWaitingForFileChange()
            => AssertOutputLineStartsWith(WaitingForFileChangeMessage);

        public Task AssertFileChanged()
            => AssertOutputLineStartsWith(WatchFileChanged);

        public async Task AssertExited()
        {
            await AssertOutputLineStartsWith(ExitingMessage);
            await AssertOutputLineStartsWith(WatchExitedMessage);
        }

        // Process ID is insufficient because PID's may be reused. Process identifier also includes other info to distinguish
        // between different process instances.
        public async Task<string> ReadProcessIdentifierFromOutput()
            => await AssertOutputLineStartsWith("Process identifier =");

        public void Start(IEnumerable<string> arguments, string workingDirectory, [CallerMemberName] string name = null)
        {
            var args = new List<string>
            {
                "watch",
            };
            args.AddRange(DotnetWatchArgs);
            args.AddRange(arguments);

            var commandSpec = new DotnetCommand(Logger, args.ToArray())
            {
                WorkingDirectory = workingDirectory,
            };

            commandSpec.WithEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "true");
            commandSpec.WithEnvironmentVariable("__DOTNET_WATCH_RUNNING_AS_TEST", "true");

            foreach (var env in EnvironmentVariables)
            {
                commandSpec.WithEnvironmentVariable(env.Key, env.Value);
            }

            Process = new AwaitableProcess(commandSpec, Logger);
            Process.Start();
        }

        public void Prepare(string projectRootPath)
        {
            if (_prepared)
            {
                return;
            }

            var buildCommand = new BuildCommand(Logger, projectRootPath);
            buildCommand.Execute().Should().Pass();

            _prepared = true;
        }

        public async Task StartWatcherAsync(string projectRootPath, IEnumerable<string> arguments = null, string workingDirectory = null, [CallerMemberName] string name = null)
        {
            Prepare(projectRootPath);

            var args = new[] { "run", "--" };
            if (arguments != null)
            {
                args = args.Concat(arguments).ToArray();
            }

            Start(args, workingDirectory ?? projectRootPath, name);

            await AssertOutputLineStartsWith(WatchStartedMessage);
        }

        public void Dispose()
        {
            Process?.Dispose();
        }
    }
}
