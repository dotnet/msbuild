using System;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Interactive
{
    public sealed class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication();
            app.Name = "dotnet interactive";
            app.FullName = ".NET interactive";
            app.Description = "Interactive for the .NET platform";
            app.HelpOption("-h|--help");
            var language = app.Argument("<LANGUAGE>", "The interactive programming language, defaults to csharp");

            app.OnExecute(() => Run(language.Value));
            return app.Execute(args);
        }

        private static int Run(string languageOpt)
        {
            string interactiveName;
            if ((languageOpt == null) || (languageOpt == "csharp"))
            {
                interactiveName = "csi";
            }
            else
            {
                Reporter.Error.WriteLine($"Unrecognized language: {languageOpt}".Red());
                return -1;
            }
            var command = Command.Create($"dotnet-interactive-{interactiveName}", string.Empty)
                .ForwardStdOut()
                .ForwardStdErr();
            var result = command.Execute();
            return result.ExitCode;
        }
    }
}
