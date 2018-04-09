// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.BuildServer
{
    internal class VBCSCompilerServerManager : IBuildServerManager
    {
        internal static readonly string VBCSCompilerPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "Roslyn",
                "bincore",
                "VBCSCompiler.dll");

        private readonly ICommandFactory _commandFactory;

        public VBCSCompilerServerManager(ICommandFactory commandFactory = null)
        {
            _commandFactory = commandFactory ?? new DotNetCommandFactory(alwaysRunOutOfProc: true);
        }

        public string ServerName => LocalizableStrings.VBCSCompilerServer;

        public Task<Result> ShutdownServerAsync()
        {
            return Task.Run(() => {
                var command = _commandFactory
                    .Create("exec", new[] { VBCSCompilerPath, "-shutdown" })
                    .CaptureStdOut()
                    .CaptureStdErr();

                var result = command.Execute();
                if (result.ExitCode != 0)
                {
                    return new Result(ResultKind.Failure, result.StdErr);
                }

                return new Result(ResultKind.Success);
            });
        }
    }
}
