// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel
{
    public class OutputPathCalculator
    {
        private readonly ProjectContext _project;

        public string BaseRootOutputPath { get; }

        public OutputPathCalculator(
            ProjectContext project,
            string rootOutputPath)
        {
            _project = project;
            BaseRootOutputPath = string.IsNullOrWhiteSpace(rootOutputPath)
                ? Path.Combine(_project.ProjectDirectory, DirectoryNames.Bin)
                : rootOutputPath;
        }

        public string GetOutputDirectoryPath(string buildConfiguration)
        {
            var outDir = Path.Combine(
                BaseRootOutputPath,
                buildConfiguration,
                _project.TargetFramework.GetTwoDigitShortFolderName());

//            if (!string.IsNullOrEmpty(_project.RuntimeIdentifier))
//            {
//                outDir = Path.Combine(outDir, _project.RuntimeIdentifier);
//            }

            return outDir;
        }
    }
}
