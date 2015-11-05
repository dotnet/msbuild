using System;
using System.IO;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Interactive.Csi
{
    public sealed class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication();
            app.Name = "dotnet interactive csi";
            app.FullName = "CSharp Interactive";
            app.Description = "CSharp Interactive for the .NET platform";
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
            var command = File.Exists(corerun) && File.Exists(csiExe) ?
                Command.Create(corerun, $@"""{csiExe}"" {csiArgs}") :
                Command.Create(csiExe, csiArgs);
            command = command.ForwardStdOut().ForwardStdErr();
            var result = command.Execute();
            return result.ExitCode;
        }
    }
}
