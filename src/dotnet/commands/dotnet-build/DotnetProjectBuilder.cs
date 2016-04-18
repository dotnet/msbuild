using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Compiler;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Tools.Build
{
    class DotNetProjectBuilder : ProjectBuilder
    {
        private readonly BuilderCommandApp _args;
        private readonly IncrementalPreconditionManager _preconditionManager;
        private readonly CompilerIOManager _compilerIOManager;
        private readonly ScriptRunner _scriptRunner;
        private readonly DotNetCommandFactory _commandFactory;

        public DotNetProjectBuilder(BuilderCommandApp args) : base(args.ShouldSkipDependencies)
        {
            _args = (BuilderCommandApp)args.ShallowCopy();
            _preconditionManager = new IncrementalPreconditionManager(
                args.ShouldPrintIncrementalPreconditions,
                args.ShouldNotUseIncrementality,
                args.ShouldSkipDependencies);
            _compilerIOManager = new CompilerIOManager(
                args.ConfigValue,
                args.OutputValue,
                args.BuildBasePathValue,
                args.GetRuntimes()
                );
            _scriptRunner = new ScriptRunner();
            _commandFactory = new DotNetCommandFactory();
        }

        private void StampProjectWithSDKVersion(ProjectContext project)
        {
            if (File.Exists(DotnetFiles.VersionFile))
            {
                var projectVersionFile = project.GetSDKVersionFile(_args.ConfigValue, _args.BuildBasePathValue, _args.OutputValue);
                var parentDirectory = Path.GetDirectoryName(projectVersionFile);

                if (!Directory.Exists(parentDirectory))
                {
                    Directory.CreateDirectory(parentDirectory);
                }

                string content = ComputeCurrentVersionFileData();

                File.WriteAllText(projectVersionFile, content);
            }
            else
            {
                Reporter.Verbose.WriteLine($"Project {project.GetDisplayName()} was not stamped with a CLI version because the version file does not exist: {DotnetFiles.VersionFile}");
            }
        }

        private static string ComputeCurrentVersionFileData()
        {
            var content = File.ReadAllText(DotnetFiles.VersionFile);
            content += Environment.NewLine;
            content += PlatformServices.Default.Runtime.GetRuntimeIdentifier();
            return content;
        }

        private void PrintSummary(ProjectGraphNode projectNode, bool success)
        {
            // todo: Ideally it's the builder's responsibility for adding the time elapsed. That way we avoid cross cutting display concerns between compile and build for printing time elapsed
            if (success)
            {
                var preconditions = _preconditionManager.GetIncrementalPreconditions(projectNode);
                Reporter.Output.Write(" " + preconditions.LogMessage());
                Reporter.Output.WriteLine();
            }

            Reporter.Output.WriteLine();
        }

        private void CreateOutputDirectories()
        {
            if (!string.IsNullOrEmpty(_args.OutputValue))
            {
                Directory.CreateDirectory(_args.OutputValue);
            }
            if (!string.IsNullOrEmpty(_args.BuildBasePathValue))
            {
                Directory.CreateDirectory(_args.BuildBasePathValue);
            }
        }

        private void CopyCompilationOutput(OutputPaths outputPaths)
        {
            var dest = outputPaths.RuntimeOutputPath;
            var source = outputPaths.CompilationOutputPath;

            // No need to copy if dest and source are the same
            if (string.Equals(dest, source, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            foreach (var file in outputPaths.CompilationFiles.All())
            {
                var destFileName = file.Replace(source, dest);
                var directoryName = Path.GetDirectoryName(destFileName);
                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }
                File.Copy(file, destFileName, true);
            }
        }

        private void MakeRunnable(ProjectGraphNode graphNode)
        {
            var runtimeContext = graphNode.ProjectContext.ProjectFile.HasRuntimeOutput(_args.ConfigValue) ?
                graphNode.ProjectContext.CreateRuntimeContext(_args.GetRuntimes()) :
                graphNode.ProjectContext;

            var outputPaths = runtimeContext.GetOutputPaths(_args.ConfigValue, _args.BuildBasePathValue, _args.OutputValue);
            var libraryExporter = runtimeContext.CreateExporter(_args.ConfigValue, _args.BuildBasePathValue);

            CopyCompilationOutput(outputPaths);

            var executable = new Executable(runtimeContext, outputPaths, libraryExporter, _args.ConfigValue);
            executable.MakeCompilationOutputRunnable();
        }

        protected override CompilationResult RunCompile(ProjectGraphNode projectNode)
        {
            try
            {
                var managedCompiler = new ManagedCompiler(_scriptRunner, _commandFactory);

                var success = managedCompiler.Compile(projectNode.ProjectContext, _args);
                if (projectNode.IsRoot)
                {
                    MakeRunnable(projectNode);
                    PrintSummary(projectNode, success);
                }

                return success ? CompilationResult.Success : CompilationResult.Failure;
            }
            finally
            {
                StampProjectWithSDKVersion(projectNode.ProjectContext);
            }
        }

        protected override void ProjectSkiped(ProjectGraphNode projectNode)
        {
            StampProjectWithSDKVersion(projectNode.ProjectContext);
        }

        private bool CLIChangedSinceLastCompilation(ProjectContext project)
        {
            var currentVersionFile = DotnetFiles.VersionFile;
            var versionFileFromLastCompile = project.GetSDKVersionFile(_args.ConfigValue, _args.BuildBasePathValue, _args.OutputValue);

            if (!File.Exists(currentVersionFile))
            {
                // this CLI does not have a version file; cannot tell if CLI changed
                return false;
            }

            if (!File.Exists(versionFileFromLastCompile))
            {
                // this is the first compilation; cannot tell if CLI changed
                return false;
            }

            var currentContent = ComputeCurrentVersionFileData();

            var versionsAreEqual = string.Equals(currentContent, File.ReadAllText(versionFileFromLastCompile), StringComparison.OrdinalIgnoreCase);

            return !versionsAreEqual;
        }

        protected override bool NeedsRebuilding(ProjectGraphNode graphNode)
        {
            var project = graphNode.ProjectContext;
            if (_args.ShouldNotUseIncrementality)
            {
                return true;
            }
            if (!_args.ShouldSkipDependencies &&
                graphNode.Dependencies.Any(d => GetCompilationResult(d) != CompilationResult.IncrementalSkip))
            {
                Reporter.Output.WriteLine($"Project {project.GetDisplayName()} will be compiled because some of it's dependencies changed");
                return true;
            }
            var preconditions = _preconditionManager.GetIncrementalPreconditions(graphNode);
            if (preconditions.PreconditionsDetected())
            {
                return true;
            }

            if (CLIChangedSinceLastCompilation(project))
            {
                Reporter.Output.WriteLine($"Project {project.GetDisplayName()} will be compiled because the version or bitness of the CLI changed since the last build");
                return true;
            }

            var compilerIO = _compilerIOManager.GetCompileIO(graphNode);

            // rebuild if empty inputs / outputs
            if (!(compilerIO.Outputs.Any() && compilerIO.Inputs.Any()))
            {
                Reporter.Output.WriteLine($"Project {project.GetDisplayName()} will be compiled because it either has empty inputs or outputs");
                return true;
            }

            //rebuild if missing inputs / outputs
            if (_compilerIOManager.AnyMissingIO(project, compilerIO.Outputs, "outputs") || _compilerIOManager.AnyMissingIO(project, compilerIO.Inputs, "inputs"))
            {
                return true;
            }

            // find the output with the earliest write time
            var minOutputPath = compilerIO.Outputs.First();
            var minDateUtc = File.GetLastWriteTimeUtc(minOutputPath);

            foreach (var outputPath in compilerIO.Outputs)
            {
                if (File.GetLastWriteTimeUtc(outputPath) >= minDateUtc)
                {
                    continue;
                }

                minDateUtc = File.GetLastWriteTimeUtc(outputPath);
                minOutputPath = outputPath;
            }

            // find inputs that are older than the earliest output
            var newInputs = compilerIO.Inputs.FindAll(p => File.GetLastWriteTimeUtc(p) >= minDateUtc);

            if (!newInputs.Any())
            {
                Reporter.Output.WriteLine($"Project {project.GetDisplayName()} was previously compiled. Skipping compilation.");
                return false;
            }

            Reporter.Output.WriteLine($"Project {project.GetDisplayName()} will be compiled because some of its inputs were newer than its oldest output.");
            Reporter.Verbose.WriteLine();
            Reporter.Verbose.WriteLine($" Oldest output item:");
            Reporter.Verbose.WriteLine($"  {minDateUtc.ToLocalTime()}: {minOutputPath}");
            Reporter.Verbose.WriteLine();

            Reporter.Verbose.WriteLine($" Inputs newer than the oldest output item:");

            foreach (var newInput in newInputs)
            {
                Reporter.Verbose.WriteLine($"  {File.GetLastWriteTime(newInput)}: {newInput}");
            }

            Reporter.Verbose.WriteLine();

            return true;
        }
    }
}