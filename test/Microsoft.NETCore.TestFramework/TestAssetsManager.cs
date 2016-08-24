// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Microsoft.NETCore.TestFramework
{
    public class TestAssetsManager
    {
        private static TestAssetsManager _testProjectsAssetsManager;

        public static TestAssetsManager TestProjectsAssetsManager
        {
            get
            {
                if(_testProjectsAssetsManager == null)
                {
                    var testProjectsDirectory = Path.Combine(RepoInfo.RepoRoot, "TestAssets", "TestProjects");
                    _testProjectsAssetsManager = new TestAssetsManager(testProjectsDirectory);
                }

                return _testProjectsAssetsManager;
            }
        }

        public string AssetsRoot
        {
            get; private set;
        }

        public TestAssetsManager(string assetsRoot)
        {
            if (!Directory.Exists(assetsRoot))
            {
                throw new DirectoryNotFoundException($"Directory not found: '{assetsRoot}'");
            }

            AssetsRoot = assetsRoot;
        }

        public TestAsset CopyTestAsset(
            string testProjectName,
            [CallerMemberName] string callingMethod = "",
            string identifier = "")
        {
            var testProjectDirectory = GetAndValidateTestProjectDirectory(testProjectName);
            var testDestinationDirectory =
                GetTestDestinationDirectoryPath(testProjectName, callingMethod, identifier);

            var testAsset = new TestAsset(testProjectDirectory, testDestinationDirectory);
            return testAsset;
        }

        private string GetAndValidateTestProjectDirectory(string testProjectName)
        {
            string testProjectDirectory = Path.Combine(AssetsRoot, testProjectName);

            if (!Directory.Exists(testProjectDirectory))
            {
                throw new Exception($"Cannot find '{testProjectName}' at '{AssetsRoot}'");
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
