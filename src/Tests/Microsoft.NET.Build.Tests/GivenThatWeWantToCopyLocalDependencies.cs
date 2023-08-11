// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToCopyLocalDependencies : SdkTest
    {
        public GivenThatWeWantToCopyLocalDependencies(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_copies_local_package_dependencies_on_build()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new TestProject()
            {
                Name = ProjectName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = _testAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            var expectedFiles = new[]
            {
                $"{ProjectName}{Constants.ExeSuffix}",
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                $"{ProjectName}.runtimeconfig.json",
                "Newtonsoft.Json.dll",
                "runtimes/linux-x64/native/libsqlite3.so",
                "runtimes/osx-x64/native/libsqlite3.dylib",
                "runtimes/win7-x64/native/sqlite3.dll",
                "runtimes/win7-x86/native/sqlite3.dll"
            };

            outputDirectory.Should().OnlyHaveFiles(expectedFiles);
        }

        [Fact]
        public void It_does_not_copy_local_package_dependencies_when_requested_not_to()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new TestProject()
            {
                Name = ProjectName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.AdditionalProperties["CopyLocalLockFileAssemblies"] = "false";
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = _testAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute().Should().Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}{Constants.ExeSuffix}",
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                $"{ProjectName}.runtimeconfig.json",
            });
        }

        [Fact]
        public void It_copies_local_specific_runtime_package_dependencies_on_build()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            var rid = EnvironmentInfo.GetCompatibleRid(ToolsetInfo.CurrentTargetFramework);

            TestProject testProject = new TestProject()
            {
                Name = ProjectName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.AdditionalProperties.Add("RuntimeIdentifier", rid);
            testProject.AdditionalProperties.Add("SelfContained", "false");
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("Libuv", "1.10.0"));

            var testProjectInstance = _testAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework: testProject.TargetFrameworks, runtimeIdentifier: rid);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}{Constants.ExeSuffix}",
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                $"{ProjectName}.runtimeconfig.json",
                "Newtonsoft.Json.dll",
                // NOTE: this may break in the future when the SDK supports platforms that libuv does not
                $"libuv{FileConstants.DynamicLibSuffix}"
            });
        }

        [Fact]
        public void It_does_not_copy_local_package_dependencies_for_lib_projects()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new TestProject()
            {
                Name = ProjectName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = false
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = _testAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
            });
        }

        [Fact]
        public void It_copies_local_package_dependencies_for_lib_projects_when_requested_to()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new TestProject()
            {
                Name = ProjectName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = false
            };

            testProject.AdditionalProperties["CopyLocalLockFileAssemblies"] = "true";
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = _testAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                "Newtonsoft.Json.dll",
                "runtimes/linux-x64/native/libsqlite3.so",
                "runtimes/osx-x64/native/libsqlite3.dylib",
                "runtimes/win7-x64/native/sqlite3.dll",
                "runtimes/win7-x86/native/sqlite3.dll"
            });
        }

        [Fact]
        public void It_does_not_copy_local_package_dependencies_for_netstandard_projects()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new TestProject()
            {
                Name = ProjectName,
                TargetFrameworks = "netstandard2.0"
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = _testAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb"
            });
        }

        [Fact]
        public void It_copies_local_package_dependencies_for_netstandard_projects_when_requested_to()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new TestProject()
            {
                Name = ProjectName,
                TargetFrameworks = "netstandard2.0"
            };

            testProject.AdditionalProperties["CopyLocalLockFileAssemblies"] = "true";
            testProject.AdditionalProperties["CopyLocalRuntimeTargetAssets"] = "true";
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = _testAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                "Newtonsoft.Json.dll",
                "runtimes/linux-x64/native/libsqlite3.so",
                "runtimes/osx-x64/native/libsqlite3.dylib",
                "runtimes/win7-x64/native/sqlite3.dll",
                "runtimes/win7-x86/native/sqlite3.dll"
            });
        }

        [Fact]
        public void It_does_not_copy_local_runtime_dependencies_for_netframework_projects()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            TestProject testProject = new TestProject()
            {
                Name = ProjectName,
                TargetFrameworks = "net46"
            };

            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("sqlite", "3.13.0"));

            var testProjectInstance = _testAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                "Newtonsoft.Json.dll",
            });
        }

        [Fact]
        public void It_copies_local_all_assets_on_self_contained_build()
        {
            const string ProjectName = "TestProjWithPackageDependencies";

            var rid = EnvironmentInfo.GetCompatibleRid(ToolsetInfo.CurrentTargetFramework);

            TestProject testProject = new TestProject()
            {
                Name = ProjectName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                SelfContained = "true"
            };

            testProject.AdditionalProperties.Add("RuntimeIdentifier", rid);
            testProject.PackageReferences.Add(new TestPackageReference("Newtonsoft.Json", ToolsetInfo.GetNewtonsoftJsonPackageVersion()));
            testProject.PackageReferences.Add(new TestPackageReference("Libuv", "1.10.0"));

            var testProjectInstance = _testAssetsManager
               .CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testProjectInstance);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework: testProject.TargetFrameworks, runtimeIdentifier: rid);

            outputDirectory.Should().HaveFiles(new[] {
                $"{ProjectName}{Constants.ExeSuffix}",
                $"{ProjectName}.deps.json",
                $"{ProjectName}.dll",
                $"{ProjectName}.pdb",
                $"{ProjectName}.runtimeconfig.json",
                "Newtonsoft.Json.dll",
                // NOTE: this may break in the future when the SDK supports platforms that libuv does not
                $"libuv{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}clrjit{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostfxr{FileConstants.DynamicLibSuffix}",
                $"{FileConstants.DynamicLibPrefix}hostpolicy{FileConstants.DynamicLibSuffix}",
                $"mscorlib.dll",
                // This is not an exhaustive list as there are many files in self-contained builds
            });

            outputDirectory.Should().NotHaveFiles(new[] {
                $"apphost{Constants.ExeSuffix}",
            });
        }
    }
}
