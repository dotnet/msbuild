// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Cli.Utils
{
    /// <summary>
    /// This class is to correct .default user's PATH caused by https://github.com/dotnet/sdk/issues/9762
    /// It will be run during elevated installer install.
    /// For example and the usage reference the test DotDefaultPathCorrectorTests
    /// </summary>
    public static class DotDefaultPathCorrector
    {
        private const string DotnetToolsSuffix = @"\.dotnet\tools";

        public static void Correct()
        {
            var pathEditor = new WindowsRegistryEnvironmentPathEditor();
            var dotDefaultPath =
                pathEditor.Get(
                    SdkEnvironmentVariableTarget.DotDefault);
            if (NeedCorrection(dotDefaultPath, out var correctedPath))
            {
                pathEditor.Set(correctedPath,
                    SdkEnvironmentVariableTarget.DotDefault);
            }
        }

        internal static bool NeedCorrection(string existingPath, out string correctedPath)
        {
            correctedPath = string.Empty;
            if (string.IsNullOrWhiteSpace(existingPath))
            {
                return false;
            }

            IEnumerable<string> paths = existingPath.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            var inCorrectToolsPaths =
                paths.Where(p => p.EndsWith(DotnetToolsSuffix, StringComparison.OrdinalIgnoreCase));

            if (!inCorrectToolsPaths.Any())
            {
                return false;
            }

            var correctedPaths = paths
                .Where(p => !p.EndsWith(DotnetToolsSuffix, StringComparison.OrdinalIgnoreCase))
                .Select(p => ReplaceExpandedUserProfile(p, inCorrectToolsPaths));

            correctedPath = string.Join(";", correctedPaths);

            return true;
        }

        private static string ReplaceExpandedUserProfile(string path, IEnumerable<string> inCorrectToolsPaths)
        {
            foreach (var inCorrectToolsPath in inCorrectToolsPaths)
            {
                var expandedUserProfile =
                    inCorrectToolsPath.Substring(0, inCorrectToolsPath.Length - DotnetToolsSuffix.Length);

                if (path.StartsWith(expandedUserProfile, StringComparison.OrdinalIgnoreCase))
                {
                    return path.Replace(expandedUserProfile, "%USERPROFILE%");
                }
            }

            return path;
        }
    }
}
