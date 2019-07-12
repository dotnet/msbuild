// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;

namespace Microsoft.Build.Shared.Debugging
{
    internal class RepositoryInfo
    {
        public static RepositoryInfo Instance = new RepositoryInfo();

        public string ArtifactsLogDirectory => _artifactsLogs.Value;

        public string Configuration { get; } =
#if DEBUG
            "Debug";
#else
            "Release";
#endif

        private RepositoryInfo()
        {
            _artifactsLogs = new Lazy<string>(ComputeArtifactsLogs);
        }

        private readonly Lazy<string> _artifactsLogs;

        private string ComputeArtifactsLogs()
        {
            var searchPathStrategies = new Func<string>[]
            {
                TryFromCurrentAssembly,
                TryFromAzureCI
            };


            return searchPathStrategies.Select(searchPathStrategy => searchPathStrategy.Invoke()).FirstOrDefault(path => path != null);
        }

        private string TryFromCurrentAssembly()
        {
            var executingAssembly = FileUtilities.ExecutingAssemblyPath;

            var binPart = $"bin";

            var logIndex = executingAssembly.IndexOf(binPart, StringComparison.Ordinal);

            if (logIndex < 0)
            {
                return null;
            }

            var artifactsPart = executingAssembly.Substring(0, logIndex);

            var path = Path.Combine(
                artifactsPart,
                "log",
                Configuration
                );

            ErrorUtilities.VerifyThrowDirectoryExists(path);

            return path;
        }

        private string TryFromAzureCI()
        {
            var repositoryPathInAzure = Environment.GetEnvironmentVariable("Build_Repository_LocalPath");

            if (repositoryPathInAzure == null)
            {
                return null;
            }

            var path = Path.Combine(repositoryPathInAzure, "artifacts", "logs", ArtifactsLogDirectory);

            ErrorUtilities.VerifyThrowDirectoryExists(path);

            return path;
        }
    }
}
