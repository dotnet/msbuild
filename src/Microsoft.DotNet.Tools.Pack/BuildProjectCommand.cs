// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Tools.Pack
{
    internal class BuildProjectCommand
    {
        private readonly Project _project;
        private readonly ArtifactPathsCalculator _artifactPathsCalculator;

        private readonly string _intermediateOutputPath;
        private readonly string _configuration;

        private bool SkipBuild => _artifactPathsCalculator.CompiledArtifactsPathSet;
        
        public BuildProjectCommand(
            Project project, 
            ArtifactPathsCalculator artifactPathsCalculator, 
            string intermediateOutputPath, 
            string configuration)
        {
            _project = project;
            _artifactPathsCalculator = artifactPathsCalculator;
            _intermediateOutputPath = intermediateOutputPath;
            _configuration = configuration;
        }

        public int Execute()
        {
            if (SkipBuild)
            {
                return 0;
            }

            if (_project.Files.SourceFiles.Any())
            {
                var argsBuilder = new StringBuilder();
                argsBuilder.Append($"--configuration {_configuration}");

                if (_artifactPathsCalculator.PackageOutputPathSet)
                {
                    argsBuilder.Append($" --output \"{_artifactPathsCalculator.PackageOutputPathParameter}\"");
                }

                if (!string.IsNullOrEmpty(_intermediateOutputPath))
                {
                    argsBuilder.Append($" --temp-output \"{_intermediateOutputPath}\"");
                }

                argsBuilder.Append($" \"{_project.ProjectFilePath}\"");

                var result = Command.Create("dotnet-build", argsBuilder.ToString())
                       .ForwardStdOut()
                       .ForwardStdErr()
                       .Execute();

                return result.ExitCode;
            }

            return 0;
        }
    }
}
