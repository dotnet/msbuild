using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Compiler
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "dotnet publish";
            app.FullName = ".NET Publisher";
            app.Description = "Publisher for the .NET Platform";
            app.HelpOption("-h|--help");

            var verbose = app.Option("-v|--verbose", "Be more verbose", CommandOptionType.NoValue);
            var framework = app.Option("-f|--framework <FRAMEWORK>", "Target framework to compile for", CommandOptionType.SingleValue);
            var runtime = app.Option("-r|--runtime <RUNTIME_IDENTIFIER>", "Target runtime to publish for", CommandOptionType.SingleValue);
            var output = app.Option("-o|--output <OUTPUT_PATH>", "Path in which to publish the app", CommandOptionType.SingleValue);
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

                return Publish(path, framework.Value(), runtime.Value(), dir, output.Value());
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

        private static int Publish(string path, string framework, string runtime, DirectoryInfo projectDir, string outputPath)
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

            // Compile the project
            var result = Command.Create("dotnet-compile", $"--framework {framework} --output {outputPath} {path}")
                .ForwardStdErr()
                .ForwardStdOut()
                .RunAsync()
                .Result;
            if (result.ExitCode != 0)
            {
                Console.Error.WriteLine("Compilation failed!");
                return result.ExitCode;
            }

            // Collect the things needed to publish
            result = Command.Create("dotnet-resolve-references", $"--framework {framework} --runtime {runtime} --assets runtime --assets native {path}")
                .CaptureStdOut()
                .ForwardStdErr(Console.Error)
                .RunAsync()
                .Result;
            if (result.ExitCode != 0)
            {
                Console.Error.WriteLine("Failed to resolve references");
                return result.ExitCode;
            }
            var references = result.StdOut.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            // Copy everything to the output
            foreach (var reference in references)
            {
                Console.Error.WriteLine($"Publishing {reference} ...");
                File.Copy(reference, Path.Combine(outputPath, Path.GetFileName(reference)), overwrite: true);
            }

            // CoreConsole should be there...
            var coreConsole = Path.Combine(outputPath, Constants.CoreConsoleName);
            if (!File.Exists(coreConsole))
            {
                Console.Error.WriteLine($"Unable to find {Constants.CoreConsoleName} at {coreConsole}. You must have an explicit dependency on Microsoft.NETCore.ConsoleHost (for now ;))");
                return 1;
            }
            var outputExe = Path.Combine(outputPath, projectDir.Name + Constants.ExeSuffix);
            if (File.Exists(outputExe))
            {
                File.Delete(outputExe);
            }
            File.Move(coreConsole, outputExe);
            Console.Error.WriteLine($"Published to {outputExe}");
            return 0;
        }
    }
}
