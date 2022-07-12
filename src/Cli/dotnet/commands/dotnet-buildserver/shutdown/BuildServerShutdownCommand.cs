// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.BuildServer;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.BuildServer.Shutdown
{
    internal class BuildServerShutdownCommand : CommandBase
    {
        private readonly ServerEnumerationFlags _enumerationFlags;
        private readonly IBuildServerProvider _serverProvider;
        private readonly bool _useOrderedWait;
        private readonly IReporter _reporter;
        private readonly IReporter _errorReporter;

        public BuildServerShutdownCommand(
            ParseResult result,
            IBuildServerProvider serverProvider = null,
            bool useOrderedWait = false,
            IReporter reporter = null)
            : base(result)
        {
            bool msbuild = result.GetValueForOption(ServerShutdownCommandParser.MSBuildOption);
            bool vbcscompiler = result.GetValueForOption(ServerShutdownCommandParser.VbcsOption);
            bool razor = result.GetValueForOption(ServerShutdownCommandParser.RazorOption);
            bool all = !msbuild && !vbcscompiler && !razor;

            _enumerationFlags = ServerEnumerationFlags.None;
            if (msbuild || all)
            {
                _enumerationFlags |= ServerEnumerationFlags.MSBuild;
            }

            if (vbcscompiler || all)
            {
                _enumerationFlags |= ServerEnumerationFlags.VBCSCompiler;
            }

            if (razor || all)
            {
                _enumerationFlags |= ServerEnumerationFlags.Razor;
            }

            _serverProvider = serverProvider ?? new BuildServerProvider();
            _useOrderedWait = useOrderedWait;
            _reporter = reporter ?? Reporter.Output;
            _errorReporter = reporter ?? Reporter.Error;
        }

        public override int Execute()
        {
            var tasks = StartShutdown();

            if (tasks.Count == 0)
            {
                _reporter.WriteLine(LocalizableStrings.NoServersToShutdown.Green());
                return 0;
            }

            bool success = true;
            while (tasks.Count > 0)
            {
                var index = WaitForResult(tasks.Select(t => t.Item2).ToArray());
                var (server, task) = tasks[index];

                if (task.IsFaulted)
                {
                    success = false;
                    WriteFailureMessage(server, task.Exception);
                }
                else
                {
                    WriteSuccessMessage(server);
                }

                tasks.RemoveAt(index);
            }

            return success ? 0 : 1;
        }

        private List<(IBuildServer, Task)> StartShutdown()
        {
            var tasks = new List<(IBuildServer, Task)>();
            foreach (var server in _serverProvider.EnumerateBuildServers(_enumerationFlags))
            {
                WriteShutdownMessage(server);
                tasks.Add((server, Task.Run(() => server.Shutdown())));
            }

            return tasks;
        }

        private int WaitForResult(Task[] tasks)
        {
            if (_useOrderedWait)
            {
                return Task.WaitAny(tasks.First());
            }
            return Task.WaitAny(tasks);
        }

        private void WriteShutdownMessage(IBuildServer server)
        {
            if (server.ProcessId != 0)
            {
                _reporter.WriteLine(
                    string.Format(
                        LocalizableStrings.ShuttingDownServerWithPid,
                        server.Name,
                        server.ProcessId));
            }
            else
            {
                _reporter.WriteLine(
                    string.Format(
                        LocalizableStrings.ShuttingDownServer,
                        server.Name));
            }
        }

        private void WriteFailureMessage(IBuildServer server, AggregateException exception)
        {
            if (server.ProcessId != 0)
            {
                _reporter.WriteLine(
                    string.Format(
                        LocalizableStrings.ShutDownFailedWithPid,
                        server.Name,
                        server.ProcessId,
                        exception.InnerException.Message).Red());
            }
            else
            {
                _reporter.WriteLine(
                    string.Format(
                        LocalizableStrings.ShutDownFailed,
                        server.Name,
                        exception.InnerException.Message).Red());
            }

            if (Reporter.IsVerbose)
            {
                Reporter.Verbose.WriteLine(exception.ToString().Red());
            }
        }

        private void WriteSuccessMessage(IBuildServer server)
        {
            if (server.ProcessId != 0)
            {
                _reporter.WriteLine(
                    string.Format(
                        LocalizableStrings.ShutDownSucceededWithPid,
                        server.Name,
                        server.ProcessId).Green());
            }
            else
            {
                _reporter.WriteLine(
                    string.Format(
                        LocalizableStrings.ShutDownSucceeded,
                        server.Name).Green());
            }
        }
    }
}
