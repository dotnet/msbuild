// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Compiler;

namespace Microsoft.DotNet.Tools.Build
{
    internal class DotNetProjectBuilder : ProjectBuilder
    {
        private readonly BuildCommandApp _args;
        private readonly IncrementalPreconditionManager _preconditionManager;
        private readonly CompilerIOManager _compilerIOManager;
        private readonly ScriptRunner _scriptRunner;
        private readonly DotNetCommandFactory _commandFactory;
        private readonly IncrementalManager _incrementalManager;

        public DotNetProjectBuilder(BuildCommandApp args) : base(args.ShouldSkipDependencies)
        {
            _args = args;

            _preconditionManager = new IncrementalPreconditionManager(
                args.ShouldPrintIncrementalPreconditions,
                args.ShouldNotUseIncrementality,
                args.ShouldSkipDependencies);

            _compilerIOManager = new CompilerIOManager(
                args.ConfigValue,
                args.OutputValue,
                args.BuildBasePathValue,
                args.GetRuntimes(),
                args.Workspace
                );

            _incrementalManager = new IncrementalManager(
                this,
                _compilerIOManager,
                _preconditionManager,
                _args.ShouldSkipDependencies,
                _args.ConfigValue,
                _args.BuildBasePathValue,
                _args.OutputValue
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

                string content = DotnetFiles.ReadAndInterpretVersionFile();

                File.WriteAllText(projectVersionFile, content);
            }
            else
            {
                Reporter.Verbose.WriteLine($"Project {project.GetDisplayName()} was not stamped with a CLI version because the version file does not exist: {DotnetFiles.VersionFile}");
            }
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
            try
            {
                var runtimeContext = graphNode.ProjectContext.ProjectFile.HasRuntimeOutput(_args.ConfigValue) ?
                    _args.Workspace.GetRuntimeContext(graphNode.ProjectContext, _args.GetRuntimes()) :
                    graphNode.ProjectContext;

                var outputPaths = runtimeContext.GetOutputPaths(_args.ConfigValue, _args.BuildBasePathValue, _args.OutputValue);
                var libraryExporter = runtimeContext.CreateExporter(_args.ConfigValue, _args.BuildBasePathValue);

                CopyCompilationOutput(outputPaths);

                var executable = new Executable(runtimeContext, outputPaths, libraryExporter, _args.ConfigValue);
                executable.MakeCompilationOutputRunnable();
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to make the following project runnable: {graphNode.ProjectContext.GetDisplayName()} reason: {e.Message}", e);
            }
        }

        protected override CompilationResult RunCompile(ProjectGraphNode projectNode)
        {
            try
            {
                var managedCompiler = new ManagedCompiler(_scriptRunner, _commandFactory);

                var success = managedCompiler.Compile(projectNode.ProjectContext, _args);
                if (projectNode.IsRoot)
                {
                    if (success)
                    {
                        MakeRunnable(projectNode);
                    }
                    PrintSummary(projectNode, success);
                }

                return success ? CompilationResult.Success : CompilationResult.Failure;
            }
            finally
            {
                StampProjectWithSDKVersion(projectNode.ProjectContext);
                _incrementalManager.CacheIncrementalState(projectNode);
            }
        }

        protected override void ProjectSkiped(ProjectGraphNode projectNode)
        {
            StampProjectWithSDKVersion(projectNode.ProjectContext);
            _incrementalManager.CacheIncrementalState(projectNode);
        }

        protected override bool NeedsRebuilding(ProjectGraphNode graphNode)
        {
            var result = _incrementalManager.NeedsRebuilding(graphNode);

            PrintIncrementalResult(graphNode.ProjectContext.GetDisplayName(), result);

            return result.NeedsRebuilding;
        }

        private void PrintIncrementalResult(string projectName, IncrementalResult result)
        {
            if (result.NeedsRebuilding)
            {
                Reporter.Output.WriteLine($"Project {projectName} will be compiled because {result.Reason}");
                PrintIncrementalItems(result);
            }
            else
            {
                Reporter.Output.WriteLine($"Project {projectName} was previously compiled. Skipping compilation.");
            }
        }

        private static void PrintIncrementalItems(IncrementalResult result)
        {
            if (Reporter.IsVerbose)
            {
                foreach (var item in result.Items)
                {
                    Reporter.Verbose.WriteLine($"\t{item}");
                }
            }
        }
    }
}