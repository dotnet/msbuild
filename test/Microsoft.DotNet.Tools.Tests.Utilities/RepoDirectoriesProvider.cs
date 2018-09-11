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
        private string _builtDotnet;
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

#if NET451
                string directory = AppDomain.CurrentDomain.BaseDirectory;
#else
                string directory = AppContext.BaseDirectory;
#endif

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
        public string BuiltDotnet => _builtDotnet;
        public string DotnetRoot => _dotnetRoot;
        public string NugetPackages => _nugetPackages;
        public string Stage2Sdk => _stage2Sdk;
        public string TestPackages => _testPackages;
        public string TestWorkingFolder => _testWorkingFolder;
        public string TestArtifactsFolder => _testArtifactsFolder;

        public RepoDirectoriesProvider(
            string artifacts = null,
            string builtDotnet = null,
            string nugetPackages = null,
            string corehostPackages = null,
            string corehostDummyPackages = null)
        {
            //  Ideally this wouldn't be hardcoded, so that you could use stage n to build stage n + 1, and then use stage n + 1 to run tests
            int previousStage = 2;

            _artifacts = artifacts ?? Path.Combine(RepoRoot,
                                                   "bin",
                                                   previousStage.ToString());
            _builtDotnet = builtDotnet ?? Path.Combine(_artifacts, "intermediate", "sharedFrameworkPublish");
            _dotnetRoot = Path.Combine(_artifacts, "dotnet");
            _nugetPackages = nugetPackages ?? Path.Combine(RepoRoot, ".nuget", "packages");
            _stage2Sdk = Directory
                .EnumerateDirectories(Path.Combine(_artifacts, "dotnet", "sdk"))
                .First(d => !d.Contains("NuGetFallbackFolder"));

            _testPackages = Environment.GetEnvironmentVariable("TEST_PACKAGES");
            if (string.IsNullOrEmpty(_testPackages))
            {
                throw new InvalidOperationException("TEST_PACKAGES environment variable not set");
            }

            _testArtifactsFolder = Path.Combine(_artifacts, "test", "artifacts");

            _testWorkingFolder = Path.Combine(RepoRoot,
                                              "bin",
                                              (previousStage + 1).ToString(),
                                              "test");
            
        }
    }
}
