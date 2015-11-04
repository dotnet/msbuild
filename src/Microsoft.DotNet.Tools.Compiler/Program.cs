using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Dnx.Runtime.Common.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.Tools.Common;
using Microsoft.Extensions.ProjectModel;
using Microsoft.Extensions.ProjectModel.Compilation;
using Microsoft.Extensions.ProjectModel.Graph;
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
            var intermediateOutput = app.Option("-t|--temp-output <OUTPUT_DIR>", "Directory in which to place temporary outputs", CommandOptionType.SingleValue);
            var framework = app.Option("-f|--framework <FRAMEWORK>", "Compile a specific framework", CommandOptionType.MultipleValue);
            var configuration = app.Option("-c|--configuration <CONFIGURATION>", "Configuration under which to build", CommandOptionType.SingleValue);
            var noProjectDependencies = app.Option("--no-project-dependencies", "Skips building project references.", CommandOptionType.NoValue);
            var project = app.Argument("<PROJECT>", "The project to compile, defaults to the current directory. Can be a path to a project.json or a project directory");
            var native = app.Option("-n|--native", "Compiles source to native machine code.", CommandOptionType.NoValue);

            app.OnExecute(() =>
            {
                // Locate the project and get the name and full path
                var path = project.Value;
                if (string.IsNullOrEmpty(path))
                {
                    path = Directory.GetCurrentDirectory();
                }

                var buildProjectReferences = !noProjectDependencies.HasValue();
                var isNative = native.HasValue();

                // Load project contexts for each framework and compile them
                bool success = true;
                if (framework.HasValue())
                {
                    foreach (var context in framework.Values.Select(f => ProjectContext.Create(path, NuGetFramework.Parse(f))))
                    {
                        success &= Compile(context, configuration.Value() ?? Constants.DefaultConfiguration, output.Value(), intermediateOutput.Value(), buildProjectReferences);

                        if (isNative)
                        {
                            success &= CompileNative(context, configuration.Value() ?? Constants.DefaultConfiguration, output.Value(), buildProjectReferences);
                        }
                    }
                }
                else
                {
                    foreach (var context in ProjectContext.CreateContextForEachFramework(path))
                    {
                        success &= Compile(context, configuration.Value() ?? Constants.DefaultConfiguration, output.Value(), intermediateOutput.Value(), buildProjectReferences);

                        if (isNative)
                        {
                            success &= CompileNative(context, configuration.Value() ?? Constants.DefaultConfiguration, output.Value(), buildProjectReferences);
                        }
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

        private static bool CompileNative(ProjectContext context, string configuration, string outputOptionValue, bool buildProjectReferences)
        {
            string outputPath = Path.Combine(GetOutputPath(context, configuration, outputOptionValue), "native");

            var compilationOptions = context.ProjectFile.GetCompilerOptions(context.TargetFramework, configuration);
            var managedBinaryPath = Path.Combine(outputPath, context.ProjectFile.Name + (compilationOptions.EmitEntryPoint.GetValueOrDefault() ? ".exe" : ".dll"));

            // Do Native Compilation
            var result = Command.Create($"dotnet-compile-native", $"\"{managedBinaryPath}\" \"{outputPath}\"")
                                .ForwardStdErr()
                                .ForwardStdOut()
                                .Execute();

            return result.ExitCode == 0;
        }

        private static bool Compile(ProjectContext context, string configuration, string outputOptionValue, string intermediateOutputValue, bool buildProjectReferences)
        {
            // Set up Output Paths
            string outputPath = GetOutputPath(context, configuration, outputOptionValue);
            string intermediateOutputPath = GetIntermediateOutputPath(context, configuration, intermediateOutputValue, outputOptionValue);

            Directory.CreateDirectory(outputPath);
            Directory.CreateDirectory(intermediateOutputPath);

            // Create the library exporter
            var exporter = context.CreateExporter(configuration);

            var diagnostics = new List<DiagnosticMessage>();

            // Collect dependency diagnostics
            diagnostics.AddRange(context.LibraryManager.GetAllDiagnostics());

            // Gather exports for the project
            var dependencies = exporter.GetDependencies().ToList();

            if (buildProjectReferences)
            {
                var projects = new Dictionary<string, ProjectDescription>();

                // Build project references
                foreach (var dependency in dependencies)
                {
                    var projectDependency = dependency.Library as ProjectDescription;

                    if (projectDependency != null && projectDependency.Project.Files.SourceFiles.Any())
                    {
                        projects[projectDependency.Identity.Name] = projectDependency;
                    }
                }

                foreach (var projectDependency in Sort(projects))
                {
                    // Skip compiling project dependencies since we've already figured out the build order
                    var compileResult = Command.Create("dotnet-compile", $"--framework {projectDependency.Framework} --configuration {configuration} --output \"{outputPath}\" --temp-output \"{intermediateOutputPath}\" --no-project-dependencies \"{projectDependency.Project.ProjectDirectory}\"")
                            .ForwardStdOut()
                            .ForwardStdErr()
                            .Execute();

                    if (compileResult.ExitCode != 0)
                    {
                        return false;
                    }
                }

                projects.Clear();
            }

            Reporter.Output.WriteLine($"Compiling {context.RootProject.Identity.Name.Yellow()} for {context.TargetFramework.DotNetFrameworkName.Yellow()}");
            var sw = Stopwatch.StartNew();

            // Dump dependency data
            // TODO: Turn on only if verbose, we can look at the response
            // file anyways
            // ShowDependencyInfo(dependencies);

            // Get compilation options
            var outputName = GetProjectOutput(context.ProjectFile, context.TargetFramework, configuration, outputPath);

            // Assemble args
            var compilerArgs = new List<string>()
            {
                $"--temp-output:{intermediateOutputPath}",
                $"--out:{outputName}"
            };

            var compilationOptions = context.ProjectFile.GetCompilerOptions(context.TargetFramework, configuration);
            // Add compilation options to the args
            compilerArgs.AddRange(compilationOptions.SerializeToArgs());

            foreach (var dependency in dependencies)
            {
                var projectDependency = dependency.Library as ProjectDescription;

                if (projectDependency != null)
                {
                    if (projectDependency.Project.Files.SourceFiles.Any())
                    {
                        var projectOutputPath = GetProjectOutput(projectDependency.Project, projectDependency.Framework, configuration, outputPath);
                        compilerArgs.Add($"-r:{projectOutputPath}");
                    }
                }
                else
                {
                    compilerArgs.AddRange(dependency.CompilationAssemblies.Select(r => $"-r:{r.ResolvedPath}"));
                }
                compilerArgs.AddRange(dependency.SourceReferences);
            }

            // Add project source files
            var sourceFiles = context.ProjectFile.Files.SourceFiles;
            compilerArgs.AddRange(sourceFiles);

            if (!AddResources(context.ProjectFile, compilerArgs, intermediateOutputPath))
            {
                return false;
            }

            var compilerName = context.ProjectFile.CompilerName;
            compilerName = compilerName ?? "csc";

            // Write RSP file
            var rsp = Path.Combine(intermediateOutputPath, $"dotnet-compile.{context.ProjectFile.Name}.rsp");
            File.WriteAllLines(rsp, compilerArgs);

            var result = Command.Create($"dotnet-compile-{compilerName}", $"@\"{rsp}\"")
                                 .OnErrorLine(line =>
                                 {
                                     var diagnostic = ParseDiagnostic(context.ProjectDirectory, line);
                                     if (diagnostic != null)
                                     {
                                         diagnostics.Add(diagnostic);
                                     }
                                     else
                                     {
                                         Reporter.Error.WriteLine(line);
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
                                         Reporter.Output.WriteLine(line);
                                     }
                                 })
                                 .Execute();

            foreach (var diag in diagnostics)
            {
                PrintDiagnostic(diag);
            }

            var success = result.ExitCode == 0;

            if (success && compilationOptions.EmitEntryPoint.GetValueOrDefault())
            {
                var runtimeContext = ProjectContext.Create(context.ProjectDirectory, context.TargetFramework, new[] { RuntimeIdentifier.Current });
                MakeRunnable(runtimeContext,
                             outputPath,
                             runtimeContext.CreateExporter(configuration));
            }

            PrintSummary(success, diagnostics);

            Reporter.Output.WriteLine($"Time elapsed {sw.Elapsed}");
            Reporter.Output.WriteLine();

            return success;
        }

        private static string GetProjectOutput(Project project, NuGetFramework framework, string configuration, string outputPath)
        {
            var compilationOptions = project.GetCompilerOptions(framework, configuration);
            var outputExtension = ".dll";

            if (framework.IsDesktop() && compilationOptions.EmitEntryPoint.GetValueOrDefault())
            {
                outputExtension = ".exe";
            }

            return Path.Combine(outputPath, project.Name + outputExtension);
        }

        private static string GetOutputPath(ProjectContext context, string configuration, string outputOptionValue)
        {
            var outputPath = string.Empty;

            if (string.IsNullOrEmpty(outputOptionValue))
            {
                outputPath = Path.Combine(
                    GetDefaultRootOutputPath(context, outputOptionValue),
                    Constants.BinDirectoryName,
                    configuration,
                    context.TargetFramework.GetTwoDigitShortFolderName());
            }
            else
            {
                outputPath = outputOptionValue;
            }

            return outputPath;
        }

        private static string GetIntermediateOutputPath(ProjectContext context, string configuration, string intermediateOutputValue, string outputOptionValue)
        {
            var intermediateOutputPath = string.Empty;

            if (string.IsNullOrEmpty(intermediateOutputValue))
            {
                intermediateOutputPath = Path.Combine(
                    GetDefaultRootOutputPath(context, outputOptionValue),
                    Constants.ObjDirectoryName,
                    configuration,
                    context.TargetFramework.GetTwoDigitShortFolderName());
            }
            else
            {
                intermediateOutputPath = intermediateOutputValue;
            }

            return intermediateOutputPath;
        }

        private static string GetDefaultRootOutputPath(ProjectContext context, string outputOptionValue)
        {
            string rootOutputPath = string.Empty;

            if (string.IsNullOrEmpty(outputOptionValue))
            {
                rootOutputPath = context.ProjectFile.ProjectDirectory;
            }

            return rootOutputPath;
        }

        private static void CleanOrCreateDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, recursive: true);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Unable to remove directory: " + path);
                    Console.WriteLine(e.Message);
                }
            }

            Directory.CreateDirectory(path);
        }

        private static void MakeRunnable(ProjectContext runtimeContext, string outputPath, LibraryExporter exporter)
        {
            if (runtimeContext.TargetFramework.IsDesktop())
            {
                // On desktop we need to copy dependencies since we don't own the host
                foreach (var export in exporter.GetDependencies())
                {
                    CopyExport(outputPath, export);
                }
            }
            else
            {
                EmitHost(runtimeContext, outputPath, exporter);
            }
        }

        private static void CopyExport(string outputPath, LibraryExport export)
        {
            CopyFiles(export.RuntimeAssemblies, outputPath);
            CopyFiles(export.NativeLibraries, outputPath);
        }

        private static void EmitHost(ProjectContext runtimeContext, string outputPath, LibraryExporter exporter)
        {
            // Write the Host information file (basically a simplified form of the lock file)
            var lines = new List<string>();
            foreach (var export in exporter.GetAllExports())
            {
                if (export.Library == runtimeContext.RootProject)
                {
                    continue;
                }

                if (export.Library is ProjectDescription)
                {
                    // Copy project dependencies to the output folder
                    CopyFiles(export.RuntimeAssemblies, outputPath);
                    CopyFiles(export.NativeLibraries, outputPath);
                }
                else
                {
                    lines.AddRange(GenerateLines(export, export.RuntimeAssemblies, "runtime"));
                    lines.AddRange(GenerateLines(export, export.NativeLibraries, "native"));
                }
            }

            File.WriteAllLines(Path.Combine(outputPath, runtimeContext.ProjectFile.Name + ".deps"), lines);

            // Copy the host in
            CopyHost(Path.Combine(outputPath, runtimeContext.ProjectFile.Name + Constants.ExeSuffix));
        }

        private static void CopyHost(string target)
        {
            var hostPath = Path.Combine(AppContext.BaseDirectory, Constants.HostExecutableName);
            File.Copy(hostPath, target, overwrite: true);
        }

        private static IEnumerable<string> GenerateLines(LibraryExport export, IEnumerable<LibraryAsset> items, string type)
        {
            return items.Select(item =>
                EscapeCsv(export.Library.Identity.Type.Value) + "," +
                EscapeCsv(export.Library.Identity.Name) + "," +
                EscapeCsv(export.Library.Identity.Version.ToNormalizedString()) + "," +
                EscapeCsv(export.Library.Hash) + "," +
                EscapeCsv(type) + "," +
                EscapeCsv(item.Name) + "," +
                EscapeCsv(item.RelativePath) + ",");
        }

        private static string EscapeCsv(string input)
        {
            return "\"" + input.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static void PrintSummary(bool success, List<DiagnosticMessage> diagnostics)
        {
            Reporter.Output.WriteLine();

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

            Reporter.Output.WriteLine();
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

                        var result = Command.Create("resgen", $"\"{fileName}\" \"{resourcesFile}\"")
                                            .ForwardStdErr()
                                            .ForwardStdOut()
                                            .Execute();

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

        private static void CopyFiles(IEnumerable<LibraryAsset> files, string outputPath)
        {
            foreach (var file in files)
            {
                File.Copy(file.ResolvedPath, Path.Combine(outputPath, Path.GetFileName(file.ResolvedPath)), overwrite: true);
            }
        }
    }
}
