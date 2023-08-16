// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishIncrementally : SdkTest
    {
        public GivenThatWeWantToPublishIncrementally(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_cleans_before_single_file_publish()
        {
            var testProject = new TestProject()
            {
                Name = "RegularPublishToSingleExe",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                RuntimeIdentifier = "win-x86"
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);



            // Publish normally
            var publishCommand = new PublishCommand(testAsset);

            publishCommand
                .Execute()
                .Should()
                .Pass();

            var publishDir = publishCommand.GetOutputDirectory(runtimeIdentifier: "win-x86").FullName;
            var expectedNonSingleExeFiles = new string[] { ".dll", ".deps.json", ".runtimeconfig.json" }
                .Select(ending => testProject.Name + ending);
            var expectedSingleExeFiles = new string[] { ".exe", ".pdb" }.Select(ending => testProject.Name + ending);

            CheckPublishOutput(publishDir, expectedSingleExeFiles.Concat(expectedNonSingleExeFiles), null);

            File.WriteAllText(Path.Combine(publishDir, "UserData.txt"), string.Empty);

            // Publish as a single file
            publishCommand
                .Execute(@"/p:PublishSingleFile=true")
                .Should()
                .Pass();
            CheckPublishOutput(publishDir, expectedSingleExeFiles.Append("UserData.txt"), expectedNonSingleExeFiles);
        }

        [Fact]
        public void It_cleans_between_renames()
        {
            var testProject = new TestProject()
            {
                Name = "PublishSingleFile1",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                RuntimeIdentifier = "win-x86"
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);


            // Publish as a single file
            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute(@"/p:PublishSingleFile=true")
                .Should()
                .Pass();

            var publishDir = publishCommand.GetOutputDirectory(runtimeIdentifier: "win-x86").FullName;
            var expectedSingleExeFileExtensions = new string[] { ".exe", ".pdb" };

            CheckPublishOutput(publishDir, expectedSingleExeFileExtensions.Select(ending => testProject.Name + ending), null);

            File.WriteAllText(Path.Combine(publishDir, "UserData.txt"), string.Empty);
            File.WriteAllText(Path.Combine(publishDir, testProject.Name + ".deps.json"), string.Empty);

            // Rename the project
            var newName = "PublishSingleFile2";
            File.Move(Path.Combine(testAsset.TestRoot, testProject.Name, testProject.Name + ".csproj"),
                Path.Combine(testAsset.TestRoot, testProject.Name, newName + ".csproj"));

            // Publish as a single file
            new PublishCommand(testAsset)
                .Execute(@"/p:PublishSingleFile=true")
                .Should()
                .Pass();
            CheckPublishOutput(publishDir, expectedSingleExeFileExtensions.Select(ending => newName + ending).Append("UserData.txt").Append(testProject.Name + ".deps.json"),
                expectedSingleExeFileExtensions.Select(ending => testProject.Name + ending));
        }

        [Fact]
        public void It_cleans_between_single_file_publishes()
        {
            var testProject = new TestProject()
            {
                Name = "PublishSingleExe",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                RuntimeIdentifier = "win-x86"
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            // Publish as a single file
            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute(@"/p:PublishSingleFile=true")
                .Should()
                .Pass();

            var publishDir = publishCommand.GetOutputDirectory(runtimeIdentifier: "win-x86").FullName;
            var expectedSingleExeFiles = new string[] { ".exe", ".pdb" }.Select(ending => testProject.Name + ending);

            CheckPublishOutput(publishDir, expectedSingleExeFiles, null);

            // Write a file that would have been in a full publish, should still be there after another single file publish
            File.WriteAllText(Path.Combine(publishDir, testProject.Name + ".dll"), string.Empty);

            // Publish as a single file
            publishCommand
                .Execute(@"/p:PublishSingleFile=true")
                .Should()
                .Pass();
            CheckPublishOutput(publishDir, expectedSingleExeFiles.Append(testProject.Name + ".dll"), null);
        }

        [Fact]
        public void It_cleans_before_trimmed_single_file_publish()
        {
            var testProject = new TestProject()
            {
                Name = "RegularPublishToTrimmedSingleExe",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                RuntimeIdentifier = "win-x86",
                SelfContained = "true"
            };

            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            // Publish trimmed
            var publishCommand = new PublishCommand(testAsset);

            publishCommand
                .Execute()
                .Should()
                .Pass();

            var publishDir = publishCommand.GetOutputDirectory(runtimeIdentifier: "win-x86").FullName;
            var expectedNonSingleExeFiles = new string[] { ".dll", ".deps.json", ".runtimeconfig.json" }
                .Select(ending => testProject.Name + ending);
            var expectedSingleExeFiles = new string[] { ".exe", ".pdb" }.Select(ending => testProject.Name + ending);

            CheckPublishOutput(publishDir, expectedSingleExeFiles.Concat(expectedNonSingleExeFiles), null);

            File.WriteAllText(Path.Combine(publishDir, "UserData.txt"), string.Empty);

            // Publish as a single file
            publishCommand
                .Execute(@"/p:PublishSingleFile=true")
                .Should()
                .Pass();
            CheckPublishOutput(publishDir, expectedSingleExeFiles.Append("UserData.txt"), expectedNonSingleExeFiles);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_cleans_for_mvc_projects()
        {
            // Create new mvc app from template
            var testDir = _testAssetsManager.CreateTestDirectory();
            var assetName = "MVCPublishProject";
            var runtimeId = "win-x86";
            new DotnetNewCommand(Log)
                .WithVirtualHive()
                .WithWorkingDirectory(testDir.Path)
                .Execute("mvc", "-n", assetName)
                .Should()
                .Pass();

            var expectedRegularFiles = new string[] { ".dll", ".deps.json", ".runtimeconfig.json" }
                .Select(ending => assetName + ending);
            var expectedSingleFiles = new string[] { ".pdb", ".exe" }.Select(ending => assetName + ending)
                .Concat(new string[] { "appsettings.json", "appsettings.Development.json", "web.config" });

            // Publish normally
            new PublishCommand(Log, Path.Combine(testDir.Path, assetName))
                .Execute(@"/p:RuntimeIdentifier=" + runtimeId)
                .Should()
                .Pass();
            var publishDir = Path.Combine(Directory.GetDirectories(Path.Combine(testDir.Path, assetName, "bin", "Debug")).FirstOrDefault(), runtimeId, "publish");
            CheckPublishOutput(publishDir, expectedSingleFiles.Concat(expectedRegularFiles), null);
            Directory.Exists(Path.Combine(publishDir, "wwwroot"));

            File.WriteAllText(Path.Combine(publishDir, "UserData.txt"), string.Empty);

            // Publish as a single file
            new PublishCommand(Log, Path.Combine(testDir.Path, assetName))
                .Execute(@"/p:RuntimeIdentifier=win-x86;PublishSingleFile=true")
                .Should()
                .Pass();
            CheckPublishOutput(publishDir, expectedSingleFiles.Append("UserData.txt"), expectedRegularFiles);
            Directory.Exists(Path.Combine(publishDir, "wwwroot"));
        }

        [Fact]
        public void It_cleans_with_custom_output_dir()
        {
            var testProject = new TestProject()
            {
                Name = "PublishToCustomDir",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                RuntimeIdentifier = "win-x86"
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var publishOutputFolder = "publishOutput";
            var publishDir = Path.Combine(testAsset.TestRoot, testProject.Name, publishOutputFolder);
            var expectedNonSingleExeFiles = new string[] { ".dll", ".deps.json", ".runtimeconfig.json" }
                .Select(ending => testProject.Name + ending);
            var expectedSingleExeFiles = new string[] { ".exe", ".pdb" }.Select(ending => testProject.Name + ending);

            // Publish normally
            new PublishCommand(testAsset)
                .Execute("/p:PublishDir=" + publishOutputFolder)
                .Should()
                .Pass();
            CheckPublishOutput(publishDir, expectedSingleExeFiles.Concat(expectedNonSingleExeFiles), null);

            File.WriteAllText(Path.Combine(publishDir, "UserData.txt"), string.Empty);

            // Publish as a single file
            new PublishCommand(testAsset)
                .Execute(@"/p:PublishSingleFile=true", "/p:PublishDir=" + publishOutputFolder)
                .Should()
                .Pass();
            CheckPublishOutput(publishDir, expectedSingleExeFiles.Append("UserData.txt"), expectedNonSingleExeFiles);
        }

        [Fact]
        public void It_cleans_with_multiple_output_dirs()
        {
            var testProject = new TestProject()
            {
                Name = "PublishToMultipleDirs",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                RuntimeIdentifier = "win-x86"
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var publishOutputFolder1 = "publishOutput1";
            var publishOutputFolder2 = "publishOutput2";
            var publishDir1 = Path.Combine(testAsset.TestRoot, testProject.Name, publishOutputFolder1);
            var publishDir2 = Path.Combine(testAsset.TestRoot, testProject.Name, publishOutputFolder2);
            var expectedNonSingleExeFiles = new string[] { ".dll", ".deps.json", ".runtimeconfig.json" }
                .Select(ending => testProject.Name + ending);
            var expectedSingleExeFiles = new string[] { ".exe", ".pdb" }.Select(ending => testProject.Name + ending);

            // Publish normally in folder 1
            new PublishCommand(testAsset)
                .Execute("/p:PublishDir=" + publishOutputFolder1)
                .Should()
                .Pass();
            CheckPublishOutput(publishDir1, expectedSingleExeFiles.Concat(expectedNonSingleExeFiles), null);

            // Publish as a single file in folder 2
            new PublishCommand(testAsset)
                .Execute(@"/p:PublishSingleFile=true", "/p:PublishDir=" + publishOutputFolder2)
                .Should()
                .Pass();
            CheckPublishOutput(publishDir2, expectedSingleExeFiles, expectedNonSingleExeFiles);
            // Check that publishing to a different folder didn't change folder 1
            CheckPublishOutput(publishDir1, expectedSingleExeFiles.Concat(expectedNonSingleExeFiles), null);

            // Change name and publish again to folder 1
            var newName = "PublishToMultipleDirs1";
            File.Move(Path.Combine(testAsset.TestRoot, testProject.Name, testProject.Name + ".csproj"),
                Path.Combine(testAsset.TestRoot, testProject.Name, newName + ".csproj"));
            new PublishCommand(testAsset)
                .Execute("/p:PublishDir=" + publishOutputFolder1)
                .Should()
                .Pass();
            CheckPublishOutput(publishDir1, new string[] { ".dll", ".deps.json", ".runtimeconfig.json", ".exe", ".pdb" }
                .Select(ending => newName + ending), expectedSingleExeFiles.Concat(expectedNonSingleExeFiles));
            CheckPublishOutput(publishDir2, expectedSingleExeFiles, expectedNonSingleExeFiles);
        }

        [Theory]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void GeneratePublishDependencyFile_runs_incrementally(string targetFramework)
        {
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = new TestProject()
            {
                Name = "PublishDependencyFileIncremental",
                TargetFrameworks = targetFramework,
                IsExe = true,
                RuntimeIdentifier = rid
            };

            testProject.PackageReferences.Add(new TestPackageReference("NewtonSoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion(), publish: "false"));
            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var publishCommand = new PublishCommand(testAsset);
            var publishDir = publishCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: rid).FullName;
            var depsJsonPath = Path.Combine(publishDir, testProject.Name + ".deps.json");

            publishCommand.Execute().Should().Pass();
            DateTime depsJsonFirstModifiedTime = File.GetLastWriteTimeUtc(depsJsonPath);

            WaitForUtcNowToAdvance();

            publishCommand.Execute().Should().Pass();
            DateTime depsJsonSecondModifiedTime = File.GetLastWriteTimeUtc(depsJsonPath);

            depsJsonSecondModifiedTime.Should().Be(depsJsonFirstModifiedTime);
        }

        private void CheckPublishOutput(string publishDir, IEnumerable<string> expectedFiles, IEnumerable<string> unexpectedFiles)
        {
            if (expectedFiles != null)
            {
                foreach (var expectedFile in expectedFiles)
                {
                    new FileInfo(Path.Combine(publishDir, expectedFile)).Should().Exist();
                }
            }
            if (unexpectedFiles != null)
            {
                foreach (var unexpectedFile in unexpectedFiles)
                {
                    new FileInfo(Path.Combine(publishDir, unexpectedFile)).Should().NotExist();
                }
            }
        }
    }
}
