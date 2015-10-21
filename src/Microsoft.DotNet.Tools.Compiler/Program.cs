using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
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

                var buildProjectReferences = !noProjectDependencies.HasValue();

                // Load project contexts for each framework and compile them
                bool success = true;
                if (framework.HasValue())
                {
                    foreach (var context in framework.Values.Select(f => ProjectContext.Create(path, NuGetFramework.Parse(f))))
                    {
                        success &= Compile(context, configuration.Value() ?? Constants.DefaultConfiguration, output.Value(), buildProjectReferences);
                    }
                }
                else
                {
                    foreach (var context in ProjectContext.CreateContextForEachFramework(path))
                    {
                        success &= Compile(context, configuration.Value() ?? Constants.DefaultConfiguration, output.Value(), buildProjectReferences);
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
#if DEBUG
                Console.Error.WriteLine(ex);
#else
                Console.Error.WriteLine(ex.Message);
#endif
                return 1;
            }
        }

        private static bool Compile(ProjectContext context, string configuration, string outputPath, bool buildProjectReferences)
        {
            Reporter.Output.WriteLine($"Compiling {context.RootProject.Identity.Name.Yellow()} for {context.TargetFramework.DotNetFrameworkName.Yellow()}");

            // Create the library exporter
            var exporter = context.CreateExporter(configuration);

            var diagnostics = new List<DiagnosticMessage>();

            bool success = true;

            // Collect dependency diagnostics
            diagnostics.AddRange(context.LibraryManager.GetAllDiagnostics());

            // Gather exports for the project
            var dependencies = exporter.GetCompilationDependencies().ToList();

            if (buildProjectReferences)
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
                        return false;
                    }
                }

                projects.Clear();
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

            string intermediateOutputPath = Path.Combine(
                    context.ProjectFile.ProjectDirectory,
                    Constants.ObjDirectoryName,
                    configuration,
                    context.TargetFramework.GetTwoDigitShortFolderName());

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            if (!Directory.Exists(intermediateOutputPath))
            {
                Directory.CreateDirectory(intermediateOutputPath);
            }

            // Get compilation options
            var compilationOptions = context.ProjectFile.GetCompilerOptions(context.TargetFramework, configuration);
            var outputName = Path.Combine(outputPath, context.ProjectFile.Name + (compilationOptions.EmitEntryPoint.GetValueOrDefault() ? ".exe" : ".dll"));
            
            // Assemble args
            var compilerArgs = new List<string>()
            {
                "-nostdlib",
                "-nologo",
                $"-out:\"{outputName}\""
            };

            // Default suppressions, some versions of mono don't support these
            compilerArgs.Add("-nowarn:CS1701");
            compilerArgs.Add("-nowarn:CS1702");
            compilerArgs.Add("-nowarn:CS1705");
            
            // Add compilation options to the args
            ApplyCompilationOptions(compilationOptions, compilerArgs);

            foreach (var dependency in dependencies)
            {
                compilerArgs.AddRange(dependency.CompilationAssemblies.Select(r => $"-r:\"{r}\""));
                compilerArgs.AddRange(dependency.SourceReferences);
            }

            // Add project source files
            compilerArgs.AddRange(context.ProjectFile.Files.SourceFiles);

            if (!AddResources(context.ProjectFile, compilerArgs, intermediateOutputPath))
            {
                return false;
            }

            // TODO: Read this from the project
            const string compiler = "csc";

            // Write RSP file
            var rsp = Path.Combine(intermediateOutputPath, $"dotnet-compile.{compiler}.rsp");
            File.WriteAllLines(rsp, compilerArgs);

            var result = Command.Create($"dotnet-compile-{compiler}", $"\"{rsp}\"")
                                 .OnErrorLine(line => 
                                 {
                                     var diagnostic = ParseDiagnostic(context.ProjectDirectory, line);
                                     if (diagnostic != null)
                                     {
                                         diagnostics.Add(diagnostic);
                                     }
                                     else
                                     {
                                         Console.Error.WriteLine(line);
                                     }
                                 })
                                 .OnOutputLine(line =>
                                 {
                                     var diagnostic = ParseDiagnostic(context.ProjectDirectory, line);

                                     if (diagnostic != null)
                                     {
                                         diagnostics.Add(diagnostic);
                                     }
                                     else
                                     {
                                         Console.Out.WriteLine(line);
                                     }
                                 })
                                 .RunAsync()
                                 .GetAwaiter()
                                 .GetResult();

            foreach (var diag in diagnostics)
            {
                success &= diag.Severity != DiagnosticMessageSeverity.Error;
                PrintDiagnostic(diag);
            }
            
            success &= result.ExitCode == 0;

            PrintSummary(diagnostics);

            return success;
        }

        private static void PrintSummary(List<DiagnosticMessage> diagnostics)
        {
            Reporter.Output.Writer.WriteLine();

            var errorCount = diagnostics.Count(d => d.Severity == DiagnosticMessageSeverity.Error);
            var warningCount = diagnostics.Count(d => d.Severity == DiagnosticMessageSeverity.Warning);

            if (errorCount > 0)
            {
                Reporter.Output.WriteLine("Compilation failed.".Red());
            }
            else
            {
                Reporter.Output.WriteLine("Compilation succeeded.".Green());
            }

            Reporter.Output.WriteLine($"    {warningCount} Warning(s)");
            Reporter.Output.WriteLine($"    {errorCount} Error(s)");

            Reporter.Output.Writer.WriteLine();
        }

        private static bool AddResources(Project project, List<string> compilerArgs, string intermediateOutputPath)
        {
            string root = PathUtility.EnsureTrailingSlash(project.ProjectDirectory);

            foreach (var resourceFile in project.Files.ResourceFiles)
            {
                string resourceName = null;
                string rootNamespace = null;

                var resourcePath = resourceFile.Key;

                if (string.IsNullOrEmpty(resourceFile.Value))
                {
                    // No logical name, so use the file name
                    resourceName = ResourcePathUtility.GetResourceName(root, resourcePath);
                    rootNamespace = project.Name;
                }
                else
                {
                    resourceName = CreateCSharpManifestResourceName.EnsureResourceExtension(resourceFile.Value, resourcePath);
                    rootNamespace = null;
                }

                var name = CreateCSharpManifestResourceName.CreateManifestName(resourceName, rootNamespace);
                var fileName = resourcePath;

                if (ResourcePathUtility.IsResxResourceFile(fileName))
                {
                    var ext = Path.GetExtension(fileName);

                    if (string.Equals(ext, ".resx", StringComparison.OrdinalIgnoreCase))
                    {
                        // {file}.resx -> {file}.resources
                        var resourcesFile = Path.Combine(intermediateOutputPath, name);

                        var result = Command.Create("resgen", $"{fileName} {resourcesFile}")
                                            .ForwardStdErr()
                                            .ForwardStdOut()
                                            .RunAsync()
                                            .GetAwaiter()
                                            .GetResult();

                        if (result.ExitCode != 0)
                        {
                            return false;
                        }

                        // Use this as the resource name instead
                        fileName = resourcesFile;
                    }
                }

                compilerArgs.Add($"-resource:\"{fileName}\",{name}");
            }

            return true;
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

        private static DiagnosticMessage ParseDiagnostic(string projectRootPath, string line)
        {
            var error = CanonicalError.Parse(line);

            if (error != null)
            {
                var severity = error.category == CanonicalError.Parts.Category.Error ?
                DiagnosticMessageSeverity.Error : DiagnosticMessageSeverity.Warning;
                
                return new DiagnosticMessage(
                    error.code,
                    error.text,
                    Path.IsPathRooted(error.origin) ? line : projectRootPath + Path.DirectorySeparatorChar + line,
                    Path.Combine(projectRootPath, error.origin),
                    severity,
                    error.line,
                    error.column,
                    error.endColumn,
                    error.endLine,
                    source: null);
            }

            return null;
        }
        
        private static void PrintDiagnostic(DiagnosticMessage diag)
        {
            switch (diag.Severity)
            {
                case DiagnosticMessageSeverity.Info:
                    Reporter.Error.WriteLine(diag.FormattedMessage);
                    break;
                case DiagnosticMessageSeverity.Warning:
                    Reporter.Error.WriteLine(diag.FormattedMessage.Yellow().Bold());
                    break;
                case DiagnosticMessageSeverity.Error:
                    Reporter.Error.WriteLine(diag.FormattedMessage.Red().Bold());
                    break;
            }
        }

        private static void ApplyCompilationOptions(CompilerOptions compilationOptions, List<string> compilerArgs)
        {
            var targetType = compilationOptions.EmitEntryPoint.GetValueOrDefault() ? "exe" : "library";

            compilerArgs.Add($"-target:{targetType}");

            if (compilationOptions.AllowUnsafe.GetValueOrDefault())
            {
                compilerArgs.Add("-unsafe+");
            }

            compilerArgs.AddRange(compilationOptions.Defines.Select(d => $"-d:{d}"));

            if (compilationOptions.Optimize.GetValueOrDefault())
            {
                compilerArgs.Add("-optimize");
            }

            if (!string.IsNullOrEmpty(compilationOptions.Platform))
            {
                compilerArgs.Add($"-platform:{compilationOptions.Platform}");
            }

            if (compilationOptions.WarningsAsErrors.GetValueOrDefault())
            {
                compilerArgs.Add("-warnaserror");
            }

            if (compilationOptions.DelaySign.GetValueOrDefault())
            {
                compilerArgs.Add("-delaysign+");
            }

            if (!string.IsNullOrEmpty(compilationOptions.KeyFile))
            {
                compilerArgs.Add($"-keyFile:\"{compilationOptions.KeyFile}\"");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                compilerArgs.Add("-debug:full");
            }
            else
            {
                compilerArgs.Add("-debug:portable");
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
