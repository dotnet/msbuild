// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.TestFramework.ProjectConstruction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.NET.TestFramework
{
    public class TestAssetsManager
    {
        public string ProjectsRoot { get; private set; }

        private List<String> TestDestinationDirectories { get; } = new List<string>();

        public TestAssetsManager()
        {
            var testAssetsDirectory = TestContext.Current.TestAssetsDirectory;

            if (!Directory.Exists(testAssetsDirectory))
            {
                throw new DirectoryNotFoundException($"Directory not found: '{testAssetsDirectory}'");
            }

            ProjectsRoot = testAssetsDirectory;
        }

        public TestAsset CopyTestAsset(
            string testProjectName,
            [CallerMemberName] string callingMethod = "",
            string identifier = "")
        {
            var testProjectDirectory = GetAndValidateTestProjectDirectory(testProjectName);

            var testDestinationDirectory =
                GetTestDestinationDirectoryPath(testProjectName, callingMethod, identifier);
            TestDestinationDirectories.Add(testDestinationDirectory);

            var testAsset = new TestAsset(testProjectDirectory, testDestinationDirectory, TestContext.Current.SdkVersion);
            return testAsset;
        }

        public TestAsset CreateTestProject(
            TestProject testProject,
            [CallerMemberName] string callingMethod = "",
            string identifier = "")
        {
            var testDestinationDirectory =
                GetTestDestinationDirectoryPath(testProject.Name, callingMethod, identifier);
            TestDestinationDirectories.Add(testDestinationDirectory);

            var testAsset = new TestAsset(testDestinationDirectory, TestContext.Current.SdkVersion);

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

        public TestDirectory CreateTestDirectory([CallerMemberName] string testName = null, string identifier = null)
        {
            string dir = GetTestDestinationDirectoryPath(testName, testName, identifier ?? string.Empty);
            return new TestDirectory(dir, TestContext.Current.SdkVersion);
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

        public static string GetTestDestinationDirectoryPath(
            string testProjectName,
            string callingMethod,
            string identifier)
        {
            string baseDirectory = TestContext.Current.TestExecutionDirectory;
            var directoryName = new StringBuilder(callingMethod).Append(identifier);

            if (testProjectName != callingMethod)
            {
                directoryName = directoryName.Append(testProjectName);
            }

            // We need to ensure the directory name isn't over 24 characters in length
            if (directoryName.Length > 24)
            {
                using (var sha256 = SHA256.Create())
                {
                    var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(directoryName.ToString()));

                    directoryName = directoryName.Remove(13, directoryName.Length - 13)
                                                 .Append("---");

                    directoryName = directoryName.AppendFormat("{0:X2}", hash[0])
                                                 .AppendFormat("{0:X2}", hash[1])
                                                 .AppendFormat("{0:X2}", hash[2])
                                                 .AppendFormat("{0:X2}", hash[3]);
                }
            }

            return Path.Combine(baseDirectory, directoryName.ToString());
        }
    }
}
