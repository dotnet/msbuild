using System;
using System.IO;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Compiler.Csc
{
    public class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication();
            app.Name = "dotnet compile csc";
            app.FullName = "CSharp compiler";
            app.Description = "CSharp Compiler for the .NET Platform";
            app.HelpOption("-h|--help");

            var responseFileArg = app.Argument("<CONFIG>", "The response file to pass to the compiler.");

            app.OnExecute(() =>
            {
                // Execute CSC!
                var result = RunCsc($"-noconfig @\"{responseFileArg.Value}\"")
                    .ForwardStdErr()
                    .ForwardStdOut()
                    .Execute();

                return result.ExitCode;
            });

            return app.Execute(args);
        }

        private static Command RunCsc(string cscArgs)
        {
            // Locate CoreRun
            string hostRoot = Environment.GetEnvironmentVariable("DOTNET_CSC_PATH");
            if (string.IsNullOrEmpty(hostRoot))
            {
                hostRoot = AppContext.BaseDirectory;
            }
            var corerun = Path.Combine(hostRoot, Constants.HostExecutableName);
            var cscExe = Path.Combine(hostRoot, "csc.exe");
            return File.Exists(corerun) && File.Exists(cscExe)
                ? Command.Create(corerun, $@"""{cscExe}"" {cscArgs}")
                : Command.Create("csc", cscArgs);
        }
    }
}
