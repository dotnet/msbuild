using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.ProjectModel;
using Microsoft.Extensions.ProjectModel.Compilation;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Compiler
{
    public class Program
    {
        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            var app = new CommandLineApplication();
            app.Name = "dotnet compile";
            app.FullName = ".NET Compiler";
            app.Description = "Compiler for the .NET Platform";
            app.HelpOption("-h|--help");

            var output = app.Option("-o|--output <OUTPUT_DIR>", "Directory in which to place outputs", CommandOptionType.SingleValue);
            var framework = app.Option("-f|--framework <FRAMEWORK>", "Compile a specific framework", CommandOptionType.MultipleValue);
            var configuration = app.Option("-c|--configuration <CONFIGURATION>", "Configuration under which to build", CommandOptionType.SingleValue);
            var noProjectDependencies = app.Option("--no-project-dependencies", "Skips building project references.", CommandOptionType.NoValue);
            var project = app.Argument("<PROJECT>", "The project to compile, defaults to the current directory. Can be a path to a project.json or a project directory");

            app.OnExecute(() =>
            {
                // Locate the project and get the name and full path
                var path = project.Value;
                if (string.IsNullOrEmpty(path))
                {
                    path = Directory.GetCurrentDirectory();
                }

                var skipBuildingProjectReferences = noProjectDependencies.HasValue();

                // Load project contexts for each framework and compile them
                bool success = true;
                if (framework.HasValue())
                {
                    foreach (var context in framework.Values.Select(f => ProjectContext.Create(path, NuGetFramework.Parse(f))))
                    {
                        success &= Compile(context, configuration.Value() ?? Constants.DefaultConfiguration, output.Value(), skipBuildingProjectReferences);
                    }
                }
                else
                {
                    foreach (var context in ProjectContext.CreateContextForEachFramework(path))
                    {
                        success &= Compile(context, configuration.Value() ?? Constants.DefaultConfiguration, output.Value(), skipBuildingProjectReferences);
                    }
                }
                return success ? 0 : 1;
            });

            try
            {
                return app.Execute(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static bool Compile(ProjectContext context, string configuration, string outputPath, bool skipBuildingProjectReferences)
        {
            Reporter.Output.WriteLine($"Building {context.RootProject.Identity.Name.Yellow()} for {context.TargetFramework.DotNetFrameworkName.Yellow()}");

            // Create the library exporter
            var exporter = context.CreateExporter(configuration);

            bool success = true;

            // Print out dependency diagnostics
            foreach (var diag in context.LibraryManager.GetAllDiagnostics())
            {
                success &= diag.Severity != DiagnosticMessageSeverity.Error;
                Console.WriteLine(diag.FormattedMessage);
            }

            // If there were dependency errors don't bother compiling
            if (!success)
            {
                return false;
            }

            // Gather exports for the project
            var dependencies = exporter.GetCompilationDependencies().ToList();

            if (!skipBuildingProjectReferences)
            {
                var projects = new Dictionary<string, ProjectDescription>();

                // Build project references
                foreach (var dependency in dependencies.Where(d => d.CompilationAssemblies.Any()))
                {
                    var projectDependency = dependency.Library as ProjectDescription;

                    if (projectDependency != null)
                    {
                        projects[projectDependency.Identity.Name] = projectDependency;
                    }
                }

                foreach (var projectDependency in Sort(projects))
                {
                    // Skip compiling project dependencies since we've already figured out the build order
                    var compileResult = Command.Create("dotnet-compile", $"--framework {projectDependency.Framework} --configuration {configuration} --no-project-dependencies {projectDependency.Project.ProjectDirectory}")
                            .ForwardStdOut()
                            .ForwardStdErr()
                            .RunAsync()
                            .Result;

                    if (compileResult.ExitCode != 0)
                    {
                        Console.Error.WriteLine($"Failed to compile dependency: {projectDependency.Identity.Name}");
                        return false;
                    }
                }
            }

            // Dump dependency data
            // TODO: Turn on only if verbose, we can look at the response
            // file anyways
            // ShowDependencyInfo(dependencies);

            // Hackily generate the output path
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(
                    context.ProjectFile.ProjectDirectory,
                    Constants.BinDirectoryName,
                    configuration,
                    context.TargetFramework.GetTwoDigitShortFolderName());
            }

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Get compilation options
            var compilationOptions = context.ProjectFile.GetCompilerOptions(context.TargetFramework, configuration);
            var outputName = Path.Combine(outputPath, context.ProjectFile.Name + ".dll");

            // Assemble args
            var compilerArgs = new List<string>()
            {
                // Default suppressions
                "-nowarn:CS1701",
                "-nowarn:CS1702",
                "-nostdlib",
                "-nologo",
                $"-out:\"{outputName}\""
            };

            // Add compilation options to the args
            ApplyCompilationOptions(compilationOptions, compilerArgs);

            foreach (var dependency in dependencies)
            {
                compilerArgs.AddRange(dependency.CompilationAssemblies.Select(r => $"-r:\"{r}\""));
                compilerArgs.AddRange(dependency.SourceReferences);
            }

            // Add project source files
            compilerArgs.AddRange(context.ProjectFile.Files.SourceFiles);

            // TODO: Read this from the project
            const string compiler = "csc";

            // Write RSP file
            var rsp = Path.Combine(outputPath, $"dotnet-compile.{compiler}.rsp");
            File.WriteAllLines(rsp, compilerArgs);

            var result = Command.Create("dotnet-compile-csc", $"\"{rsp}\"")
                                 .ForwardStdErr()
                                 .ForwardStdOut()
                                 .RunAsync()
                                 .GetAwaiter()
                                 .GetResult();

            return result.ExitCode == 0;
        }

        private static ISet<ProjectDescription> Sort(Dictionary<string, ProjectDescription> projects)
        {
            var outputs = new HashSet<ProjectDescription>();

            foreach (var pair in projects)
            {
                Sort(pair.Value, projects, outputs);
            }

            return outputs;
        }

        private static void Sort(ProjectDescription project, Dictionary<string, ProjectDescription> projects, ISet<ProjectDescription> outputs)
        {
            // Sorts projects in dependency order so that we only build them once per chain
            foreach (var dependency in project.Dependencies)
            {
                ProjectDescription projectDependency;
                if (projects.TryGetValue(dependency.Name, out projectDependency))
                {
                    Sort(projectDependency, projects, outputs);
                }
            }

            outputs.Add(project);
        }

        private static void ApplyCompilationOptions(CompilerOptions compilationOptions, List<string> cscArgs)
        {
            // TODO: Move compilation arguments into the compiler itself
            var targetType = compilationOptions.EmitEntryPoint.GetValueOrDefault() ? "exe" : "library";

            cscArgs.Add($"-target:{targetType}");

            if (compilationOptions.AllowUnsafe.GetValueOrDefault())
            {
                cscArgs.Add("-unsafe+");
            }

            cscArgs.AddRange(compilationOptions.Defines.Select(d => $"-d:{d}"));

            if (compilationOptions.Optimize.GetValueOrDefault())
            {
                cscArgs.Add("-optimize");
            }

            if (!string.IsNullOrEmpty(compilationOptions.Platform))
            {
                cscArgs.Add($"-platform:{compilationOptions.Platform}");
            }

            if (compilationOptions.WarningsAsErrors.GetValueOrDefault())
            {
                cscArgs.Add("-warnaserror");
            }

            if (compilationOptions.DelaySign.GetValueOrDefault())
            {
                cscArgs.Add("-delaysign+");
            }

            if (!string.IsNullOrEmpty(compilationOptions.KeyFile))
            {
                cscArgs.Add($"-keyFile:\"{compilationOptions.KeyFile}\"");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                cscArgs.Add("-debug:full");
            }
            else
            {
                cscArgs.Add("-debug:portable");
            }
            
            // TODO: OSS signing
        }

        private static void ShowDependencyInfo(IEnumerable<LibraryExport> dependencies)
        {
            foreach (var dependency in dependencies)
            {
                if (!dependency.Library.Resolved)
                {
                    Reporter.Error.WriteLine($"  Unable to resolve dependency {dependency.Library.Identity.ToString().Red().Bold()}");
                    Reporter.Error.WriteLine("");
                }
                else
                {
                    Reporter.Output.WriteLine($"  Using {dependency.Library.Identity.Type.Value.Cyan().Bold()} dependency {dependency.Library.Identity.ToString().Cyan().Bold()}");
                    Reporter.Output.WriteLine($"    Path: {dependency.Library.Path}");

                    foreach (var metadataReference in dependency.CompilationAssemblies)
                    {
                        Reporter.Output.WriteLine($"    Assembly: {metadataReference}");
                    }

                    foreach (var sourceReference in dependency.SourceReferences)
                    {
                        Reporter.Output.WriteLine($"    Source: {sourceReference}");
                    }
                    Reporter.Output.WriteLine("");
                }
            }
        }
    }
}
