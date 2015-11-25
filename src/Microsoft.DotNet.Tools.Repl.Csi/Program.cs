// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Repl.Csi
{
    public sealed class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication();
            app.Name = "dotnet repl csi";
            app.FullName = "C# REPL";
            app.Description = "C# REPL for the .NET platform";
            app.HelpOption("-h|--help");
            var script = app.Argument("<SCRIPT>", "The .csx file to run. Defaults to interactive mode.");

            app.OnExecute(() => Run(script.Value));
            return app.Execute(args);
        }

        private static int Run(string scriptOpt)
        {
            var corerun = Path.Combine(AppContext.BaseDirectory, Constants.HostExecutableName);
            var csiExe = Path.Combine(AppContext.BaseDirectory, "csi.exe");
            var csiArgs = string.IsNullOrEmpty(scriptOpt) ? "-i" : scriptOpt;
            var result = Command.Create(csiExe, csiArgs)
                .ForwardStdOut()
                .ForwardStdErr()
                .Execute();
            return result.ExitCode;
        }
    }
}
