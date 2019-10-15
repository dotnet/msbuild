// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class RepoDirectoriesProvider
    {
        private static string s_repoRoot;

        private string _artifacts;
        private string _dotnetRoot;
        private string _nugetPackages;
        private string _stage2Sdk;
        private string _testPackages;
        private string _testWorkingFolder;
        private string _testArtifactsFolder;

        public static string RepoRoot
        {
            get
            {
                if (!string.IsNullOrEmpty(s_repoRoot))
                {
                    return s_repoRoot;
                }

                string directory = AppContext.BaseDirectory;

                while (directory != null)
                {
                    var gitDirOrFile = Path.Combine(directory, ".git");
                    if (Directory.Exists(gitDirOrFile) || File.Exists(gitDirOrFile))
                    {
                        break;
                    }
                    directory = Directory.GetParent(directory)?.FullName;
                }

                if (directory == null)
                {
                    throw new Exception("Cannot find the git repository root");
                }

                s_repoRoot = directory;
                return s_repoRoot;
            }
        }

        public string Artifacts => _artifacts;
        public string DotnetRoot => _dotnetRoot;
        public string NugetPackages => _nugetPackages;
        public string Stage2Sdk => _stage2Sdk;
        public string TestPackages => _testPackages;
        public string TestWorkingFolder => _testWorkingFolder;
        public string TestArtifactsFolder => _testArtifactsFolder;

        public RepoDirectoriesProvider()
        {
            var configuration =
                Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            _artifacts = Path.Combine(RepoRoot, "artifacts");
            _dotnetRoot = Path.Combine(_artifacts, "tmp", configuration, "dotnet");
            _nugetPackages = Path.Combine(RepoRoot, ".nuget", "packages");
            _stage2Sdk = Directory
                .EnumerateDirectories(Path.Combine(_dotnetRoot, "sdk"))
                .First(d => !d.Contains("NuGetFallbackFolder"));

            _testPackages = Path.Combine(_artifacts, "tmp", configuration, "test", "packages");

            _testArtifactsFolder = Path.Combine(_artifacts, "tmp", configuration, "test", "artifacts");

            _testWorkingFolder = Path.Combine(_artifacts, "tmp", configuration, "test");
            
        }
    }
}
