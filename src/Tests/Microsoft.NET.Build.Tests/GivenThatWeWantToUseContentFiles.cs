// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{

    public class GivenThatWeWantToUseContentFiles : SdkTest
    {
        public GivenThatWeWantToUseContentFiles(ITestOutputHelper log) : base(log)
        {
        }


        [Fact]
        public void It_handles_content_files_correctly()
        {
            const string targetFramework = ToolsetInfo.CurrentTargetFramework;

            var project = new TestProject
            {
                Name = "ContentFiles",
                IsExe = true,
                TargetFrameworks = targetFramework,
                PackageReferences = { new TestPackageReference("ContentFilesExample", "1.0.2") },
            };

            project.SourceFiles[project.Name + ".cs"] =
$@"
using System;

namespace {project.Name}
{{
    static class Program
    {{
        static void Main()
        {{
            Console.WriteLine(ExampleReader.GetDataText());
        }}
    }}
}}";

            var asset = _testAssetsManager
                .CreateTestProject(project);

            // First Build
            var cmd = new BuildCommand(asset);
            cmd.Execute().Should().Pass();

            string outputDir = cmd.GetOutputDirectory(targetFramework).FullName;
            string intmediateDir = cmd.GetIntermediateDirectory(targetFramework).FullName;
            var dirEnum = Directory.GetFiles(intmediateDir, "ExampleReader.cs", SearchOption.AllDirectories);
            string contentFileName = dirEnum.FirstOrDefault();
            contentFileName.Should().NotBeNullOrEmpty("Unable to locate 'ExampleReader.cs'");

            string[] filePaths =
                    {
                        Path.Combine(outputDir, @"ContentFiles.deps.json"),
                        Path.Combine(outputDir, @"ContentFiles.dll"),
                        Path.Combine(outputDir, @"ContentFiles.pdb"),
                        Path.Combine(outputDir, @"ContentFiles.runtimeconfig.json"),
                        Path.Combine(outputDir, @"tools", "run.cmd"),
                        Path.Combine(outputDir, @"tools", "run.sh"),
                        contentFileName,
                    };

            VerifyFileExists(filePaths, true, out DateTime firstBuild);

            // Incremental Build
            cmd = new BuildCommand(asset);
            cmd.Execute().Should().Pass();
            VerifyFileExists(filePaths, true, out DateTime firstIncremental);

            (firstBuild == firstIncremental).Should().BeTrue("First Incremental build should not update any files in the output directory.");

            // Incremental Build
            cmd = new BuildCommand(asset);
            cmd.Execute().Should().Pass();
            VerifyFileExists(filePaths, true, out DateTime secondIncremental);

            (firstBuild == secondIncremental).Should().BeTrue("Second Incremental build should not update any files in the output directory.");

            // Clean Project
            var cleanCmd = new MSBuildCommand(asset, "Clean");
            cleanCmd.Execute().Should().Pass();
            VerifyFileExists(filePaths, false, out _);

            // Rebuild Project
            var rebuildCmd = new MSBuildCommand(asset, "ReBuild");
            rebuildCmd.Execute().Should().Pass();
            VerifyFileExists(filePaths, true, out _);

            // Rebuild again to verify that clean worked.
            rebuildCmd = new MSBuildCommand(asset, "ReBuild");
            rebuildCmd.Execute().Should().Pass();
            VerifyFileExists(filePaths, true, out _);

            // Validate Clean Project works after a Rebuild
            cleanCmd = new MSBuildCommand(asset, "Clean");
            cleanCmd.Execute().Should().Pass();
            VerifyFileExists(filePaths, false, out _);
        }

        private void VerifyFileExists(string[] fileList, bool shouldExists, out DateTime latestDate)
        {
            latestDate = DateTime.MinValue;
            long longTime = 0;

            foreach (string filePath in fileList)
            {
                var fileInfo = new FileInfo(filePath);
                if (shouldExists)
                    fileInfo.Should().Exist();
                else
                    fileInfo.Should().NotExist();

                longTime = Math.Max(fileInfo.CreationTimeUtc.Ticks, latestDate.Ticks);
            }

            latestDate = DateTime.FromFileTimeUtc(longTime);
        }
    }
}
