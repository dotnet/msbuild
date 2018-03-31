// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.BuildServer;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.BuildServer.Shutdown
{
    internal class BuildServerShutdownCommand : CommandBase
    {
        private readonly bool _useOrderedWait;
        private readonly IReporter _reporter;
        private readonly IReporter _errorReporter;

        public BuildServerShutdownCommand(
            AppliedOption options,
            ParseResult result,
            IEnumerable<IBuildServerManager> managers = null,
            bool useOrderedWait = false,
            IReporter reporter = null)
            : base(result)
        {
            if (managers == null)
            {
                bool msbuild = options.ValueOrDefault<bool>("msbuild");
                bool vbcscompiler  = options.ValueOrDefault<bool>("vbcscompiler");
                bool razor = options.ValueOrDefault<bool>("razor");
                bool all = !msbuild && !vbcscompiler && !razor;

                var enabledManagers = new List<IBuildServerManager>();
                if (msbuild || all)
                {
                    enabledManagers.Add(new MSBuildServerManager());
                }

                if (vbcscompiler || all)
                {
                    enabledManagers.Add(new VBCSCompilerServerManager());
                }

                if (razor || all)
                {
                    enabledManagers.Add(new RazorServerManager());
                }

                managers = enabledManagers;
            }

            Managers = managers;
            _useOrderedWait = useOrderedWait;
            _reporter = reporter ?? Reporter.Output;
            _errorReporter = reporter ?? Reporter.Error;
        }

        public IEnumerable<IBuildServerManager> Managers { get; }

        public override int Execute()
        {
            bool success = true;

            var tasks = StartShutdown();

            while (tasks.Count > 0)
            {
                var index = WaitForResult(tasks.Select(t => t.Item2).ToArray());
                var (manager, task) = tasks[index];

                success &= HandleResult(manager, task.Result);

                tasks.RemoveAt(index);
            }

            return success ? 0 : 1;
        }

        private List<(IBuildServerManager, Task<Result>)> StartShutdown()
        {
            var tasks = new List<(IBuildServerManager, Task<Result>)>();
            foreach (var manager in Managers)
            {
                _reporter.WriteLine(string.Format(LocalizableStrings.ShuttingDownServer, manager.ServerName));
                tasks.Add((manager, manager.ShutdownServerAsync()));
            }
            return tasks;
        }

        private int WaitForResult(Task[] tasks)
        {
            if (_useOrderedWait)
            {
                tasks[0].Wait();
                return 0;
            }
            return Task.WaitAny(tasks);
        }

        private bool HandleResult(IBuildServerManager manager, Result result)
        {
            switch (result.Kind)
            {
                case ResultKind.Success:
                    _reporter.WriteLine(
                        string.Format(
                            LocalizableStrings.ShutDownSucceeded,
                            manager.ServerName).Green());
                    return true;

                case ResultKind.Skipped:
                    _reporter.WriteLine(
                        string.Format(
                            LocalizableStrings.ShutDownSkipped,
                            manager.ServerName,
                            result.Message).Cyan());
                    return true;

                case ResultKind.Failure:
                    _errorReporter.WriteLine(
                            string.Format(
                                LocalizableStrings.ShutDownFailed,
                                manager.ServerName,
                                result.Message).Red());

                    if (Reporter.IsVerbose && result.Exception != null)
                    {
                        Reporter.Verbose.WriteLine(result.Exception.ToString().Red());
                    }
                    return false;

                default:
                    throw new NotSupportedException(
                        string.Format(
                            LocalizableStrings.UnsupportedEnumValue,
                            result.Kind.ToString(),
                            nameof(ResultKind)));
            }
        }
    }
}
