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
        public string Stage2Sdk => _stage2Sdk;
        public string TestPackages => _testPackages;

        public RepoDirectoriesProvider(
            string artifacts = null,
            string builtDotnet = null,
            string nugetPackages = null,
            string corehostPackages = null,
            string corehostDummyPackages = null)
        {
            var currentRid = RuntimeEnvironment.GetRuntimeIdentifier();

            _artifacts = artifacts ?? Path.Combine(RepoRoot, "artifacts", currentRid);
            _nugetPackages = nugetPackages ?? Path.Combine(RepoRoot, ".nuget", "packages");
            _builtDotnet = builtDotnet ?? Path.Combine(_artifacts, "intermediate", "sharedFrameworkPublish");
            _stage2Sdk = Directory.EnumerateDirectories(Path.Combine(_artifacts, "stage2", "sdk")).First();
            _testPackages = Path.Combine(_artifacts, "tests", "packages");
        }
    }
}