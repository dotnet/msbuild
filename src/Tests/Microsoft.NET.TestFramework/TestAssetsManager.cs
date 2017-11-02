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
        public string ProjectsRoot { get; private set; }


        private string BuildVersion { get; set; }

        private List<String> TestDestinationDirectories { get; } = new List<string>();

        public TestAssetsManager()
        {
            var testAssetsDirectory = TestContext.Current.TestAssetsDirectory;
            var testProjectsDirectory = Path.Combine(testAssetsDirectory, "TestProjects");
            
            if (!Directory.Exists(testAssetsDirectory))
            {
                throw new DirectoryNotFoundException($"Directory not found: '{testAssetsDirectory}'");
            }

            if (!Directory.Exists(testProjectsDirectory))
            {
                throw new DirectoryNotFoundException($"Directory not found: '{testProjectsDirectory}'");
            }

            var buildVersion = Path.Combine(testAssetsDirectory, "buildVersion.txt");
            if (!File.Exists(buildVersion))
            {
                throw new FileNotFoundException($"File not found: {buildVersion}");
            }

            ProjectsRoot = testProjectsDirectory;
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

        public string GetAndValidateTestProjectDirectory(string testProjectName)
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
            string ret;
            if (testProjectName == callingMethod)
            {
                //  If testProjectName and callingMethod are the same, don't duplicate it in the test path
                ret = Path.Combine(baseDirectory, callingMethod + identifier);
            }
            else
            {
                ret = Path.Combine(baseDirectory, callingMethod + identifier, testProjectName);
            }

            TestDestinationDirectories.Add(ret);

            return ret;
        }

        const int MAX_PATH = 260;

        //  Drop root for signed build is 73 characters, then add 6 for "\Tests"
        const int MAX_TESTROOT_LENGTH = 79;

        //  1 space subtracted for path separator between base path and relative file path
        const int AVAILABLE_TEST_PATH_LENGTH = MAX_PATH - MAX_TESTROOT_LENGTH - 1;

        void ValidateDestinationDirectory(string path)
        {
            var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                string relativeFilePath = file.Replace(AppContext.BaseDirectory, "")
                    //  Remove path separator
                    .Substring(1);

                if (relativeFilePath.Length > AVAILABLE_TEST_PATH_LENGTH)
                {
                    throw new PathTooLongException("Test path may be too long: " + relativeFilePath);
                }
            }
        }

        public void ValidateDestinationDirectories()
        {
            foreach (var path in TestDestinationDirectories)
            {
                ValidateDestinationDirectory(path);
            }
        }
    }
}
