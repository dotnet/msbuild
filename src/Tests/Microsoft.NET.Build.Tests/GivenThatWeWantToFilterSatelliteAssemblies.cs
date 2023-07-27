// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToFilterSatelliteAssemblies : SdkTest
    {
        public GivenThatWeWantToFilterSatelliteAssemblies(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netcoreapp2.0", true, false)]
        [InlineData("netcoreapp3.0", false, false)]
        [InlineData("net47", false, true)]
        public void It_only_publish_selected_ResourceLanguages(string targetFramework, bool explicitCopyLocalLockFile,
            bool needsNetFrameworkReferenceAssemblies)
        {
            if (needsNetFrameworkReferenceAssemblies && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //  .NET Framework reference assemblies aren't currently available on non-Windows
                return;
            }

            var testProject = new TestProject()
            {
                Name = "FilterSatelliteAssemblies",
                TargetFrameworks = targetFramework,
                IsExe = true,
            };

            testProject.PackageReferences.Add(new TestPackageReference("System.Spatial", "5.8.3"));
            testProject.AdditionalProperties.Add("SatelliteResourceLanguages", "en-US;it;fr");
            if (explicitCopyLocalLockFile)
            {
                testProject.AdditionalProperties.Add("CopyLocalLockFileAssemblies", "true");
            }

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var buildCommand = new BuildCommand(testProjectInstance);
            var buildResult = buildCommand.Execute();

            buildResult.Should().Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework: testProject.TargetFrameworks);

            List<string> expectedFiles = new List<string>()
            {
                "it/System.Spatial.resources.dll",
                "fr/System.Spatial.resources.dll",
                "System.Spatial.dll",
                $"{testProject.Name}.pdb",
            };

            if (testProject.TargetFrameworks.StartsWith("netcoreapp"))
            {
                expectedFiles.AddRange(new[]
                {
                    $"{testProject.Name}.dll",
                    $"{testProject.Name}.deps.json",
                    $"{testProject.Name}.runtimeconfig.json",
                    $"{testProject.Name}.runtimeconfig.dev.json"
                });

                if (testProject.TargetFrameworks == "netcoreapp3.0")
                {
                    expectedFiles.Add($"{testProject.Name}{Constants.ExeSuffix}");
                }
            }
            else
            {
                expectedFiles.Add($"{testProject.Name}.exe");
                expectedFiles.Add($"{testProject.Name}.exe.config");
            }

            outputDirectory.Should().OnlyHaveFiles(expectedFiles);
        }
        [Theory]
        [InlineData("netcoreapp2.0", true, false)]
        [InlineData("net47", false, true)]
        public void It_copies_all_satellites_when_not_filtered(string targetFramework, bool explicitCopyLocalLockFile,
            bool needsNetFrameworkReferenceAssemblies)
        {
            if (needsNetFrameworkReferenceAssemblies && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //  .NET Framework reference assemblies aren't currently available on non-Windows
                return;
            }
            var testProject = new TestProject()
            {
                Name = "DontFilterSatelliteAssemblies",
                TargetFrameworks = targetFramework,
                IsExe = true,
            };

            testProject.PackageReferences.Add(new TestPackageReference("System.Spatial", "5.8.3"));
            if (explicitCopyLocalLockFile)
            {
                testProject.AdditionalProperties.Add("CopyLocalLockFileAssemblies", "true");
            }

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var buildCommand = new BuildCommand(testProjectInstance);
            var buildResult = buildCommand.Execute();

            buildResult.Should().Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework: testProject.TargetFrameworks);

            List<string> expectedFiles = new List<string>()
            {
                "de/System.Spatial.resources.dll",
                "es/System.Spatial.resources.dll",
                "fr/System.Spatial.resources.dll",
                "it/System.Spatial.resources.dll",
                "ja/System.Spatial.resources.dll",
                "ko/System.Spatial.resources.dll",
                "ru/System.Spatial.resources.dll",
                "zh-Hans/System.Spatial.resources.dll",
                "zh-Hant/System.Spatial.resources.dll",
                "System.Spatial.dll",
                $"{testProject.Name}.pdb",
            };

            if (testProject.TargetFrameworks.StartsWith("netcoreapp"))
            {
                expectedFiles.AddRange(new[]
                {
                    $"{testProject.Name}.dll",
                    $"{testProject.Name}.deps.json",
                    $"{testProject.Name}.runtimeconfig.json",
                    $"{testProject.Name}.runtimeconfig.dev.json"
                });
            }
            else
            {
                expectedFiles.Add($"{testProject.Name}.exe");
                expectedFiles.Add($"{testProject.Name}.exe.config");
            }

            outputDirectory.Should().OnlyHaveFiles(expectedFiles);
        }
    }
}
