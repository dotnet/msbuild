// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.ProjectModel.Utilities;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel
{
    public class OutputPathsCalculator
    {
        private const string ObjDirectoryName = "obj";
        private const string BinDirectoryName = "bin";

        public static OutputPaths GetOutputPaths(
            Project project,
            NuGetFramework framework,
            string runtimeIdentifier,
            string configuration,
            string solutionRootPath,
            string buildBasePath,
            string outputPath)
        {
            string resolvedBuildBasePath;
            if (string.IsNullOrEmpty(buildBasePath))
            {
                resolvedBuildBasePath = project.ProjectDirectory;
            }
            else
            {
                if (string.IsNullOrEmpty(solutionRootPath))
                {
                    resolvedBuildBasePath = Path.Combine(buildBasePath, project.Name);
                }
                else
                {
                    resolvedBuildBasePath = project.ProjectDirectory.Replace(solutionRootPath, buildBasePath);
                }
            }

            var compilationOutputPath = PathUtility.EnsureTrailingSlash(Path.Combine(resolvedBuildBasePath,
                BinDirectoryName,
                configuration,
                framework.GetShortFolderName()));

            string runtimeOutputPath = null;
            if (string.IsNullOrEmpty(outputPath))
            {
                if (!string.IsNullOrEmpty(runtimeIdentifier))
                {
                    runtimeOutputPath = PathUtility.EnsureTrailingSlash(Path.Combine(compilationOutputPath, runtimeIdentifier));
                }
                else
                {
                    // "Runtime" assets (i.e. the deps file) will be dropped to the compilation output path, because
                    // we are building a RID-less target.
                    runtimeOutputPath = compilationOutputPath;
                }
            }
            else
            {
                runtimeOutputPath = PathUtility.EnsureTrailingSlash(Path.GetFullPath(outputPath));
            }

            var intermediateOutputPath = PathUtility.EnsureTrailingSlash(Path.Combine(
                resolvedBuildBasePath,
                ObjDirectoryName,
                configuration,
                framework.GetTwoDigitShortFolderName()));

            var compilationFiles = new CompilationOutputFiles(compilationOutputPath, project, configuration, framework);

            RuntimeOutputFiles runtimeFiles = new RuntimeOutputFiles(runtimeOutputPath, project, configuration, framework, runtimeIdentifier);
            return new OutputPaths(intermediateOutputPath, compilationOutputPath, runtimeOutputPath, compilationFiles, runtimeFiles);
        }
    }
}