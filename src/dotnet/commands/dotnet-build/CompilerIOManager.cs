// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Compiler;

namespace Microsoft.DotNet.Tools.Build
{
    internal class CompilerIOManager
    {
        private readonly string _configuration;
        private readonly string _outputPath;
        private readonly string _buildBasePath;
        private readonly IList<string> _runtimes;
        private readonly BuildWorkspace _workspace;
		private readonly ConcurrentDictionary<ProjectContextIdentity, CompilerIO> _cache;

        public CompilerIOManager(string configuration,
            string outputPath,
            string buildBasePath,
            IEnumerable<string> runtimes,
            BuildWorkspace workspace)
        {
            _configuration = configuration;
            _outputPath = outputPath;
            _buildBasePath = buildBasePath;
            _runtimes = runtimes.ToList();
            _workspace = workspace;

            _cache = new ConcurrentDictionary<ProjectContextIdentity, CompilerIO>();
        }


        // computes all the inputs and outputs that would be used in the compilation of a project
        public CompilerIO GetCompileIO(ProjectGraphNode graphNode)
        {
            return _cache.GetOrAdd(graphNode.ProjectContext.Identity, i => ComputeIO(graphNode));
        }

        private CompilerIO ComputeIO(ProjectGraphNode graphNode)
        {
            var inputs = new List<string>();
            var outputs = new List<string>();

            var isRootProject = graphNode.IsRoot;
            var project = graphNode.ProjectContext;

            var calculator = project.GetOutputPaths(_configuration, _buildBasePath, _outputPath);
            var binariesOutputPath = calculator.CompilationOutputPath;
            var compilerOptions = project.ProjectFile.GetCompilerOptions(project.TargetFramework, _configuration);

            // input: project.json
            inputs.Add(project.ProjectFile.ProjectFilePath);

            // input: lock file; find when dependencies change
            AddLockFile(project, inputs);

            // input: source files
            inputs.AddRange(CompilerUtil.GetCompilationSources(project, compilerOptions));

            var allOutputPath = new HashSet<string>(calculator.CompilationFiles.All());
            if (isRootProject && project.ProjectFile.HasRuntimeOutput(_configuration))
            {
                var runtimeContext = _workspace.GetRuntimeContext(project, _runtimes);
                foreach (var path in runtimeContext.GetOutputPaths(_configuration, _buildBasePath, _outputPath).RuntimeFiles.All())
                {
                    allOutputPath.Add(path);
                }
            }
            foreach (var dependency in graphNode.Dependencies)
            {
                var outputFiles = dependency.ProjectContext
                    .GetOutputPaths(_configuration, _buildBasePath, _outputPath)
                    .CompilationFiles;

                inputs.Add(outputFiles.Assembly);
            }

            // output: compiler outputs
            foreach (var path in allOutputPath)
            {
                outputs.Add(path);
            }

            // input compilation options files
            AddCompilationOptions(project, _configuration, inputs);

            // input / output: resources with culture
            AddNonCultureResources(project, calculator.IntermediateOutputDirectoryPath, inputs, outputs, compilerOptions);

            // input / output: resources without culture
            AddCultureResources(project, binariesOutputPath, inputs, outputs, compilerOptions);

            return new CompilerIO(inputs, outputs);
        }

        private static void AddLockFile(ProjectContext project, List<string> inputs)
        {
            if (project.LockFile == null)
            {
                var errorMessage = $"Project {project.ProjectName()} does not have a lock file. Please run \"dotnet restore\" to generate a new lock file.";
                Reporter.Error.WriteLine(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            inputs.Add(project.LockFile.Path);
        }


        private static void AddCompilationOptions(ProjectContext project, string config, List<string> inputs)
        {
            var compilerOptions = project.ResolveCompilationOptions(config);

            // input: key file
            if (compilerOptions.KeyFile != null)
            {
                inputs.Add(compilerOptions.KeyFile);
            }
        }

        private static void AddNonCultureResources(
            ProjectContext project,
            string intermediaryOutputPath,
            List<string> inputs,
            IList<string> outputs,
            CommonCompilerOptions compilationOptions)
        {
            List<CompilerUtil.NonCultureResgenIO> resources = null;
            if (compilationOptions.EmbedInclude == null)
            {
                resources = CompilerUtil.GetNonCultureResources(project.ProjectFile, intermediaryOutputPath);
            }
            else
            {
                resources = CompilerUtil.GetNonCultureResourcesFromIncludeEntries(project.ProjectFile, intermediaryOutputPath, compilationOptions);
            }

            foreach (var resourceIO in resources)
            {
                inputs.Add(resourceIO.InputFile);

                if (resourceIO.OutputFile != null)
                {
                    outputs.Add(resourceIO.OutputFile);
                }
            }
        }

        private static void AddCultureResources(
            ProjectContext project,
            string outputPath,
             List<string> inputs,
             List<string> outputs,
            CommonCompilerOptions compilationOptions)
        {
            List<CompilerUtil.CultureResgenIO> resources = null;
            if (compilationOptions.EmbedInclude == null)
            {
                resources = CompilerUtil.GetCultureResources(project.ProjectFile, outputPath);
            }
            else
            {
                resources = CompilerUtil.GetCultureResourcesFromIncludeEntries(project.ProjectFile, outputPath, compilationOptions);
            }

            foreach (var cultureResourceIO in resources)
            {
                inputs.AddRange(cultureResourceIO.InputFileToMetadata.Keys);

                if (cultureResourceIO.OutputFile != null)
                {
                    outputs.Add(cultureResourceIO.OutputFile);
                }
            }
        }
    }
}