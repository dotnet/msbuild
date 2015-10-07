using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Run
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandLineApplication(throwOnUnexpectedArg: false);
            app.Name = "dotnet run";
            app.FullName = ".NET Executer";
            app.Description = "Executer for the .NET Platform";
            app.HelpOption("-h|--help");

            var framework = app.Option("-f|--framework <FRAMEWORK>", "Target framework to compile for", CommandOptionType.SingleValue);
            var runtime = app.Option("-r|--runtime <RUNTIME_IDENTIFIER>", "Target runtime on which to run", CommandOptionType.SingleValue);
            var output = app.Option("-o|--output <OUTPUT_DIR>", "Directory in which to compile the application", CommandOptionType.SingleValue);
            var project = app.Argument("<PROJECT>", "The project to compile, defaults to the current directory. Can be a path to a project.json or a project directory");

            app.OnExecute(() =>
            {
                // Validate arguments
                CheckArg(framework, "--framework");
                CheckArg(runtime, "--runtime");

                // Locate the project and get the name and full path
                var path = project.Value;
                if (string.IsNullOrEmpty(path))
                {
                    path = Directory.GetCurrentDirectory();
                }
                if (!string.Equals(Path.GetFileName(path), "project.json", StringComparison.OrdinalIgnoreCase))
                {
                    path = Path.Combine(path, "project.json");
                }
                if (!File.Exists(path))
                {
                    Console.Error.WriteLine($"Could not find project: {path}");
                    return 1;
                }
                var dir = new FileInfo(path).Directory;

                return Run(path, framework.Value(), runtime.Value(), dir, output.Value(), app.RemainingArguments);
            });

            try
            {
                return app.Execute(args);
            }
            catch (OperationCanceledException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static void CheckArg(CommandOption argument, string name)
        {
            if (!argument.HasValue())
            {
                // TODO: GROOOOOOSS
                throw new OperationCanceledException($"Missing required argument: {name}");
            }
        }

        private static int Run(string path, string framework, string runtime, DirectoryInfo projectDir, string outputPath, IEnumerable<string> remainingArgs)
        {
            // Make output directory
            // TODO(anurse): per-framework and per-configuration output dir
            // TODO(anurse): configurable base output dir? (maybe dotnet compile doesn't support that?)
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(projectDir.FullName, "bin", "publish");
            }

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Publish the app
            var result = Command.Create("dotnet-publish", $"--framework {framework} --runtime {runtime} --output {outputPath} {path}")
                .ForwardStdErr()
                .ForwardStdOut()
                .RunAsync()
                .Result;
            if(result.ExitCode != 0)
            {
                Console.Error.WriteLine("Error publishing app");
                return result.ExitCode;
            }

            // Run the output!
            var output = Path.Combine(outputPath, projectDir.Name + Constants.ExeSuffix);
            if(!File.Exists(output))
            {
                Console.Error.WriteLine($"Could not find output: {output}");
                return 1;
            }
            return Command.Create(output, remainingArgs)
                .ForwardStdErr()
                .ForwardStdOut()
                .RunAsync()
                .Result
                .ExitCode;
        }
    }
}
