// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Compiler;
using Microsoft.DotNet.ProjectModel.Utilities;
using Microsoft.DotNet.Cli.Compiler.Common;

namespace Microsoft.DotNet.Tools.Build
{
    // Knows how to orchestrate compilation for a ProjectContext
    // Collects icnremental safety checks and transitively compiles a project context
    internal class CompileContext
    {

        public static readonly string[] KnownCompilers = { "csc", "vbc", "fsc" };

        private readonly ProjectContext _rootProject;
        private readonly BuilderCommandApp _args;
        private readonly Dictionary<string, ProjectDescription> _dependencies;
        private readonly string _outputPath;
        private readonly string _intermediateOutputPath;
        private readonly IncrementalPreconditions _preconditions;

        public bool IsSafeForIncrementalCompilation => _preconditions.PreconditionsDetected();

        public CompileContext(ProjectContext rootProject, BuilderCommandApp args)
        {
            _rootProject = rootProject;
            _args = args;

            // Set up Output Paths. They are unique per each CompileContext
            // Todo: clone args and mutate the clone so the rest of this class does not have special treatment for output paths
            _outputPath = _rootProject.GetOutputPath(_args.ConfigValue, _args.OutputValue);
            _intermediateOutputPath = _rootProject.GetIntermediateOutputPath(_args.ConfigValue, _args.IntermediateValue, _args.OutputValue);

            // Set up dependencies
            _dependencies = GetProjectDependenciesWithSources(_rootProject, _args.ConfigValue);

            //gather preconditions
            _preconditions = GatherIncrementalPreconditions();
        }

        public bool Compile(bool incremental)
        {
            CreateOutputDirectories();

            //compile dependencies
            foreach (var dependency in Sort(_dependencies))
            {
                if (!InvokeCompileOnDependency(dependency, _outputPath, _intermediateOutputPath))
                {
                    return false;
                }
            }

            //compile project
            var success = InvokeCompileOnRootProject(_outputPath, _intermediateOutputPath);

            PrintSummary(success);

            return success;
        }

        private void PrintSummary(bool success)
        {
            //todo: Ideally it's the builder's responsibility for adding the time elapsed. That way we avoid cross cutting display concerns between compile and build for printing time elapsed
            if (success)
            {
                Reporter.Output.Write(" " + _preconditions.LogMessage());
                Reporter.Output.WriteLine();
            }

            Reporter.Output.WriteLine();
        }

        private void CreateOutputDirectories()
        {
            Directory.CreateDirectory(_outputPath);
            Directory.CreateDirectory(_intermediateOutputPath);
        }

        //todo make extension of ProjectContext?
        //returns map with dependencies: string projectName -> ProjectDescription
        private static Dictionary<string, ProjectDescription> GetProjectDependenciesWithSources(ProjectContext projectContext, string configuration)
        {
            var projects = new Dictionary<string, ProjectDescription>();

            // Create the library exporter
            var exporter = projectContext.CreateExporter(configuration);

            // Gather exports for the project
            var dependencies = exporter.GetDependencies().ToList();

            // Build project references
            foreach (var dependency in dependencies)
            {
                var projectDependency = dependency.Library as ProjectDescription;

                if (projectDependency != null && projectDependency.Project.Files.SourceFiles.Any())
                {
                    projects[projectDependency.Identity.Name] = projectDependency;
                }
            }

            return projects;
        }

        private IncrementalPreconditions GatherIncrementalPreconditions()
        {
            var preconditions = new IncrementalPreconditions(_args.BuildProfileValue);

            var projectsToCheck = GetProjectsToCheck();

            foreach (var project in projectsToCheck)
            {
                CollectScriptPreconditions(project, preconditions);
                CollectCompilerNamePreconditions(project, preconditions);
                CheckPathProbing(project, preconditions);
            }

            return preconditions;
        }

        //check the entire project tree that needs to be compiled, duplicated for each framework
        private List<ProjectContext> GetProjectsToCheck()
        {
            //include initial root project
            var contextsToCheck = new List<ProjectContext>(1 + _dependencies.Count) {_rootProject};

            //convert ProjectDescription to ProjectContext
            var dependencyContexts = _dependencies.Select
                (keyValuePair => ProjectContext.Create(keyValuePair.Value.Path, keyValuePair.Value.TargetFrameworkInfo.FrameworkName));

            contextsToCheck.AddRange(dependencyContexts);


            return contextsToCheck;
        }

        private void CheckPathProbing(ProjectContext project, IncrementalPreconditions preconditions)
        {
            var pathCommands = CompilerUtil.GetCommandsInvokedByCompile(project)
                .Select(commandName => Command.Create(commandName, "", project.TargetFramework))
                .Where(c => Command.CommandResolutionStrategy.Path.Equals(c.ResolutionStrategy));

            foreach (var pathCommand in pathCommands)
            {
                preconditions.AddPathProbingPrecondition(project.ProjectName(), pathCommand.CommandName);
            }
        }

        private void CollectCompilerNamePreconditions(ProjectContext project, IncrementalPreconditions preconditions)
        {
            var projectCompiler = CompilerUtil.ResolveCompilerName(project);

            if (KnownCompilers.Any(knownCompiler => knownCompiler.Equals(projectCompiler, StringComparison.Ordinal)))
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

        private bool InvokeCompileOnDependency(ProjectDescription projectDependency, string outputPath, string intermediateOutputPath)
        {
            var compileResult = Command.Create("dotnet-compile",
                $"--framework {projectDependency.Framework} " +
                $"--configuration {_args.ConfigValue} " +
                $"--output \"{outputPath}\" " +
                $"--temp-output \"{intermediateOutputPath}\" " +
                (_args.NoHostValue ? "--no-host " : string.Empty) +
                $"\"{projectDependency.Project.ProjectDirectory}\"")
                .ForwardStdOut()
                .ForwardStdErr()
                .Execute();

            return compileResult.ExitCode == 0;
        }

        private bool InvokeCompileOnRootProject(string outputPath, string intermediateOutputPath)
        {
            //todo: add methods to CompilerCommandApp to generate the arg string
            var compileResult = Command.Create("dotnet-compile",
                $"--framework {_rootProject.TargetFramework} " +
                $"--configuration {_args.ConfigValue} " +
                $"--output \"{outputPath}\" " +
                $"--temp-output \"{intermediateOutputPath}\" " +
                (_args.NoHostValue ? "--no-host " : string.Empty) +
                //nativeArgs
                (_args.IsNativeValue ? "--native " : string.Empty) +
                (_args.IsCppModeValue ? "--cpp " : string.Empty) +
                (!string.IsNullOrWhiteSpace(_args.ArchValue) ? $"--arch {_args.ArchValue} " : string.Empty) +
                (!string.IsNullOrWhiteSpace(_args.IlcArgsValue) ? $"--ilcargs \"{_args.IlcArgsValue}\" " : string.Empty) +
                (!string.IsNullOrWhiteSpace(_args.IlcPathValue) ? $"--ilcpath \"{_args.IlcPathValue}\" " : string.Empty) +
                (!string.IsNullOrWhiteSpace(_args.IlcSdkPathValue) ? $"--ilcsdkpath \"{_args.IlcSdkPathValue}\" " : string.Empty) +
                $"\"{_rootProject.ProjectDirectory}\"")
                .ForwardStdOut()
                .ForwardStdErr()
                .Execute();

            return compileResult.ExitCode == 0;
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
    }

}
