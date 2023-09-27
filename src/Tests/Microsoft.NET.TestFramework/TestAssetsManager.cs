// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Microsoft.NET.TestFramework
{
    public class TestAssetsManager
    {
        public string TestAssetsRoot { get; private set; }

        private List<string> TestDestinationDirectories { get; } = new List<string>();

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
            string testDestinationDirectory = null,
            bool allowCopyIfPresent = false)
        {
            var testProjectDirectory = GetAndValidateTestProjectDirectory(testProjectName, testAssetSubdirectory);

            var fileName = Path.GetFileNameWithoutExtension(callerFilePath);
            testDestinationDirectory ??= GetTestDestinationDirectoryPath(testProjectName, callingMethod + "_" + fileName, identifier, allowCopyIfPresent);
            TestDestinationDirectories.Add(testDestinationDirectory);

            var testAsset = new TestAsset(testProjectDirectory, testDestinationDirectory, TestContext.Current.SdkVersion, Log);
            return testAsset;
        }

        /// <summary>
        /// Writes an in-memory test project onto the disk.
        /// </summary>
        /// <param name="testProject">The testProject used to create a testAsset with.</param>
        /// <param name="callingMethod">Defaults to the name of the caller function (presumably the test).
        /// Used to prevent file collisions on tests which share the same test project.</param>
        /// <param name="identifier">Use this for theories.
        /// Pass in the unique theory parameters that can indentify that theory from others.
        /// The Identifier is used to distinguish between theory child tests.  Generally it should be created using a combination of all of the theory parameter values.
        /// This is distinct from the test project name and is used to prevent file collisions between theory tests that use the same test project.</param>
        /// <param name="targetExtension">The extension type of the desired test project, e.g. .csproj, or .fsproj.</param>
        /// <returns>A new TestAsset directory for the TestProject.</returns>
        public TestAsset CreateTestProject(
            TestProject testProject,
            [CallerMemberName] string callingMethod = "",
            string identifier = "",
            string targetExtension = ".csproj")
        {
            var testDestinationDirectory =
                GetTestDestinationDirectoryPath(testProject.Name, callingMethod, identifier);
            TestDestinationDirectories.Add(testDestinationDirectory);

            var testAsset = CreateTestProjectsInDirectory(new List<TestProject>() { testProject }, testDestinationDirectory, targetExtension);
            testAsset.TestProject = testProject;

            return testAsset;
        }

        /// <summary>
        /// Creates a list of test projects and adds them to a solution
        /// </summary>
        /// <param name="testProjects">The in-memory test projects to write to disk</param>
        /// <param name="callingMethod">Defaults to the name of the caller function (presumably the test).
        /// Used to prevent file collisions on tests which share the same test project.</param>
        /// <param name="identifier">Use this for theories.
        /// Pass in the unique theory parameters that can indentify that theory from others.
        /// The Identifier is used to distinguish between theory child tests.  Generally it should be created using a combination of all of the theory parameter values.
        /// This is distinct from the test project name and is used to prevent file collisions between theory tests that use the same test project.</param>
        /// <param name="targetExtension">The extension type of the desired test project, e.g. .csproj, or .fsproj.</param>
        /// <returns>A new TestAsset directory with the solution and test projects in it.</returns>
        public TestAsset CreateTestProjects(
            IEnumerable<TestProject> testProjects,
            [CallerMemberName] string callingMethod = "",
            string identifier = "",
            string targetExtension = ".csproj")
        {
            var testDestinationDirectory =
                GetTestDestinationDirectoryPath(callingMethod, callingMethod, identifier);
            TestDestinationDirectories.Add(testDestinationDirectory);

            var testAsset = CreateTestProjectsInDirectory(testProjects, testDestinationDirectory, targetExtension);

            var slnCreationResult = new DotnetNewCommand(Log, "sln")
                .WithVirtualHive()
                .WithWorkingDirectory(testDestinationDirectory)
                .Execute();

            if (slnCreationResult.ExitCode != 0)
            {
                throw new Exception($"This test failed during a call to dotnet new. If {testDestinationDirectory} is valid, it's likely this test is failing because of dotnet new. If there are failing .NET new tests, please fix those and then see if this test still fails.");
            }

            foreach (var testProject in testProjects)
            {
                new DotnetCommand(Log, "sln", "add", testProject.Name)
                    .WithWorkingDirectory(testDestinationDirectory)
                    .Execute()
                    .Should()
                    .Pass();
            }

            return testAsset;
        }

        private TestAsset CreateTestProjectsInDirectory(
            IEnumerable<TestProject> testProjects,
            string testDestinationDirectory,
            string targetExtension = ".csproj")
        {
            var testAsset = new TestAsset(testDestinationDirectory, TestContext.Current.SdkVersion, Log);

            Stack<TestProject> projectStack = new(testProjects);
            HashSet<TestProject> createdProjects = new();

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
