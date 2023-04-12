// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.TestFramework.ProjectConstruction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework
{
    public class TestAssetsManager
    {
        public string TestAssetsRoot { get; private set; }

        private List<String> TestDestinationDirectories { get; } = new List<string>();

        protected ITestOutputHelper Log { get; }

        public TestAssetsManager(ITestOutputHelper log)
        {
            var testAssetsDirectory = TestContext.Current.TestAssetsDirectory;
            Log = log;

            if (!Directory.Exists(testAssetsDirectory))
            {
                throw new DirectoryNotFoundException($"Directory not found: '{testAssetsDirectory}'");
            }

            TestAssetsRoot = testAssetsDirectory;
        }

        public TestAsset CopyTestAsset(
            string testProjectName,
            [CallerMemberName] string callingMethod = "",
            [CallerFilePath] string callerFilePath = null,
            string identifier = "",
            string testAssetSubdirectory = "",
            bool allowCopyIfPresent = false)
        {
            var testProjectDirectory = GetAndValidateTestProjectDirectory(testProjectName, testAssetSubdirectory);

            var fileName = Path.GetFileNameWithoutExtension(callerFilePath);
            var testDestinationDirectory =
                GetTestDestinationDirectoryPath(testProjectName, callingMethod + "_" + fileName, identifier, allowCopyIfPresent);
            TestDestinationDirectories.Add(testDestinationDirectory);

            var testAsset = new TestAsset(testProjectDirectory, testDestinationDirectory, TestContext.Current.SdkVersion, Log);
            return testAsset;
        }

        public TestAsset CreateTestProject(
            TestProject testProject,
            [CallerMemberName] string callingMethod = "",
            string identifier = "",
            string targetExtension = ".csproj")
        {
            var testDestinationDirectory =
                GetTestDestinationDirectoryPath(testProject.Name, callingMethod, identifier);
            TestDestinationDirectories.Add(testDestinationDirectory);

            var testAsset = new TestAsset(testDestinationDirectory, TestContext.Current.SdkVersion, Log);
            testAsset.TestProject = testProject;

            Stack<TestProject> projectStack = new Stack<TestProject>();
            projectStack.Push(testProject);

            HashSet<TestProject> createdProjects = new HashSet<TestProject>();

            while (projectStack.Count > 0)
            {
                var project = projectStack.Pop();
                if (!createdProjects.Contains(project))
                {
                    project.Create(testAsset, TestAssetsRoot, targetExtension);
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

        public string GetAndValidateTestProjectDirectory(string testProjectName, string testAssetSubdirectory = "")
        {
            if (string.IsNullOrEmpty(testAssetSubdirectory))
            {
                testAssetSubdirectory = "TestProjects";
            }
            string testProjectDirectory = Path.Combine(TestAssetsRoot, testAssetSubdirectory, testProjectName);

            if (!Directory.Exists(testProjectDirectory))
            {
                throw new DirectoryNotFoundException($"Cannot find test project directory '{testProjectDirectory}'");
            }

            return testProjectDirectory;
        }

        public static string GetTestDestinationDirectoryPath(
            string testProjectName,
            string callingMethodAndFileName,
            string identifier,
            bool allowCopyIfPresent = false)
        {
            string baseDirectory = TestContext.Current.TestExecutionDirectory;
            var directoryName = new StringBuilder(callingMethodAndFileName).Append(identifier);

            if (testProjectName != callingMethodAndFileName)
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

            var directoryPath = Path.Combine(baseDirectory, directoryName.ToString());
#if CI_BUILD
            if (!allowCopyIfPresent && Directory.Exists(directoryPath))
            {
                //Arcade test retry reuses the machine so the directory might already be present in CI
                directoryPath = Directory.Exists(directoryPath+"_1") ? directoryPath+"_2" : directoryPath+"_1";
                if (Directory.Exists(directoryPath))
                {
                    throw new Exception($"Test dir {directoryPath} already exists");
                }
            }
#endif

            return directoryPath;
        }
    }
}
