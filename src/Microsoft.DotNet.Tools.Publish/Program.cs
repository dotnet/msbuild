using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.ProjectModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Publish
{
    public class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication();
            app.Name = "dotnet publish";
            app.FullName = ".NET Publisher";
            app.Description = "Publisher for the .NET Platform";
            app.HelpOption("-h|--help");

            var framework = app.Option("-f|--framework <FRAMEWORK>", "Target framework to compile for", CommandOptionType.SingleValue);
            var runtime = app.Option("-r|--runtime <RUNTIME_IDENTIFIER>", "Target runtime to publish for", CommandOptionType.SingleValue);
            var output = app.Option("-o|--output <OUTPUT_PATH>", "Path in which to publish the app", CommandOptionType.SingleValue);
            var configuration = app.Option("-c|--configuration <CONFIGURATION>", "Configuration under which to build", CommandOptionType.SingleValue);
            var project = app.Argument("<PROJECT>", "The project to publish, defaults to the current directory. Can be a path to a project.json or a project directory");

            app.OnExecute(() =>
            {
                CheckArg(framework);
                CheckArg(runtime);

                // Locate the project and get the name and full path
                var path = project.Value;
                if (string.IsNullOrEmpty(path))
                {
                    path = Directory.GetCurrentDirectory();
                }

                // Load project context and publish it
                var context = ProjectContext.Create(path, NuGetFramework.Parse(framework.Value()), new[] { runtime.Value() });
                return Publish(context, output.Value(), configuration.Value() ?? Constants.DefaultConfiguration);
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

        private static void CheckArg(CommandOption argument)
        {
            if (!argument.HasValue())
            {
                // TODO: GROOOOOOSS
                throw new OperationCanceledException($"Missing required argument: {argument.LongName}");
            }
        }

        private static int Publish(ProjectContext context, string outputPath, string configuration)
        {
            Reporter.Output.WriteLine($"Publishing {context.RootProject.Identity.Name.Yellow()} for {context.TargetFramework.DotNetFrameworkName.Yellow()}/{context.RuntimeIdentifier}");

            // Hackily generate the output path
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(
                    context.Project.ProjectDirectory,
                    "bin",
                    configuration,
                    context.TargetFramework.GetTwoDigitShortFolderName(),
                    "publish");
            }
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Compile the project (and transitively, all it's dependencies)
            var result = Command.Create("dotnet-compile", $"--framework {context.TargetFramework.DotNetFrameworkName} {context.Project.ProjectDirectory}")
                .ForwardStdErr()
                .ForwardStdOut()
                .RunAsync()
                .Result;
            if (result.ExitCode != 0)
            {
                Console.Error.WriteLine("Compilation failed!");
                return result.ExitCode;
            }

            // Use a library exporter to collect publish assets
            var exporter = context.CreateExporter(configuration);
            foreach (var export in exporter.GetAllExports())
            {
                Reporter.Output.WriteLine($"Publishing {export.Library.Identity.ToString().Green().Bold()} ...");

                PublishFiles(export.RuntimeAssemblies, outputPath);
                PublishFiles(export.NativeLibraries, outputPath);
            }

            // Publishing for windows, TODO(anurse): Publish for Mac/Linux/etc.

            // CoreConsole should be there...
            var coreConsole = Path.Combine(outputPath, Constants.CoreConsoleName);
            if (!File.Exists(coreConsole))
            {
                Console.Error.WriteLine($"Unable to find {Constants.CoreConsoleName} at {coreConsole}. You must have an explicit dependency on Microsoft.NETCore.ConsoleHost (for now ;))");
                return 1;
            }

            // Allow CoreConsole to be replaced
            string overrideCoreConsole = Environment.GetEnvironmentVariable("DOTNET_CORE_CONSOLE_PATH");
            if(!string.IsNullOrEmpty(overrideCoreConsole) && File.Exists(overrideCoreConsole))
            {
                Console.WriteLine($"Using CoreConsole override: {overrideCoreConsole}");
                File.Copy(overrideCoreConsole, coreConsole, overwrite: true);
            }

            // Use the 'command' field to generate the name
            var outputExe = Path.Combine(outputPath, context.Project.Name + ".exe");
            if (File.Exists(outputExe))
            {
                File.Delete(outputExe);
            }
            File.Move(coreConsole, outputExe);

            // Check if the a command name is specified, and rename the necessary files
            if(context.Project.Commands.Count == 1)
            {
                var commandName = context.Project.Commands.Single().Key;

                // Move coreconsole and the matching dll
                var renamedExe = Path.Combine(outputPath, commandName + ".exe");
                var renamedDll = Path.ChangeExtension(renamedExe, ".dll");
                if(File.Exists(renamedExe))
                {
                    File.Delete(renamedExe);
                }
                File.Move(outputExe, renamedExe);
                File.Move(Path.ChangeExtension(outputExe, ".dll"), renamedDll);
                outputExe = Path.Combine(outputPath, commandName + ".exe");
            }

            Console.Error.WriteLine($"Published to {outputExe}");
            return 0;
        }

        private static void PublishFiles(IEnumerable<string> files, string outputPath)
        {
            foreach (var file in files)
            {
                File.Copy(file, Path.Combine(outputPath, Path.GetFileName(file)), overwrite: true);
            }
        }
    }
}
