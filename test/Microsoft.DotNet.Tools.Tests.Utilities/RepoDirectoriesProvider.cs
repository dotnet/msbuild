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
        private static string s_buildRid;

        private string _artifacts;
        private string _builtDotnet;
        private string _nugetPackages;
        private string _stage2Sdk;
        private string _stage2WithBackwardsCompatibleRuntimesDirectory;
        private string _testPackages;
        private string _testWorkingFolder;

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

        public static string BuildRid
        {
            get
            {
                if (string.IsNullOrEmpty(s_buildRid))
                {
                    var buildInfoPath = Path.Combine(RepoRoot, "bin", "obj", "BuildInfo.props");
                    var root = XDocument.Load(buildInfoPath).Root;
                    var ns = root.Name.Namespace;

                    s_buildRid = root
                        .Elements(ns + "PropertyGroup")
                        .Elements(ns + "Rid")
                        .FirstOrDefault()
                        ?.Value;

                    if (string.IsNullOrEmpty(s_buildRid))
                    {
                        throw new InvalidOperationException($"Could not find a property named 'Rid' in {buildInfoPath}");
                    }
                }
                
                return s_buildRid;
            }
        }

        public string Artifacts => _artifacts;
        public string BuiltDotnet => _builtDotnet;
        public string NugetPackages => _nugetPackages;
        public string Stage2Sdk => _stage2Sdk;
        public string Stage2WithBackwardsCompatibleRuntimesDirectory => _stage2WithBackwardsCompatibleRuntimesDirectory;
        public string TestPackages => _testPackages;
        public string TestWorkingFolder => _testWorkingFolder;

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
                                                   previousStage.ToString(),
                                                   BuildRid);
            _builtDotnet = builtDotnet ?? Path.Combine(_artifacts, "intermediate", "sharedFrameworkPublish");
            _nugetPackages = nugetPackages ?? Path.Combine(RepoRoot, ".nuget", "packages");
            _stage2Sdk = Directory
                .EnumerateDirectories(Path.Combine(_artifacts, "dotnet", "sdk"))
                .First(d => !d.Contains("NuGetFallbackFolder"));

            _stage2WithBackwardsCompatibleRuntimesDirectory =
                Path.Combine(_artifacts, "dotnetWithBackwardsCompatibleRuntimes");

            _testPackages = Environment.GetEnvironmentVariable("TEST_PACKAGES");
            if (string.IsNullOrEmpty(_testPackages))
            {
                throw new InvalidOperationException("TEST_PACKAGES environment variable not set");
            }

            _testWorkingFolder = Path.Combine(RepoRoot,
                                              "bin",
                                              (previousStage + 1).ToString(),
                                              BuildRid,
                                              "test");
            
        }
    }
}
