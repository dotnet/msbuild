// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.DotNet.Cli;
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
