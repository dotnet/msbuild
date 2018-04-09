// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Build.Exceptions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.BuildServer
{
    internal class RazorServerManager : IBuildServerManager
    {
        private readonly IRazorAssemblyResolver _resolver;
        private readonly ICommandFactory _commandFactory;

        public RazorServerManager(IRazorAssemblyResolver resolver = null, ICommandFactory commandFactory = null)
        {
            _resolver = resolver ?? new RazorAssemblyResolver();
            _commandFactory = commandFactory ?? new DotNetCommandFactory(alwaysRunOutOfProc: true);
        }

        public string ServerName => LocalizableStrings.RazorServer;

        public Task<Result> ShutdownServerAsync()
        {
            return Task.Run(() => {
                try
                {
                    bool haveRazorAssembly = false;

                    foreach (var toolAssembly in _resolver.EnumerateRazorToolAssemblies())
                    {
                        haveRazorAssembly = true;

                        var command = _commandFactory
                            .Create("exec", new string[] { toolAssembly.Value, "shutdown" })
                            .CaptureStdOut()
                            .CaptureStdErr();

                        var result = command.Execute();
                        if (result.ExitCode != 0)
                        {
                            return new Result(ResultKind.Failure, result.StdErr);
                        }
                    }

                    if (!haveRazorAssembly)
                    {
                        return new Result(ResultKind.Skipped, LocalizableStrings.NoRazorProjectFound);
                    }

                    return new Result(ResultKind.Success);
                }
                catch (InvalidProjectFileException ex)
                {
                    return new Result(ex);
                }
            });
        }
    }
}
