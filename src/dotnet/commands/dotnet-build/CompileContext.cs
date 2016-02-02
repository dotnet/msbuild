// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dotnet.Cli.Compiler.Common;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Utilities;
using Microsoft.DotNet.Tools.Compiler;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.DotNet.Tools.Build
{
    // todo: Convert CompileContext into a DAG of dependencies: if a node needs recompilation, the entire path up to root needs compilation
    // Knows how to orchestrate compilation for a ProjectContext
    // Collects icnremental safety checks and transitively compiles a project context
    internal class CompileContext
    {
        public static readonly string[] KnownCompilers = { "csc", "vbc", "fsc" };

        private readonly ProjectContext _rootProject;
        private readonly BuilderCommandApp _args;
        private readonly IncrementalPreconditions _preconditions;
        private readonly ProjectDependenciesFacade _dependencies;

        public bool IsSafeForIncrementalCompilation => !_preconditions.PreconditionsDetected();

        public CompileContext(ProjectContext rootProject, BuilderCommandApp args)
        {
            _rootProject = rootProject;

            // Cleaner to clone the args and mutate the clone than have separate CompileContext fields for mutated args 
            // and then reasoning which ones to get from args and which ones from fields.
            _args = (BuilderCommandApp)args.ShallowCopy();

            // Set up Output Paths. They are unique per each CompileContext
            var outputPathCalculator = _rootProject.GetOutputPathCalculator(_args.OutputValue);
            _args.OutputValue = outputPathCalculator.BaseCompilationOutputPath;
            _args.IntermediateValue =
                outputPathCalculator.GetIntermediateOutputDirectoryPath(_args.ConfigValue, _args.IntermediateValue);

            // Set up dependencies
            _dependencies = new ProjectDependenciesFacade(_rootProject, _args.ConfigValue);

            // gather preconditions
            _preconditions = GatherIncrementalPreconditions();
        }

        public bool Compile(bool incremental)
        {
            CreateOutputDirectories();

            // compile dependencies
            foreach (var dependency in Sort(_dependencies.ProjectDependenciesWithSources))
            {
                if (incremental)
                {
                    var dependencyProjectContext = ProjectContext.Create(dependency.Path, dependency.Framework);

                    if (!DependencyNeedsRebuilding(dependencyProjectContext, new ProjectDependenciesFacade(dependencyProjectContext, _args.ConfigValue)))
                    {
                        continue;
                    }
                }

                if (!InvokeCompileOnDependency(dependency))
                {
                    return false;
                }
            }

            if (incremental && !NeedsRebuilding(_rootProject, _dependencies))
            {
                // todo: what if the previous build had errors / warnings and nothing changed? Need to propagate them in case of incremental
                return true;
            }

            // compile project
            var success = InvokeCompileOnRootProject();

            PrintSummary(success);

            return success;
        }

        private bool DependencyNeedsRebuilding(ProjectContext project, ProjectDependenciesFacade dependencies)
        {
            return NeedsRebuilding(project, dependencies, buildOutputPath: null, intermediateOutputPath: null);
        }

        private bool NeedsRebuilding(ProjectContext project, ProjectDependenciesFacade dependencies)
        {
            return NeedsRebuilding(project, dependencies, _args.OutputValue, _args.IntermediateValue);
        }

        private bool NeedsRebuilding(ProjectContext project, ProjectDependenciesFacade dependencies, string buildOutputPath, string intermediateOutputPath)
        {
            var compilerIO = GetCompileIO(project, _args.ConfigValue, buildOutputPath, intermediateOutputPath, dependencies);

            // rebuild if empty inputs / outputs
            if (!(compilerIO.Outputs.Any() && compilerIO.Inputs.Any()))
            {
                Reporter.Output.WriteLine($"Project {project.GetDisplayName()} will be compiled because it either has empty inputs or outputs");
                return true;
            }

            //rebuild if missing inputs / outputs
            if (AnyMissingIO(project, compilerIO.Outputs, "outputs") || AnyMissingIO(project, compilerIO.Inputs, "inputs"))
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
            var newInputs = compilerIO.Inputs.FindAll(p => File.GetLastWriteTimeUtc(p) > minDateUtc);

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

        private static bool AnyMissingIO(ProjectContext project, IEnumerable<string> items, string itemsType)
        {
            var missingItems = items.Where(i => !File.Exists(i)).ToList();

            if (!missingItems.Any())
            {
                return false;
            }

            Reporter.Verbose.WriteLine($"Project {project.GetDisplayName()} will be compiled because expected {itemsType} are missing.");

            foreach (var missing in missingItems)
            {
                Reporter.Verbose.WriteLine($" {missing}");
            }

            Reporter.Verbose.WriteLine(); ;

            return true;
        }

        private void PrintSummary(bool success)
        {
            // todo: Ideally it's the builder's responsibility for adding the time elapsed. That way we avoid cross cutting display concerns between compile and build for printing time elapsed
            if (success)
            {
                Reporter.Output.Write(" " + _preconditions.LogMessage());
                Reporter.Output.WriteLine();
            }

            Reporter.Output.WriteLine();
        }

        private void CreateOutputDirectories()
        {
            Directory.CreateDirectory(_args.OutputValue);
            Directory.CreateDirectory(_args.IntermediateValue);
        }

        private IncrementalPreconditions GatherIncrementalPreconditions()
        {
            var preconditions = new IncrementalPreconditions(_args.BuildProfileValue);

            if (_args.ForceUnsafeValue)
            {
                preconditions.AddForceUnsafePrecondition();
            }

            var projectsToCheck = GetProjectsToCheck();

            foreach (var project in projectsToCheck)
            {
                CollectScriptPreconditions(project, preconditions);
                CollectCompilerNamePreconditions(project, preconditions);
                CollectCheckPathProbingPreconditions(project, preconditions);
            }

            return preconditions;
        }

        // check the entire project tree that needs to be compiled, duplicated for each framework
        private List<ProjectContext> GetProjectsToCheck()
        {
            // include initial root project
            var contextsToCheck = new List<ProjectContext>(1 + _dependencies.ProjectDependenciesWithSources.Count) { _rootProject };

            // convert ProjectDescription to ProjectContext
            var dependencyContexts = _dependencies.ProjectDependenciesWithSources.Select
                (keyValuePair => ProjectContext.Create(keyValuePair.Value.Path, keyValuePair.Value.Framework));

            contextsToCheck.AddRange(dependencyContexts);


            return contextsToCheck;
        }

        private void CollectCheckPathProbingPreconditions(ProjectContext project, IncrementalPreconditions preconditions)
        {
            var pathCommands = CompilerUtil.GetCommandsInvokedByCompile(project)
                .Select(commandName => Command.CreateDotNet(commandName, Enumerable.Empty<string>(), project.TargetFramework))
                .Where(c => c.ResolutionStrategy.Equals(CommandResolutionStrategy.Path));

            foreach (var pathCommand in pathCommands)
            {
                preconditions.AddPathProbingPrecondition(project.ProjectName(), pathCommand.CommandName);
            }
        }

        private void CollectCompilerNamePreconditions(ProjectContext project, IncrementalPreconditions preconditions)
        {
            var projectCompiler = CompilerUtil.ResolveCompilerName(project);

            if (!KnownCompilers.Any(knownCompiler => knownCompiler.Equals(projectCompiler, StringComparison.Ordinal)))
            {
                preconditions.AddUnknownCompilerPrecondition(project.ProjectName(), projectCompiler);
            }
        }

        private void CollectScriptPreconditions(ProjectContext project, IncrementalPreconditions preconditions)
        {
            var preCompileScripts = project.ProjectFile.Scripts.GetOrEmpty(ScriptNames.PreCompile);
            var postCompileScripts = project.ProjectFile.Scripts.GetOrEmpty(ScriptNames.PostCompile);

            if (preCompileScripts.Any())
            {
                preconditions.AddPrePostScriptPrecondition(project.ProjectName(), ScriptNames.PreCompile);
            }

            if (postCompileScripts.Any())
            {
                preconditions.AddPrePostScriptPrecondition(project.ProjectName(), ScriptNames.PostCompile);
            }
        }

        private bool InvokeCompileOnDependency(ProjectDescription projectDependency)
        {
            var args = new List<string>();

            args.Add("--framework");
            args.Add($"{projectDependency.Framework}");                       
            
            args.Add("--configuration");
            args.Add(_args.ConfigValue);
            args.Add(projectDependency.Project.ProjectDirectory);
            
            var compileResult = Command.CreateDotNet("compile", args)
                .ForwardStdOut()
                .ForwardStdErr()
                .Execute();

            return compileResult.ExitCode == 0;
        }

        private bool InvokeCompileOnRootProject()
        {
            // todo: add methods to CompilerCommandApp to generate the arg string?
            var args = new List<string>();
            args.Add("--framework");
            args.Add(_rootProject.TargetFramework.ToString());            
            args.Add("--configuration");
            args.Add(_args.ConfigValue);
            args.Add("--output");
            args.Add(_args.OutputValue);
            args.Add("--temp-output");
            args.Add(_args.IntermediateValue);

            //native args
            if (_args.IsNativeValue)
            {
                args.Add("--native");
            }

            if (_args.IsCppModeValue)
            {
                args.Add("--cpp");
            }

            if (!string.IsNullOrWhiteSpace(_args.ArchValue))
            {
                args.Add("--arch");
                args.Add(_args.ArchValue);
            }

            if (!string.IsNullOrWhiteSpace(_args.IlcArgsValue))
            {
                args.Add("--ilcargs");
                args.Add(_args.IlcArgsValue);
            }

            if (!string.IsNullOrWhiteSpace(_args.IlcPathValue))
            {
                args.Add("--ilcpath");
                args.Add(_args.IlcPathValue);
            }

            if (!string.IsNullOrWhiteSpace(_args.IlcSdkPathValue))
            {
                args.Add("--ilcsdkpath");
                args.Add(_args.IlcSdkPathValue);
            }

            args.Add(_rootProject.ProjectDirectory);

            var compileResult = Command.CreateDotNet("compile", args)
                .ForwardStdOut()
                .ForwardStdErr()
                .Execute();

            var succeeded = compileResult.ExitCode == 0;

            if (succeeded)
            {
                MakeRunnableIfNecessary();
            }            
            
            return succeeded;
        }

        private void MakeRunnableIfNecessary()
        {
            var compilationOptions = CompilerUtil.ResolveCompilationOptions(_rootProject, _args.ConfigValue);

            // TODO: Make this opt in via another mechanism
            var makeRunnable = compilationOptions.EmitEntryPoint.GetValueOrDefault() ||
                               _rootProject.IsTestProject();

            if (makeRunnable)
            {
                var outputPathCalculator = _rootProject.GetOutputPathCalculator(_args.OutputValue);
                var rids = new List<string>();
                if (string.IsNullOrEmpty(_args.RuntimeValue))
                {
                    rids.AddRange(PlatformServices.Default.Runtime.GetAllCandidateRuntimeIdentifiers());
                }
                else
                {
                    rids.Add(_args.RuntimeValue);
                }

                var runtimeContext = ProjectContext.Create(_rootProject.ProjectDirectory, _rootProject.TargetFramework, rids);
                var executable = new Executable(runtimeContext, outputPathCalculator);
                executable.MakeCompilationOutputRunnable(_args.ConfigValue);
            }
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

        public struct CompilerIO
        {
            public readonly List<string> Inputs;
            public readonly List<string> Outputs;

            public CompilerIO(List<string> inputs, List<string> outputs)
            {
                Inputs = inputs;
                Outputs = outputs;
            }
        }

        // computes all the inputs and outputs that would be used in the compilation of a project
        // ensures that all paths are files
        // ensures no missing inputs
        public static CompilerIO GetCompileIO(
            ProjectContext project,
            string buildConfiguration,
            string outputPath,
            string intermediaryOutputPath,
            ProjectDependenciesFacade dependencies)
        {
            var compilerIO = new CompilerIO(new List<string>(), new List<string>());
            var calculator = project.GetOutputPathCalculator(outputPath);
            var binariesOutputPath = calculator.GetOutputDirectoryPath(buildConfiguration);

            // input: project.json
            compilerIO.Inputs.Add(project.ProjectFile.ProjectFilePath);

            // input: lock file; find when dependencies change
            AddLockFile(project, compilerIO);

            // input: source files
            compilerIO.Inputs.AddRange(CompilerUtil.GetCompilationSources(project));

            // todo: Factor out dependency resolution between Build and Compile. Ideally Build injects the dependencies into Compile
            // input: dependencies
            AddDependencies(dependencies, compilerIO);

            // output: compiler outputs
            foreach (var path in calculator.GetBuildOutputs(buildConfiguration))
            {
                compilerIO.Outputs.Add(path);
            }
            
            // input compilation options files
            AddCompilationOptions(project, buildConfiguration, compilerIO);

            // input / output: resources without culture
            AddCultureResources(project, intermediaryOutputPath, compilerIO);

            // input / output: resources with culture
            AddNonCultureResources(project, binariesOutputPath, compilerIO);

            return compilerIO;
        }

        private static void AddLockFile(ProjectContext project, CompilerIO compilerIO)
        {
            if (project.LockFile == null)
            {
                var errorMessage = $"Project {project.ProjectName()} does not have a lock file.";
                Reporter.Error.WriteLine(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            compilerIO.Inputs.Add(project.LockFile.LockFilePath);
        }

        private static void AddDependencies(ProjectDependenciesFacade dependencies, CompilerIO compilerIO)
        {
            // add dependency sources that need compilation
            compilerIO.Inputs.AddRange(dependencies.ProjectDependenciesWithSources.Values.SelectMany(p => p.Project.Files.SourceFiles));

            // non project dependencies get captured by changes in the lock file
        }

        private static void AddCompilationOptions(ProjectContext project, string config, CompilerIO compilerIO)
        {
            var compilerOptions = CompilerUtil.ResolveCompilationOptions(project, config);

            // input: key file
            if (compilerOptions.KeyFile != null)
            {
                compilerIO.Inputs.Add(compilerOptions.KeyFile);
            }
        }

        private static void AddNonCultureResources(ProjectContext project, string intermediaryOutputPath, CompilerIO compilerIO)
        {
            foreach (var resourceIO in CompilerUtil.GetNonCultureResources(project.ProjectFile, intermediaryOutputPath))
            {
                compilerIO.Inputs.Add(resourceIO.InputFile);

                if (resourceIO.OutputFile != null)
                {
                    compilerIO.Outputs.Add(resourceIO.OutputFile);
                }
            }
        }

        private static void AddCultureResources(ProjectContext project, string outputPath, CompilerIO compilerIO)
        {
            foreach (var cultureResourceIO in CompilerUtil.GetCultureResources(project.ProjectFile, outputPath))
            {
                compilerIO.Inputs.AddRange(cultureResourceIO.InputFileToMetadata.Keys);

                if (cultureResourceIO.OutputFile != null)
                {
                    compilerIO.Outputs.Add(cultureResourceIO.OutputFile);
                }
            }
        }
    }

}
