// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System.IO;
using Microsoft.DotNet.ProjectModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Utils
{
    internal static class ProjectContextExtensions
    {
        public static string ProjectName(this ProjectContext context) => context.RootProject.Identity.Name;

        public static string GetOutputPath(this ProjectContext context, string configuration, string currentOutputPath)
        {
            var outputPath = string.Empty;

            if (string.IsNullOrEmpty(currentOutputPath))
            {
                outputPath = Path.Combine(
                    GetDefaultRootOutputPath(context, currentOutputPath),
                    Constants.BinDirectoryName,
                    configuration,
                    context.TargetFramework.GetTwoDigitShortFolderName());
            }
            else
            {
                outputPath = currentOutputPath;
            }

            return outputPath;
        }

        public static string GetIntermediateOutputPath(this ProjectContext context, string configuration, string intermediateOutputValue, string currentOutputPath)
        {
            var intermediateOutputPath = string.Empty;

            if (string.IsNullOrEmpty(intermediateOutputValue))
            {
                intermediateOutputPath = Path.Combine(
                    GetDefaultRootOutputPath(context, currentOutputPath),
                    Constants.ObjDirectoryName,
                    configuration,
                    context.TargetFramework.GetTwoDigitShortFolderName());
            }
            else
            {
                intermediateOutputPath = intermediateOutputValue;
            }

            return intermediateOutputPath;
        }

        public static string GetDefaultRootOutputPath(ProjectContext context, string currentOutputPath)
        {
            string rootOutputPath = string.Empty;

            if (string.IsNullOrEmpty(currentOutputPath))
            {
                rootOutputPath = context.ProjectFile.ProjectDirectory;
            }

            return rootOutputPath;
        }
    }
}
