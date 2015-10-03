using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime.Common.CommandLine;

namespace Microsoft.DotNet.Cli
{
    public class Program
    {
        public static int Main(string[] args)
        {
            // TODO: Use System.CommandLine
            var app = new CommandLineApplication();
            app.Name = "dotnet";
            app.Description = "The .NET CLI";

            app.HelpOption("-?|-h|--help");

            app.Command("init", c =>
            {
                c.Description = "Scaffold a basic .NET application";

                c.HelpOption("-?|-h|--help");
            });

            app.Command("compile", c =>
            {
                c.Description = "Produce assemblies for the project in given directory";

                var optionFramework = c.Option("--framework <TARGET_FRAMEWORK>", "A list of target frameworks to build.", CommandOptionType.MultipleValue);
                var optionOut = c.Option("--out <OUTPUT_DIR>", "Output directory", CommandOptionType.SingleValue);
                var optionQuiet = c.Option("--quiet", "Do not show output such as dependencies in use",
                    CommandOptionType.NoValue);
                var argProjectDir = c.Argument(
                    "[projects]",
                    "One or more projects build. If not specified, the project in the current directory will be used.",
                    multipleValues: true);
                c.HelpOption("-?|-h|--help");
            });

            app.Command("restore", c =>
            {
                c.Description = "Restore packages";

                var argRoot = c.Argument("[project]",
                    "List of projects and project folders to restore. Each value can be: a path to a project.json or global.json file, or a folder to recursively search for project.json files.",
                    multipleValues: true);

                var optRuntimes = c.Option("--runtime <RID>",
                    "List of runtime identifiers to restore for",
                    CommandOptionType.MultipleValue);

                c.HelpOption("-?|-h|--help");
            });

            app.Command("pack", c =>
            {
                c.Description = "Produce a NuGet package";

                c.HelpOption("-?|-h|--help");
            });

            app.Command("publish", c =>
            {
                c.Description = "Produce deployable assets";

                c.HelpOption("-?|-h|--help");
            });

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 2;
            });


            return app.Execute(args);
        }
    }
}
