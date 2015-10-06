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
            app.Name = "dotnet compile";
            app.FullName = ".NET Compiler";
            app.Description = "Compiler for the .NET Platform";
            app.HelpOption("-h|--help");

            var output = app.Option("-o|--output <OUTPUT_DIR>", "Directory in which to place outputs", CommandOptionType.SingleValue);
            var framework = app.Option("-f|--framework <FRAMEWORK>", "Target framework to compile for", CommandOptionType.SingleValue);
            var project = app.Argument("<PROJECT>", "The project to compile, defaults to the current directory. Can be a path to a project.json or a project directory");

            app.OnExecute(() =>
            {
                // Validate arguments
                CheckArg(framework, "--framework");

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

                return Compile(path, framework.Value(), dir, output.Value());
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

        private static int Compile(string path, string framework, DirectoryInfo projectDir, string outputPath)
        {
            // Make output directory
            // TODO(anurse): per-framework and per-configuration output dir
            // TODO(anurse): configurable base output dir? (maybe dotnet compile doesn't support that?)
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(projectDir.FullName, "bin");
            }

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Resolve compilation references
            var result = Command.Create("dotnet-resolve-references", $"--framework {framework} --assets compile {path}")
                .CaptureStdOut()
                .ForwardStdErr(Console.Error)
                .RunAsync()
                .Result;
            if(result.ExitCode != 0)
            {
                Console.Error.WriteLine("Failed to resolve references");
                return result.ExitCode;
            }
            var references = result.StdOut.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            // Resolve source files
            result = Command.Create("dotnet-resolve-sources", $"{path}")
                .CaptureStdOut()
                .ForwardStdErr(Console.Error)
                .RunAsync()
                .Result;
            if(result.ExitCode != 0)
            {
                Console.Error.WriteLine("Failed to resolve sources");
                return result.ExitCode;
            }
            var sources = result.StdOut.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            // Build csc args
            var cscArgs = new[]
                {
                    "/nostdlib",
                    $"/out:{Path.Combine(outputPath, projectDir.Name + ".dll")}"
                }
                .Concat(references.Select(r => $"/r:{r}"))
                .Concat(sources);
            var rsp = Path.Combine(outputPath, "csc.rsp");
            if(File.Exists(rsp))
            {
                File.Delete(rsp);
            }
            File.WriteAllLines(rsp, cscArgs);

            // Run csc
            return Command.Create("csc", $"@{rsp}")
                .ForwardStdErr(Console.Error)
                .ForwardStdOut(Console.Out)
                .RunAsync()
                .Result
                .ExitCode;
        }
    }
}
