// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Pack
{
    public class ArtifactPathsCalculator
    {
        private readonly Project _project;

        private readonly string _configuration;

        public bool PackageOutputPathSet => !string.IsNullOrWhiteSpace(PackageOutputPathParameter);

        public string CompiledArtifactsPathParameter { get; }

        public string PackageOutputPathParameter { get; }

        public bool CompiledArtifactsPathSet => !string.IsNullOrWhiteSpace(CompiledArtifactsPathParameter);        

        public string CompiledArtifactsPath => 
            CompiledArtifactsPathSet ? CompiledArtifactsPathParameter : PackageOutputPath;

        public string PackageOutputPath
        {
            get
            {
                if (PackageOutputPathSet)
                {
                    return PackageOutputPathParameter;
                }

                var outputPath = Path.Combine(
                    _project.ProjectDirectory,
                    Constants.BinDirectoryName);

                return outputPath;
            }
        }        

        public ArtifactPathsCalculator(
            Project project, 
            string compiledArtifactsPath, 
            string packageOutputPath,
            string configuration)
        {
            _project = project;
            CompiledArtifactsPathParameter = compiledArtifactsPath;
            PackageOutputPathParameter = packageOutputPath;
            _configuration = configuration;
        }

        public string InputPathForContext(ProjectContext context)
        {
            return OutputPathsCalculator.GetOutputPaths(context.ProjectFile,
                context.TargetFramework,
                context.RuntimeIdentifier,
                _configuration,
                context.RootDirectory,
                CompiledArtifactsPathParameter,
                null).CompilationOutputPath;
        }
    }
}
