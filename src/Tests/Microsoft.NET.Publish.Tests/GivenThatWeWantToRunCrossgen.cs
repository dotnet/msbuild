using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToRunCrossgen : SdkTest
    {
        public GivenThatWeWantToRunCrossgen(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void It_only_runs_readytorun_compiler_when_switch_is_enabled(string targetFramework)
        {
            var projectName = "CrossgenTest1";

            var testProject = CreateTestProjectForR2RTesting(
                EnvironmentInfo.GetCompatibleRid(targetFramework),
                projectName,
                "ClassLib");

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var publishCommand = new PublishCommand(Log, Path.Combine(testProjectInstance.Path, testProject.Name));
            publishCommand.Execute("/v:n").Should().Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework,
                "Debug",
                EnvironmentInfo.GetCompatibleRid(targetFramework));

            DoesImageHaveR2RInfo(Path.Combine(publishDirectory.FullName, $"{projectName}.dll")).ToString().Should().Be(false.ToString());
            DoesImageHaveR2RInfo(Path.Combine(publishDirectory.FullName, "ClassLib.dll")).ToString().Should().Be(false.ToString());
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void It_creates_readytorun_images_for_all_assemblies_except_excluded_ones(string targetFramework)
        {
            var projectName = "CrossgenTest2";

            var testProject = CreateTestProjectForR2RTesting(
                EnvironmentInfo.GetCompatibleRid(targetFramework),
                projectName, 
                "ClassLib");

            testProject.AdditionalProperties["PublishReadyToRun"] = "True";
            testProject.AdditionalItems["PublishReadyToRunExclude"] = "Classlib.dll";

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var publishCommand = new PublishCommand(Log, Path.Combine(testProjectInstance.Path, testProject.Name));
            publishCommand.Execute("/v:n").Should().Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework, 
                "Debug", 
                EnvironmentInfo.GetCompatibleRid(targetFramework));

            var mainProjectDll = Path.Combine(publishDirectory.FullName, $"{projectName}.dll");
            var classLibDll = Path.Combine(publishDirectory.FullName, $"ClassLib.dll");

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                publishDirectory.Should().NotHaveFiles(new[] {
                    GetPDBFileName(mainProjectDll),
                    GetPDBFileName(classLibDll),
                });
            }

            DoesImageHaveR2RInfo(mainProjectDll).ToString().Should().Be(true.ToString());
            DoesImageHaveR2RInfo(classLibDll).ToString().Should().Be(false.ToString());
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void It_creates_readytorun_symbols_when_switch_is_used(string targetFramework)
        {
            var projectName = "CrossgenTest3";

            var testProject = CreateTestProjectForR2RTesting(
                EnvironmentInfo.GetCompatibleRid(targetFramework),
                projectName, 
                "ClassLib");

            testProject.AdditionalProperties["PublishReadyToRun"] = "True";
            testProject.AdditionalProperties["PublishReadyToRunEmitSymbols"] = "True";

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var publishCommand = new PublishCommand(Log, Path.Combine(testProjectInstance.Path, testProject.Name));
            publishCommand.Execute("/v:n").Should().Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework, 
                "Debug",
                EnvironmentInfo.GetCompatibleRid(targetFramework));

            var mainProjectDll = Path.Combine(publishDirectory.FullName, $"{projectName}.dll");
            var classLibDll = Path.Combine(publishDirectory.FullName, $"ClassLib.dll");

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                publishDirectory.Should().HaveFiles(new[] {
                    GetPDBFileName(mainProjectDll),
                    GetPDBFileName(classLibDll),
                });
            }

            DoesImageHaveR2RInfo(mainProjectDll).ToString().Should().Be(true.ToString());
            DoesImageHaveR2RInfo(classLibDll).ToString().Should().Be(true.ToString());
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void It_does_not_support_framework_dependent_publishing(string targetFramework)
        {
            var projectName = "FrameworkDependent";

            var testProject = CreateTestProjectForR2RTesting(
                EnvironmentInfo.GetCompatibleRid(targetFramework),
                projectName,
                "ClassLib");

            testProject.AdditionalProperties["PublishReadyToRun"] = "True";

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            // TODO: This test should be changed to expect publishing to succeed when fixing #3109 and #3110.
            // When fixing the issues, change the function name to reflect what we're testing, and change the test's expected 
            // behavior (check that the output assemblies exist and are R2R images).
            var publishCommand = new PublishCommand(Log, Path.Combine(testProjectInstance.Path, testProject.Name));
            publishCommand.Execute("/p:SelfContained=false", "/v:n")
                .Should()
                .Fail()
                .And.HaveStdOutContainingIgnoreCase("NETSDK1095");
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        public void It_does_not_support_cross_platform_readytorun_compilation(string targetFramework)
        {
            var ridToUse = EnvironmentInfo.GetCompatibleRid(targetFramework);
            int separator = ridToUse.LastIndexOf('-');

            string platform = ridToUse.Substring(0, separator).ToLowerInvariant();
            string architectureStr = ridToUse.Substring(separator + 1).ToLowerInvariant();

            var testProject = new TestProject()
            {
                Name = "FailingToCrossgen",
                TargetFrameworks = "netcoreapp3.0",
                IsExe = true,
                IsSdkProject = true,
            };

            if (platform.Contains("win"))
            {
                testProject.RuntimeIdentifier = $"linux-{architectureStr}";
            }
            else if ((platform.Contains("linux") || platform.Contains("freebsd") || platform.Contains("ubuntu")) && !platform.Contains("linux-musl"))
            {
                testProject.RuntimeIdentifier = $"linux-musl-{architectureStr}";
            }
            else if (platform.Contains("linux-musl") || platform.Contains("alpine"))
            {
                testProject.RuntimeIdentifier = $"linux-{architectureStr}";
            }
            else if (platform.Contains("osx"))
            {
                testProject.RuntimeIdentifier = $"win-{architectureStr}";
            }
            else if (platform.Contains("rhel.6"))
            {
                testProject.RuntimeIdentifier = $"linux-{architectureStr}";
            }
            else
            {
                return;
            }

            testProject.AdditionalProperties["PublishReadyToRun"] = "True";

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject)
                .Restore(Log, testProject.Name);

            var publishCommand = new PublishCommand(Log, Path.Combine(testProjectInstance.Path, testProject.Name));

            publishCommand.Execute("/v:n")
                .Should()
                .Fail()
                .And.HaveStdOutContainingIgnoreCase("NETSDK1095");
        }

        private TestProject CreateTestProjectForR2RTesting(string ridToUse, string mainProjectName, string referenceProjectName)
        {
            var referenceProject = new TestProject()
            {
                Name = referenceProjectName,
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
            };
            referenceProject.SourceFiles[$"{referenceProjectName}.cs"] = @"
using System;
public class Classlib
{
    public string Func()
    {
        return ""Hello from a netcoreapp2.0.!"";
    }
}";

            var testProject = new TestProject()
            {
                Name = mainProjectName,
                TargetFrameworks = "netcoreapp3.0",
                IsExe = true,
                IsSdkProject = true,
                RuntimeIdentifier = ridToUse,
                ReferencedProjects = { referenceProject },
            };
            testProject.SourceFiles[$"{mainProjectName}.cs"] = @"
using System;
public class Program
{
    public static void Main()
    {
        Console.WriteLine(new Classlib().Func());
    }
}";

            return testProject;
        }

        private string GetPDBFileName(string assemblyFile)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.GetFileName(Path.ChangeExtension(assemblyFile, "ni.pdb"));
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                using (FileStream fs = new FileStream(assemblyFile, FileMode.Open, FileAccess.Read))
                {
                    PEReader pereader = new PEReader(fs);
                    MetadataReader mdReader = pereader.GetMetadataReader();
                    Guid mvid = mdReader.GetGuid(mdReader.GetModuleDefinition().Mvid);

                    return Path.GetFileName(Path.ChangeExtension(assemblyFile, "ni.{" + mvid + "}.map"));
                }
            }

            return null;
        }

        private bool DoesImageHaveR2RInfo(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (var pereader = new PEReader(fs))
                {
                    return (pereader.PEHeaders.CorHeader.Flags & CorFlags.ILOnly) != CorFlags.ILOnly;
                }
            }
        }
    }
}
