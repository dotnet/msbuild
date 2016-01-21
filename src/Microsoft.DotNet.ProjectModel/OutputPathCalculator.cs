// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel
{
    public class OutputPathCalculator
    {
        private const string ObjDirectoryName = "obj";

        private readonly ProjectContext _project;

        /// <summary>
        /// Unaltered output path. Either what is passed in in the constructor, or the project directory.
        /// </summary>
        private string BaseOutputPath { get; }

        public string BaseCompilationOutputPath { get; }

        public OutputPathCalculator(
            ProjectContext project,
            string baseOutputPath)
        {
            _project = project;

            BaseOutputPath = string.IsNullOrWhiteSpace(baseOutputPath) ? _project.ProjectDirectory : baseOutputPath;

            BaseCompilationOutputPath = string.IsNullOrWhiteSpace(baseOutputPath)
                ? Path.Combine(_project.ProjectDirectory, DirectoryNames.Bin)
                : baseOutputPath;
        }

        public string GetCompilationOutputPath(string buildConfiguration)
        {
            var outDir = Path.Combine(
                BaseCompilationOutputPath,
                buildConfiguration,
                _project.TargetFramework.GetTwoDigitShortFolderName());

            return outDir;
        }

        public string GetIntermediateOutputPath(string buildConfiguration, string intermediateOutputValue)
        {
            string intermediateOutputPath;

            if (string.IsNullOrEmpty(intermediateOutputValue))
            {
                intermediateOutputPath = Path.Combine(
                    BaseOutputPath,
                    ObjDirectoryName,
                    buildConfiguration,
                    _project.TargetFramework.GetTwoDigitShortFolderName());
            }
            else
            {
                intermediateOutputPath = intermediateOutputValue;
            }

            return intermediateOutputPath;
        }
    }
}
