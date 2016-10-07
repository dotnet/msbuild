// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Microsoft.NET.TestFramework
{
    public class TestAssetsManager
    {
        private static TestAssetsManager _testProjectsAssetsManager;

        public static TestAssetsManager TestProjectsAssetsManager
        {
            get
            {
                if (_testProjectsAssetsManager == null)
                {
                    var testAssetsDirectory = Path.Combine(RepoInfo.RepoRoot, "TestAssets");
                    var testProjectsDirectory = Path.Combine(testAssetsDirectory, "TestProjects");
                    _testProjectsAssetsManager = new TestAssetsManager(testAssetsDirectory, testProjectsDirectory);
                }

                return _testProjectsAssetsManager;
            }
        }

        public string ProjectsRoot { get; private set; }


        private string BuildVersion { get; set; }

        public TestAssetsManager(string assetsRoot, string projectRoot)
        {
            if (!Directory.Exists(assetsRoot))
            {
                throw new DirectoryNotFoundException($"Directory not found: '{assetsRoot}'");
            }

            if (!Directory.Exists(projectRoot))
            {
                throw new DirectoryNotFoundException($"Directory not found: '{projectRoot}'");
            }

            var buildVersion = Path.Combine(assetsRoot, "buildVersion.txt");
            if (!File.Exists(buildVersion))
            {
                throw new FileNotFoundException($"File not found: {buildVersion}");
            }

            ProjectsRoot = projectRoot;
            BuildVersion = File.ReadAllText(buildVersion).Trim();
        }

        public TestAsset CopyTestAsset(
            string testProjectName,
            [CallerMemberName] string callingMethod = "",
            string identifier = "")
        {
            var testProjectDirectory = GetAndValidateTestProjectDirectory(testProjectName);
            var testDestinationDirectory =
                GetTestDestinationDirectoryPath(testProjectName, callingMethod, identifier);

            var testAsset = new TestAsset(testProjectDirectory, testDestinationDirectory, BuildVersion);
            return testAsset;
        }

        private string GetAndValidateTestProjectDirectory(string testProjectName)
        {
            string testProjectDirectory = Path.Combine(ProjectsRoot, testProjectName);

            if (!Directory.Exists(testProjectDirectory))
            {
                throw new Exception($"Cannot find '{testProjectName}' at '{ProjectsRoot}'");
            }

            return testProjectDirectory;
        }

        private string GetTestDestinationDirectoryPath(
            string testProjectName,
            string callingMethod,
            string identifier)
        {
#if NET451
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
#else
            string baseDirectory = AppContext.BaseDirectory;
#endif
            return Path.Combine(baseDirectory, callingMethod + identifier, testProjectName);
        }
    }
}
