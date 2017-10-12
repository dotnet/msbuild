using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Commands;

namespace Microsoft.NET.TestFramework
{
    public class TestContext
    {
        //  Generally the folder the test DLL is in
        public string TestExecutionDirectory { get; set; }

        public string TestAssetsDirectory { get; set; }

        public string NuGetCachePath { get; set; }

        public string NuGetFallbackFolder { get; set; }

        public string NuGetExePath { get; set; }

        public ToolsetInfo ToolsetUnderTest { get; set; }

        private static TestContext _current;

        public static TestContext Current
        {
            get
            {
                if (_current == null)
                {
                    //  Initialize test context in cases where it hasn't been initialized via the entry point
                    //  (ie when using test explorer or another runner)
                    Initialize(TestCommandLine.Parse(Array.Empty<string>()));
                }
                return _current;
            }
            set
            {
                _current = value;
            }
        }

        // For test purposes, override the implicit .NETCoreApp version for self-contained apps that to builds thare 
        //  (1) different from the fixed framework-dependent defaults (1.0.5, 1.1.2, 2.0.0)
        //  (2) currently available on nuget.org
        //
        // This allows bumping the versions before builds without causing tests to fail.
        public const string ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_0 = "1.0.4";
        public const string ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_1 = "1.1.1";
        public const string ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp2_0 = "2.0.0-preview2-25407-01";

        public void AddTestEnvironmentVariables(SdkCommandSpec command)
        {
            command.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";

            //  Set NUGET_PACKAGES environment variable to match value from build.ps1
            command.Environment["NUGET_PACKAGES"] = NuGetCachePath;

            command.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";

            command.Environment[nameof(ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_0)] = ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_0;
            command.Environment[nameof(ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_1)] = ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp1_1;
            command.Environment[nameof(ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp2_0)] = ImplicitRuntimeFrameworkVersionForSelfContainedNetCoreApp2_0;

            command.Environment["GenerateResourceMSBuildArchitecture"] = "CurrentArchitecture";
            command.Environment["GenerateResourceMSBuildRuntime"] = "CurrentRuntime";

            ToolsetUnderTest.AddTestEnvironmentVariables(command);
        }


        public static void Initialize(TestCommandLine commandLine)
        {
            TestContext testContext = new TestContext();
            testContext.TestExecutionDirectory = AppContext.BaseDirectory;
            testContext.TestAssetsDirectory = FindFolderInTree("TestAssets", AppContext.BaseDirectory);

            string repoRoot = null;
            string repoConfiguration = null;

            if (commandLine.SDKRepoPath != null)
            {
                repoRoot = commandLine.SDKRepoPath;
            }
            else if (!commandLine.NoRepoInference)
            {
                repoRoot = GetRepoRoot();

                if (repoRoot != null)
                {
                    // assumes tests are always executed from the "bin/$Configuration/Tests" directory
                    repoConfiguration = new DirectoryInfo(AppContext.BaseDirectory).Parent.Name;
                }
            }
            if (repoRoot != null)
            {
                testContext.NuGetFallbackFolder = Path.Combine(repoRoot, "bin", "NuGetFallbackFolder");
                testContext.NuGetExePath = Path.Combine(repoRoot, ".nuget", $"nuget{Constants.ExeSuffix}");
                testContext.NuGetCachePath = Path.Combine(repoRoot, "packages");
            }
            else
            {
                testContext.NuGetFallbackFolder = FindOrCreateFolderInTree("NuGetFallbackFolder", AppContext.BaseDirectory);
                testContext.NuGetCachePath = FindOrCreateFolderInTree("packages", AppContext.BaseDirectory);
                var nuGetFolder = FindFolderInTree(".nuget", AppContext.BaseDirectory, false);
                if (nuGetFolder != null)
                {
                    testContext.NuGetExePath = Path.Combine(nuGetFolder, $"nuget{Constants.ExeSuffix}");
                }
            }

            testContext.ToolsetUnderTest = ToolsetInfo.Create(repoRoot, repoConfiguration, commandLine);

            TestContext.Current = testContext;
        }

        private static string GetRepoRoot()
        {
            string directory = AppContext.BaseDirectory;

            while (!Directory.Exists(Path.Combine(directory, ".git")) && directory != null)
            {
                directory = Directory.GetParent(directory).FullName;
            }

            if (directory == null)
            {
                return null;
            }
            return directory;
        }
        private static string FindOrCreateFolderInTree(string relativePath, string startPath)
        {
            string ret = FindFolderInTree(relativePath, startPath, throwIfNotFound: false);
            if (ret != null)
            {
                return ret;
            }
            ret = Path.Combine(startPath, relativePath);
            Directory.CreateDirectory(ret);
            return ret;
        }
        private static string FindFolderInTree(string relativePath, string startPath, bool throwIfNotFound = true)
        {
            string currentPath = startPath;
            while (true)
            {
                string path = Path.Combine(currentPath, relativePath);
                if (Directory.Exists(path))
                {
                    return path;
                }
                var parent = Directory.GetParent(currentPath);
                if (parent == null)
                {
                    if (throwIfNotFound)
                    {
                        throw new FileNotFoundException($"Could not find folder '{relativePath}' in '{startPath}' or any of its ancestors");
                    }
                    else
                    {
                        return null;
                    }
                }
                currentPath = parent.FullName;
            }
        }
    }
}
