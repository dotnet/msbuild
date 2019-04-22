using System;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using FluentAssertions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToRunILLink : SdkTest
    {
        public GivenThatWeWantToRunILLink(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void ILLink_only_runs_when_switch_is_enabled(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name, args: $"/p:RuntimeIdentifier={rid}");

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", $"/p:SelfContained=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            Directory.Exists(linkedDirectory).Should().BeFalse();

            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");
            var unusedFrameworkDll = Path.Combine(publishDirectory, $"{unusedFrameworkAssembly}.dll");

            // Linker inputs are kept, including unused assemblies
            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(unusedDll).Should().BeTrue();
            File.Exists(unusedFrameworkDll).Should().BeTrue();

            var depsFile = Path.Combine(publishDirectory, $"{projectName}.deps.json");
            DoesDepsFileHaveAssembly(depsFile, referenceProjectName).Should().BeTrue();
            DoesDepsFileHaveAssembly(depsFile, unusedFrameworkAssembly).Should().BeTrue();
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void ILLink_runs_and_creates_linked_app(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name, args: $"/p:RuntimeIdentifier={rid}");

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", $"/p:SelfContained=true", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            Directory.Exists(linkedDirectory).Should().BeTrue();

            var linkedDll = Path.Combine(linkedDirectory, $"{projectName}.dll");
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");
            var unusedFrameworkDll = Path.Combine(publishDirectory, $"{unusedFrameworkAssembly}.dll");

            // Intermediate assembly is kept by linker and published, but not unused assemblies
            File.Exists(linkedDll).Should().BeTrue();
            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(unusedDll).Should().BeFalse();
            File.Exists(unusedFrameworkDll).Should().BeFalse();

            var depsFile = Path.Combine(publishDirectory, $"{projectName}.deps.json");
            DoesDepsFileHaveAssembly(depsFile, projectName).Should().BeTrue();
            DoesDepsFileHaveAssembly(depsFile, referenceProjectName).Should().BeFalse();
            DoesDepsFileHaveAssembly(depsFile, unusedFrameworkAssembly).Should().BeFalse();
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void ILLink_accepts_root_descriptor(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name, args: $"/p:RuntimeIdentifier={rid}");

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", $"/p:SelfContained=true", "/p:PublishTrimmed=true",
                                   $"/p:TrimmerRootDescriptors={referenceProjectName}.xml").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            // With root descriptor, linker keeps specified roots but removes unused methods
            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(unusedDll).Should().BeTrue();
            DoesImageHaveMethod(unusedDll, "UnusedMethod").Should().BeFalse();
            DoesImageHaveMethod(unusedDll, "UnusedMethodToRoot").Should().BeTrue();
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void ILLink_runs_incrementally(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name, args: $"/p:RuntimeIdentifier={rid}");

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            var linkSemaphore = Path.Combine(intermediateDirectory, "Link.semaphore");

            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", $"/p:SelfContained=true", "/p:PublishTrimmed=true").Should().Pass();
            DateTime semaphoreFirstModifiedTime = File.GetLastWriteTimeUtc(linkSemaphore);

            WaitForUtcNowToAdvance();

            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", $"/p:SelfContained=true", "/p:PublishTrimmed=true").Should().Pass();
            DateTime semaphoreSecondModifiedTime = File.GetLastWriteTimeUtc(linkSemaphore);

            semaphoreFirstModifiedTime.Should().Be(semaphoreSecondModifiedTime);
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void ILLink_does_not_include_leftover_artifacts_on_second_run(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name, args: $"/p:RuntimeIdentifier={rid}");

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            var linkSemaphore = Path.Combine(intermediateDirectory, "Link.semaphore");

            // Link, keeping classlib
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", $"/p:SelfContained=true", "/p:PublishTrimmed=true",
                                   $"/p:TrimmerRootDescriptors={referenceProjectName}.xml").Should().Pass();
            var publishedDllKeptFirstTimeOnly = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");
            var linkedDllKeptFirstTimeOnly = Path.Combine(linkedDirectory, $"{referenceProjectName}.dll");
            File.Exists(linkedDllKeptFirstTimeOnly).Should().BeTrue();
            File.Exists(publishedDllKeptFirstTimeOnly).Should().BeTrue();

            // Delete kept dll from publish output (works around lack of incremental publish)
            File.Delete(publishedDllKeptFirstTimeOnly);

            // Modify input timestamp to force a re-build and re-link
            WaitForUtcNowToAdvance();
            File.SetLastWriteTimeUtc(Path.Combine(testAsset.TestRoot, testProject.Name, $"{projectName}.cs"), DateTime.UtcNow);

            // Link, discarding classlib
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", $"/p:SelfContained=true", "/p:PublishTrimmed=true").Should().Pass();
            File.Exists(linkedDllKeptFirstTimeOnly).Should().BeFalse();
            File.Exists(publishedDllKeptFirstTimeOnly).Should().BeFalse();

            // "linked" intermediate directory does not pollute the publish output
            Directory.Exists(Path.Combine(publishDirectory, "linked")).Should().BeFalse();
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void ILLink_runs_on_portable_app(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand.Execute("/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            Directory.Exists(linkedDirectory).Should().BeTrue();

            var linkedDll = Path.Combine(linkedDirectory, $"{projectName}.dll");
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            File.Exists(linkedDll).Should().BeTrue();
            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(unusedDll).Should().BeFalse();

            var depsFile = Path.Combine(publishDirectory, $"{projectName}.deps.json");
            DoesDepsFileHaveAssembly(depsFile, projectName).Should().BeTrue();
            DoesDepsFileHaveAssembly(depsFile, referenceProjectName).Should().BeFalse();
        }

        private static bool DoesImageHaveMethod(string path, string methodNameToCheck)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var peReader = new PEReader(fs))
            {
                var metadataReader = peReader.GetMetadataReader();
                foreach (var handle in metadataReader.MethodDefinitions)
                {
                    var methodDefinition = metadataReader.GetMethodDefinition(handle);
                    string methodName = metadataReader.GetString(methodDefinition.Name);
                    if (methodName == methodNameToCheck)
                        return true;
                }
            }
            return false;
        }

        private static bool DoesDepsFileHaveAssembly(string depsFilePath, string assemblyName)
        {
            DependencyContext dependencyContext;
            using (var fs = File.OpenRead(depsFilePath))
            {
                dependencyContext = new DependencyContextJsonReader().Read(fs);
            }

            return dependencyContext.RuntimeLibraries.Any(l =>
                l.RuntimeAssemblyGroups.Any(rag =>
                    rag.AssetPaths.Any(f =>
                        Path.GetFileName(f) == $"{assemblyName}.dll")));
        }

        static string unusedFrameworkAssembly = "System.IO";

        private TestPackageReference GetPackageReference(TestProject project)
        {
            var asset = _testAssetsManager.CreateTestProject(project, project.Name).Restore(Log, project.Name);
            var pack = new PackCommand(Log, Path.Combine(asset.TestRoot, project.Name));
            pack.Execute().Should().Pass();

            return new TestPackageReference(project.Name, "1.0.0", pack.GetNuGetPackage(project.Name));
        }

        private TestProject CreateTestProjectForILLinkTesting(string targetFramework, string mainProjectName, string referenceProjectName)
        {
            var referenceProject = new TestProject()
            {
                Name = referenceProjectName,
                TargetFrameworks = targetFramework,
                IsSdkProject = true
            };
            referenceProject.SourceFiles[$"{referenceProjectName}.cs"] = @"
using System;
public class ClassLib
{
    public void UnusedMethod()
    {
    }

    public void UnusedMethodToRoot()
    {
    }
}
";

            var packageReference = GetPackageReference(referenceProject);

            var testProject = new TestProject()
            {
                Name = mainProjectName,
                TargetFrameworks = targetFramework,
                IsSdkProject = true,
                PackageReferences = { packageReference }
            };

            testProject.AdditionalProperties.Add("RestoreAdditionalProjectSources", Path.GetDirectoryName(packageReference.NupkgPath));

            testProject.SourceFiles[$"{mainProjectName}.cs"] = @"
using System;
public class Program
{
    public static void Main()
    {
        Console.WriteLine(""Hello world"");
    }
}
";

            testProject.SourceFiles[$"{referenceProjectName}.xml"] = $@"
<linker>
  <assembly fullname=""{referenceProjectName}"">
    <type fullname=""ClassLib"">
      <method name=""UnusedMethodToRoot"" />
    </type>
  </assembly>
</linker>
";

            return testProject;
        }
    }
}
