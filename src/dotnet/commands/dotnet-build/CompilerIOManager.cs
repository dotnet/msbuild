// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Tools.Compiler;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Compiler.Common;

namespace Microsoft.DotNet.Tools.Build
{
    internal class CompilerIOManager
    {
        private readonly string _configuration;
        private readonly string _outputPath;
        private readonly string _buildBasePath;
        private readonly IList<string> _runtimes;

        public CompilerIOManager(string configuration,
            string outputPath,
            string buildBasePath,
            IEnumerable<string> runtimes)
        {
            _configuration = configuration;
            _outputPath = outputPath;
            _buildBasePath = buildBasePath;
            _runtimes = runtimes.ToList();
        }

        public bool AnyMissingIO(ProjectContext project, IEnumerable<string> items, string itemsType)
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

        // computes all the inputs and outputs that would be used in the compilation of a project
        // ensures that all paths are files
        // ensures no missing inputs
        public CompilerIO GetCompileIO(ProjectGraphNode graphNode)
        {
            var isRootProject = graphNode.IsRoot;
            var project = graphNode.ProjectContext;

            var compilerIO = new CompilerIO(new List<string>(), new List<string>());
            var calculator = project.GetOutputPaths(_configuration, _buildBasePath, _outputPath);
            var binariesOutputPath = calculator.CompilationOutputPath;

            // input: project.json
            compilerIO.Inputs.Add(project.ProjectFile.ProjectFilePath);

            // input: lock file; find when dependencies change
            AddLockFile(project, compilerIO);

            // input: source files
            compilerIO.Inputs.AddRange(CompilerUtil.GetCompilationSources(project));

            var allOutputPath = new HashSet<string>(calculator.CompilationFiles.All());
            if (isRootProject && project.ProjectFile.HasRuntimeOutput(_configuration))
            {
                var runtimeContext = project.CreateRuntimeContext(_runtimes);
                foreach (var path in runtimeContext.GetOutputPaths(_configuration, _buildBasePath, _outputPath).RuntimeFiles.All())
                {
                    allOutputPath.Add(path);
                }
            }

            // output: compiler outputs
            foreach (var path in allOutputPath)
            {
                compilerIO.Outputs.Add(path);
            }

            // input compilation options files
            AddCompilationOptions(project, _configuration, compilerIO);

            // input / output: resources with culture
            AddNonCultureResources(project, calculator.IntermediateOutputDirectoryPath, compilerIO);

            // input / output: resources without culture
            AddCultureResources(project, binariesOutputPath, compilerIO);

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

            if (project.LockFile.ExportFile != null)
            {
                compilerIO.Inputs.Add(project.LockFile.ExportFile.ExportFilePath);
            }
        }


        private static void AddCompilationOptions(ProjectContext project, string config, CompilerIO compilerIO)
        {
            var compilerOptions = project.ResolveCompilationOptions(config);

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