// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class WatchableApp : IDisposable
    {
        private static readonly TimeSpan DefaultMessageTimeOut = TimeSpan.FromSeconds(30);

        private const string StartedMessage = "Started";
        private const string ExitingMessage = "Exiting";
        private const string WatchExitedMessage = "watch : Exited";
        private const string WaitingForFileChangeMessage = "watch : Waiting for a file to change";

        private readonly ITestOutputHelper _logger;
        private bool _prepared;

        public WatchableApp(string sourceDirectory, ITestOutputHelper logger)
        {
            SourceDirectory = sourceDirectory;
            _logger = logger;
        }

        public AwaitableProcess Process { get; private set; }

        public List<string> DotnetWatchArgs { get; } = new List<string>();

        public Dictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>();

        public string SourceDirectory { get; }

        public Task HasRestarted()
            => HasRestarted(DefaultMessageTimeOut);

        public Task HasRestarted(TimeSpan timeout)
            => Process.GetOutputLineAsync(StartedMessage, timeout);

        public async Task HasExited()
        {
            await Process.GetOutputLineAsync(ExitingMessage, DefaultMessageTimeOut);
            await Process.GetOutputLineStartsWithAsync(WatchExitedMessage, DefaultMessageTimeOut);
        }

        public Task IsWaitingForFileChange()
        {
            return Process.GetOutputLineStartsWithAsync(WaitingForFileChangeMessage, DefaultMessageTimeOut);
        }

        public bool UsePollingWatcher { get; set; }

        public async Task<string> GetProcessIdentifier()
        {
            // Process ID is insufficient because PID's may be reused. Process identifier also includes other info to distinguish
            // between different process instances.
            var line = await Process.GetOutputLineStartsWithAsync("Process identifier =", DefaultMessageTimeOut);
            return line.Split('=').Last();
        }

        public void Start(IEnumerable<string> arguments, [CallerMemberName] string name = null)
        {
            var args = new List<string>
            {
                "watch",
            };
            args.AddRange(DotnetWatchArgs);
            args.AddRange(arguments);

            var commandSpec = new DotnetCommand(_logger, args.ToArray())
            {
                WorkingDirectory = SourceDirectory,
            };
            commandSpec.WithEnvironmentVariable("DOTNET_USE_POLLING_FILE_WATCHER", "true");
            commandSpec.WithEnvironmentVariable("__DOTNET_WATCH_RUNNING_AS_TEST", "true");

            foreach (var env in EnvironmentVariables)
            {
                commandSpec.WithEnvironmentVariable(env.Key, env.Value);
            }

            Process = new AwaitableProcess(commandSpec, _logger);
            Process.Start();
        }

        public Task StartWatcherAsync([CallerMemberName] string name = null)
            => StartWatcherAsync(Array.Empty<string>(), name);

        public void Prepare()
        {
            if (_prepared)
            {
                return;
            }

            var buildCommand = new BuildCommand(_logger, SourceDirectory);
            buildCommand.Execute().Should().Pass();

            _prepared = true;
        }

        public async Task StartWatcherAsync(string[] arguments, [CallerMemberName] string name = null)
        {
            Prepare();

            var args = new[] { "run", "--" }.Concat(arguments);
            Start(args, name);

            // Make this timeout long because it depends much on the MSBuild compilation speed.
            // Slow machines may take a bit to compile and boot test apps
            await Process.GetOutputLineAsync(StartedMessage, TimeSpan.FromMinutes(2));
        }

        public void Dispose()
        {
            _logger?.WriteLine("Disposing WatchableApp");
            Process?.Dispose();
        }
    }
}
