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

        public bool PackageArtifactsPathSet => !string.IsNullOrWhiteSpace(PackageArtifactsPathParameter);

        public string CompiledArtifactsPathParameter { get; }

        public string PackageArtifactsPathParameter { get; }

        public bool CompiledArtifactsPathSet => !string.IsNullOrWhiteSpace(CompiledArtifactsPathParameter);        

        public string CompiledArtifactsPath => 
            CompiledArtifactsPathSet ? CompiledArtifactsPathParameter : PackageArtifactsPath;

        public string PackageArtifactsPath
        {
            get
            {
                if (PackageArtifactsPathSet)
                {
                    return PackageArtifactsPathParameter;
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
            string packageArtifactsPath, 
            string configuration)
        {
            _project = project;
            CompiledArtifactsPathParameter = compiledArtifactsPath;
            PackageArtifactsPathParameter = packageArtifactsPath;
            _configuration = configuration;
        }

        public string InputPathForContext(ProjectContext context)
        {
            return Path.Combine(
                CompiledArtifactsPath,
                _configuration,
                context.TargetFramework.GetTwoDigitShortFolderName());
        }        
    }
}
