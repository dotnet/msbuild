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
                if(!CheckArg(framework))
                {
                    return 1;
                }
                if(!CheckArg(runtime))
                {
                    return 1;
                }

                // Locate the project and get the name and full path
                var path = project.Value;
                if (string.IsNullOrEmpty(path))
                {
                    path = Directory.GetCurrentDirectory();
                }

                // Load project context and publish it
                var fx = NuGetFramework.Parse(framework.Value());
                var rids = new[] { runtime.Value() };
                var context = ProjectContext.Create(path, fx, rids);
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

        private static bool CheckArg(CommandOption argument)
        {
            if (!argument.HasValue())
            {
                Reporter.Error.WriteLine($"Missing required argument: {argument.LongName.Red().Bold()}");
                return false;
            }
            return true;
        }

        private static int Publish(ProjectContext context, string outputPath, string configuration)
        {
            Reporter.Output.WriteLine($"Publishing {context.RootProject.Identity.Name.Yellow()} for {context.TargetFramework.DotNetFrameworkName.Yellow()}/{context.RuntimeIdentifier}");

            // Hackily generate the output path
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(
                    context.ProjectFile.ProjectDirectory,
                    Constants.BinDirectoryName,
                    configuration,
                    context.TargetFramework.GetTwoDigitShortFolderName(),
                    "publish");
            }
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Compile the project (and transitively, all it's dependencies)
            var result = Command.Create("dotnet-compile", $"--framework {context.TargetFramework.DotNetFrameworkName} {context.ProjectFile.ProjectDirectory}")
                .ForwardStdErr()
                .ForwardStdOut()
                .RunAsync()
                .Result;
            if (result.ExitCode != 0)
            {
                Reporter.Error.WriteLine("Compilation failed!".Red().Bold());
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

            // Locate CoreConsole
            string hostsPath = Environment.GetEnvironmentVariable(Constants.HostsPathEnvironmentVariable);
            if(string.IsNullOrEmpty(hostsPath))
            {
                hostsPath = AppContext.BaseDirectory;
            }
            var coreConsole = Path.Combine(hostsPath, Constants.CoreConsoleName);
            if(!File.Exists(coreConsole))
            {
                Reporter.Error.WriteLine($"Unable to locate CoreConsole.exe in {coreConsole}, use {Constants.HostsPathEnvironmentVariable} to set the path to it.".Red().Bold());
                return 1;
            }

            // TEMPORARILY bring CoreConsole along for the ride on it's own (without renaming)
            File.Copy(coreConsole, Path.Combine(outputPath, Constants.CoreConsoleName), overwrite: true);

            // Use the 'command' field to generate the name
            var outputExe = Path.Combine(outputPath, context.ProjectFile.Name + ".exe");
            File.Copy(coreConsole, outputExe, overwrite: true);

            // Check if the a command name is specified, and rename the necessary files
            if(context.ProjectFile.Commands.Count == 1)
            {
                var commandName = context.ProjectFile.Commands.Single().Key;

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

            Reporter.Output.WriteLine($"Published to {outputExe}".Green().Bold());
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
