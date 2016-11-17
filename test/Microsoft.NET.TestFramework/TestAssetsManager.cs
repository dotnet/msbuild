// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.TestFramework.ProjectConstruction;
using System;
using System.Collections.Generic;
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

        public TestAsset CreateTestProject(
            TestProject testProject,
            [CallerMemberName] string callingMethod = "",
            string identifier = "")
        {
            var testDestinationDirectory =
                GetTestDestinationDirectoryPath(testProject.Name, callingMethod, identifier);

            var testAsset = new TestAsset(testDestinationDirectory, BuildVersion);

            Stack<TestProject> projectStack = new Stack<TestProject>();
            projectStack.Push(testProject);

            HashSet<TestProject> createdProjects = new HashSet<TestProject>();

            while (projectStack.Count > 0)
            {
                var project = projectStack.Pop();
                if (!createdProjects.Contains(project))
                {
                    project.Create(testAsset, ProjectsRoot);
                    createdProjects.Add(project);

                    foreach (var referencedProject in project.ReferencedProjects)
                    {
                        projectStack.Push(referencedProject);
                    }
                }
            }

            return testAsset;
        }

        private string GetAndValidateTestProjectDirectory(string testProjectName)
        {
            string testProjectDirectory = Path.Combine(ProjectsRoot, testProjectName);

            if (!Directory.Exists(testProjectDirectory))
            {
                throw new DirectoryNotFoundException($"Cannot find '{testProjectName}' at '{ProjectsRoot}'");
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
