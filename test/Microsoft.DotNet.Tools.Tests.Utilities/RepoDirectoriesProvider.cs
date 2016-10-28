using System;
using System.IO;
using System.Linq;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class RepoDirectoriesProvider
    {
        private static string s_repoRoot;

        private string _artifacts;
        private string _builtDotnet;
        private string _nugetPackages;
        private string _stage2Sdk;
        private string _testPackages;
        private string _pjDotnet;

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

                while (!Directory.Exists(Path.Combine(directory, ".git")) && directory != null)
                {
                    directory = Directory.GetParent(directory).FullName;
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
        public string NugetPackages => _nugetPackages;
        public string PjDotnet => _pjDotnet;
        public string Stage2Sdk => _stage2Sdk;
        public string TestPackages => _testPackages;

        public RepoDirectoriesProvider(
            string artifacts = null,
            string builtDotnet = null,
            string nugetPackages = null,
            string corehostPackages = null,
            string corehostDummyPackages = null,
            string pjDotnet = null)
        {
            var currentRid = RuntimeEnvironment.GetRuntimeIdentifier();

            _artifacts = artifacts ?? Path.Combine(RepoRoot, "artifacts", currentRid);
            _builtDotnet = builtDotnet ?? Path.Combine(_artifacts, "intermediate", "sharedFrameworkPublish");
            _nugetPackages = nugetPackages ?? Path.Combine(RepoRoot, ".nuget", "packages");
            _pjDotnet = pjDotnet ?? GetPjDotnetPath();
            _stage2Sdk = Directory.EnumerateDirectories(Path.Combine(_artifacts, "stage2", "sdk")).First();
            _testPackages = Path.Combine(_artifacts, "tests", "packages");
        }

        private string GetPjDotnetPath()
        {
            return new DirectoryInfo(Path.Combine(RepoRoot, ".dotnet_stage0"))
                .GetDirectories().First()
                .GetFiles("dotnet*").First()
                .FullName;
        }
    }
}