// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.CommandFactory;

namespace Microsoft.DotNet.BuildServer
{
    internal class VBCSCompilerServer : IBuildServer
    {
        internal static readonly string VBCSCompilerPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "Roslyn",
                "bincore",
                "VBCSCompiler.dll");

        private readonly ICommandFactory _commandFactory;

        public VBCSCompilerServer(ICommandFactory commandFactory = null)
        {
            _commandFactory = commandFactory ?? new DotNetCommandFactory(alwaysRunOutOfProc: true);
        }

        public int ProcessId => 0; // Not yet used

        public string Name => LocalizableStrings.VBCSCompilerServer;

        public void Shutdown()
        {
            var command = _commandFactory
                .Create("exec", new[] { VBCSCompilerPath, "-shutdown" })
                .CaptureStdOut()
                .CaptureStdErr();

            var result = command.Execute();
            if (result.ExitCode != 0)
            {
                throw new BuildServerException(
                    string.Format(
                        LocalizableStrings.ShutdownCommandFailed,
                        result.StdErr));
            }
        }
    }
}
