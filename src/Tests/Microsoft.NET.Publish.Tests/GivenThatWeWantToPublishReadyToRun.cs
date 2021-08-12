using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using NuGet.Frameworks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishReadyToRun : SdkTest
    {
        public GivenThatWeWantToPublishReadyToRun(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("netcoreapp3.0")]
        [InlineData("net5.0")]
        [InlineData("net6.0")]
        public void It_only_runs_readytorun_compiler_when_switch_is_enabled(string targetFramework)
        {
            var projectName = "CrossgenTest1";

            var testProject = CreateTestProjectForR2RTesting(
                targetFramework,
                projectName,
                "ClassLib");

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testProjectInstance);
            publishCommand.Execute().Should().Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework,
                "Debug",
                testProject.RuntimeIdentifier);

            DoesImageHaveR2RInfo(Path.Combine(publishDirectory.FullName, $"{projectName}.dll")).Should().BeFalse();
            DoesImageHaveR2RInfo(Path.Combine(publishDirectory.FullName, "ClassLib.dll")).Should().BeFalse();

            publishDirectory.Should().HaveFile("System.Private.CoreLib.dll"); // self-contained
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("netcoreapp3.0")]
        [InlineData("net5.0")]
        [InlineData("net6.0")]
        public void It_creates_readytorun_images_for_all_assemblies_except_excluded_ones(string targetFramework)
        {
            var projectName = "CrossgenTest2";

            var testProject = CreateTestProjectForR2RTesting(
                targetFramework,
                projectName,
                "ClassLib");

            testProject.AdditionalProperties["PublishReadyToRun"] = "True";
            testProject.AddItem("PublishReadyToRunExclude", "Include", "Classlib.dll");

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testProjectInstance);
            publishCommand.Execute().Should().Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework,
                "Debug",
                testProject.RuntimeIdentifier);

            var mainProjectDll = Path.Combine(publishDirectory.FullName, $"{projectName}.dll");
            var classLibDll = Path.Combine(publishDirectory.FullName, $"ClassLib.dll");

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                NuGetFramework framework = NuGetFramework.Parse(targetFramework);

                publishDirectory.Should().NotHaveFiles(new[] {
                    GetPDBFileName(mainProjectDll, framework),
                    GetPDBFileName(classLibDll, framework),
                });
            }

            DoesImageHaveR2RInfo(mainProjectDll).Should().BeTrue();
            DoesImageHaveR2RInfo(classLibDll).Should().BeFalse();

            publishDirectory.Should().HaveFile("System.Private.CoreLib.dll"); // self-contained
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("netcoreapp3.0")]
        [InlineData("net5.0")]
        [InlineData("net6.0")]
        public void It_creates_readytorun_symbols_when_switch_is_used(string targetFramework)
        {
            TestProjectPublishing_Internal("CrossgenTest3", targetFramework, emitNativeSymbols: true, composite: false, identifier: targetFramework);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("netcoreapp3.0")]
        [InlineData("net5.0")]
        [InlineData("net6.0")]
        public void It_supports_framework_dependent_publishing(string targetFramework)
        {
            TestProjectPublishing_Internal("FrameworkDependent", targetFramework, isSelfContained: false, emitNativeSymbols:true, identifier: targetFramework);
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        [InlineData("net5.0")]
        [InlineData("net6.0")]
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

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testProjectInstance);

            publishCommand.Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContainingIgnoreCase("NETSDK1095");
        }

        [Fact]
        public void It_warns_when_targetting_netcoreapp_2_x_readytorun()
        {
            var testProject = new TestProject()
            {
                Name = "ConsoleApp",
                TargetFrameworks = "netcoreapp2.2",
                IsExe = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute($"/p:PublishReadyToRun=true",
                                   $"/p:RuntimeIdentifier={RuntimeInformation.RuntimeIdentifier}")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(Strings.PublishReadyToRunRequiresVersion30);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("netcoreapp3.0")]
        [InlineData("net5.0")]
        [InlineData("net6.0")]
        public void It_can_publish_readytorun_for_library_projects(string targetFramework)
        {
            TestProjectPublishing_Internal("LibraryProject1", targetFramework, isSelfContained: false, makeExeProject: false, identifier: targetFramework);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("netcoreapp3.0")]
        [InlineData("net5.0")]
        [InlineData("net6.0")]
        public void It_can_publish_readytorun_for_selfcontained_library_projects(string targetFramework)
        {
            TestProjectPublishing_Internal("LibraryProject2", targetFramework, isSelfContained:true, makeExeProject: false, identifier: targetFramework);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("net5.0")]
        [InlineData("net6.0")]
        void It_can_publish_readytorun_using_crossgen2(string targetFramework)
        {
            // In .NET 5 Crossgen2 supported Linux/Windows x64 only
            if (targetFramework == "net5.0" &&
                (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.OSArchitecture != Architecture.X64))
                return;

            TestProjectPublishing_Internal("Crossgen2TestApp", targetFramework, isSelfContained: true, emitNativeSymbols: true, useCrossgen2: true, composite: false, identifier: targetFramework);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("net5.0")]
        [InlineData("net6.0")]
        void It_can_publish_readytorun_using_crossgen2_composite_mode(string targetFramework)
        {
            // In .NET 5 Crossgen2 supported Linux/Windows x64 only
            if (targetFramework == "net5.0" &&
                (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.OSArchitecture != Architecture.X64))
                return;

            TestProjectPublishing_Internal("Crossgen2TestApp", targetFramework, isSelfContained: true, emitNativeSymbols: false, useCrossgen2: true, composite: true, identifier: targetFramework);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("net5.0")]
        [InlineData("net6.0")]
        public void It_supports_libraries_when_using_crossgen2(string targetFramework)
        {
            // In .NET 5 Crossgen2 supported Linux/Windows x64 only
            if (targetFramework == "net5.0" &&
                (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.OSArchitecture != Architecture.X64))
                return;

            var projectName = "FrameworkDependentUsingCrossgen2";

            var testProject = CreateTestProjectForR2RTesting(
                targetFramework,
                projectName,
                "ClassLib");

            testProject.AdditionalProperties["PublishReadyToRun"] = "True";
            testProject.AdditionalProperties["PublishReadyToRunUseCrossgen2"] = "True";
            testProject.AdditionalProperties["SelfContained"] = "False";

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject, targetFramework);

            var publishCommand = new PublishCommand(Log, Path.Combine(testProjectInstance.Path, testProject.Name));
            publishCommand.Execute().Should().Pass();
        }

        private void TestProjectPublishing_Internal(string projectName,
            string targetFramework,
            bool makeExeProject = true,
            bool isSelfContained = true,
            bool emitNativeSymbols = false,
            bool useCrossgen2 = false,
            bool composite = true,
            [CallerMemberName] string callingMethod = "",
            string identifier = null)
        {
            var testProject = CreateTestProjectForR2RTesting(
                targetFramework,
                projectName,
                "ClassLib",
                isExeProject: makeExeProject);

            testProject.AdditionalProperties["PublishReadyToRun"] = "True";
            testProject.AdditionalProperties["PublishReadyToRunEmitSymbols"] = emitNativeSymbols ? "True" : "False";
            testProject.AdditionalProperties["PublishReadyToRunUseCrossgen2"] = useCrossgen2 ? "True" : "False";
            testProject.AdditionalProperties["PublishReadyToRunComposite"] = composite ? "True" : "False";
            testProject.AdditionalProperties["SelfContained"] = isSelfContained ? "True" : "False";

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject, callingMethod, identifier);

            var publishCommand = new PublishCommand(testProjectInstance);
            publishCommand.Execute().Should().Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework,
                "Debug",
                testProject.RuntimeIdentifier);

            var mainProjectDll = Path.Combine(publishDirectory.FullName, $"{projectName}.dll");
            var classLibDll = Path.Combine(publishDirectory.FullName, $"ClassLib.dll");

            DoesImageHaveR2RInfo(mainProjectDll).Should().BeTrue();
            DoesImageHaveR2RInfo(classLibDll).Should().BeTrue();

            if (isSelfContained)
                publishDirectory.Should().HaveFile("System.Private.CoreLib.dll");
            else
                publishDirectory.Should().NotHaveFile("System.Private.CoreLib.dll");

            if (emitNativeSymbols && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                NuGetFramework framework = NuGetFramework.Parse(targetFramework);

                publishDirectory.Should().HaveFiles(new[] {
                    GetPDBFileName(mainProjectDll, framework),
                    GetPDBFileName(classLibDll, framework),
                });
            }
        }

        private TestProject CreateTestProjectForR2RTesting(string targetFramework, string mainProjectName, string referenceProjectName, bool isExeProject = true)
        {
            var referenceProject = new TestProject()
            {
                Name = referenceProjectName,
                TargetFrameworks = targetFramework,
            };
            referenceProject.SourceFiles[$"{referenceProjectName}.cs"] = @"
using System;
public class Classlib
{
    public string Func()
    {
        return ""Hello from a netcoreapp3.0.!"";
    }
}";

            var testProject = new TestProject()
            {
                Name = mainProjectName,
                TargetFrameworks = targetFramework,
                IsExe = isExeProject,
                RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework),
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

        public static string GetPDBFileName(string assemblyFile, NuGetFramework framework)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.GetFileName(Path.ChangeExtension(assemblyFile, "ni.pdb"));
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (framework.Version.Major >= 6)
                {
                    return Path.GetFileName(Path.ChangeExtension(assemblyFile, "ni.r2rmap"));
                }

                // Legacy perfmap file naming prior to .NET 6
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

        public static bool DoesImageHaveR2RInfo(string path)
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
