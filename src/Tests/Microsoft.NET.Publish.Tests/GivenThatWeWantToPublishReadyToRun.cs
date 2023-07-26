// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using Microsoft.NET.Build.Tasks;
using NuGet.Frameworks;

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
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
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
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_creates_readytorun_images_for_all_assemblies_except_excluded_ones(string targetFramework)
        {
            var projectName = "CrossgenTest2";

            var testProject = CreateTestProjectForR2RTesting(
                targetFramework,
                projectName,
                "ClassLib");

            testProject.AdditionalProperties["PublishReadyToRun"] = "True";
            testProject.SelfContained = "True";
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
                    GetPDBFileName(mainProjectDll, framework, testProject.RuntimeIdentifier),
                    GetPDBFileName(classLibDll, framework, testProject.RuntimeIdentifier),
                });
            }

            DoesImageHaveR2RInfo(mainProjectDll).Should().BeTrue();
            DoesImageHaveR2RInfo(classLibDll).Should().BeFalse();

            publishDirectory.Should().HaveFile("System.Private.CoreLib.dll"); // self-contained
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("netcoreapp3.0")]
        [InlineData("net5.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_creates_readytorun_symbols_when_switch_is_used(string targetFramework)
        {
            TestProjectPublishing_Internal("CrossgenTest3", targetFramework, emitNativeSymbols: true, composite: false, identifier: targetFramework);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("netcoreapp3.0")]
        [InlineData("net5.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_supports_framework_dependent_publishing(string targetFramework)
        {
            TestProjectPublishing_Internal("FrameworkDependent", targetFramework, isSelfContained: false, composite: false, emitNativeSymbols: true, identifier: targetFramework);
        }

        [Theory]
        [InlineData("netcoreapp3.0")]
        [InlineData("net5.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
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
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_can_publish_readytorun_for_library_projects(string targetFramework)
        {
            TestProjectPublishing_Internal("LibraryProject1", targetFramework, isSelfContained: false, composite: false, makeExeProject: false, identifier: targetFramework);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("netcoreapp3.0")]
        [InlineData("net5.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_can_publish_readytorun_for_selfcontained_library_projects(string targetFramework)
        {
            TestProjectPublishing_Internal("LibraryProject2", targetFramework, isSelfContained: true, composite: true, makeExeProject: false, identifier: targetFramework);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("net6.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        void It_can_publish_readytorun_using_crossgen2(string targetFramework)
        {
            // In .NET 5 Crossgen2 supported Linux/Windows x64 only
            if (targetFramework == "net5.0" &&
                (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.OSArchitecture != Architecture.X64))
                return;

            TestProjectPublishing_Internal("Crossgen2TestApp", targetFramework, isSelfContained: true, emitNativeSymbols: true, useCrossgen2: true, composite: false, identifier: targetFramework);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("net6.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        void It_can_publish_readytorun_using_crossgen2_composite_mode(string targetFramework)
        {
            // In .NET 5 Crossgen2 supported Linux/Windows x64 only
            if (targetFramework == "net5.0" &&
                (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.OSArchitecture != Architecture.X64))
                return;

            TestProjectPublishing_Internal("Crossgen2TestApp", targetFramework, isSelfContained: true, emitNativeSymbols: false, useCrossgen2: true, composite: true, identifier: targetFramework);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("net6.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
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
            testProject.SelfContained = "False";

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject, targetFramework);

            var publishCommand = new PublishCommand(Log, Path.Combine(testProjectInstance.Path, testProject.Name));
            publishCommand.Execute().Should().Pass();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "linux-x64", "windows,linux,osx", "X64,Arm64", "_", "_")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "linux-x64", "windows,linux,osx", "X64,Arm64", "composite", "selfcontained")] // Composite in .NET 6.0 is only supported for self-contained builds
        // In .NET 6.0 building targeting Windows on linux or osx doesn't support emitting native symbols.
        [InlineData(ToolsetInfo.CurrentTargetFramework, "win-x64", "windows", "X64,Arm64", "composite", "selfcontained")] // Composite in .NET 6.0 is only supported for self-contained builds
        [InlineData(ToolsetInfo.CurrentTargetFramework, "osx-arm64", "windows,linux,osx", "X64,Arm64", "_", "_")]
        // In .NET 6.0 building targeting Windows on linux or osx doesn't support emitting native symbols.
        [InlineData(ToolsetInfo.CurrentTargetFramework, "win-x86", "windows", "X86,X64,Arm64,Arm", "_", "_")]
        public void It_supports_crossos_arch_compilation(string targetFramework, string runtimeIdentifier, string sdkSupportedOs, string sdkSupportedArch, string composite, string selfcontained)
        {
            var projectName = $"CrossArchOs{targetFramework}{runtimeIdentifier.Replace("-", ".")}{composite}{selfcontained}";
            string sdkOs = "NOTHING";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                sdkOs = "linux";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                sdkOs = "windows";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                sdkOs = "osx";
            }

            Assert.NotEqual("NOTHING", sdkOs); // We should know which OS we are running on
            Log.WriteLine($"sdkOs = {sdkOs}");
            if (!sdkSupportedOs.Contains(sdkOs))
            {
                Log.WriteLine("Running test on OS that doesn't support this cross platform build");
                return;
            }

            string sdkArch = RuntimeInformation.ProcessArchitecture.ToString();
            Log.WriteLine($"sdkArch = {sdkArch}");
            Assert.Contains(sdkArch, new string[] { "Arm", "Arm64", "X64", "X86" }); // Assert that the Architecture in use is a known architecture
            if (!sdkSupportedArch.Split(',').Contains(sdkArch))
            {
                Log.WriteLine("Running test on processor architecture that doesn't support this cross platform build");
                return;
            }

            TestProjectPublishing_Internal(projectName, targetFramework, isSelfContained: selfcontained == "selfcontained", emitNativeSymbols: true, useCrossgen2: true, composite: composite == "composite", identifier: targetFramework, runtimeIdentifier: runtimeIdentifier);
        }

        private enum TargetOSEnum
        {
            Windows,
            Linux,
            OsX
        }

        private static TargetOSEnum GetTargetOS(string runtimeIdentifier)
        {
            if (runtimeIdentifier.Contains("osx"))
            {
                return TargetOSEnum.OsX;
            }
            else if (runtimeIdentifier.Contains("win"))
            {
                return TargetOSEnum.Windows;
            }
            else if (runtimeIdentifier.Contains("linux") ||
                     runtimeIdentifier.Contains("ubuntu") ||
                     runtimeIdentifier.Contains("alpine") ||
                     runtimeIdentifier.Contains("android") ||
                     runtimeIdentifier.Contains("centos") ||
                     runtimeIdentifier.Contains("debian") ||
                     runtimeIdentifier.Contains("fedora") ||
                     runtimeIdentifier.Contains("gentoo") ||
                     runtimeIdentifier.Contains("suse") ||
                     runtimeIdentifier.Contains("rhel") ||
                     runtimeIdentifier.Contains("sles") ||
                     runtimeIdentifier.Contains("tizen"))
            {
                return TargetOSEnum.Linux;
            }

            Assert.True(false, $"{runtimeIdentifier} could not be converted into a known OS type. Adjust the if statement above until this does not happen");
            return TargetOSEnum.Windows;
        }

        private static bool IsTargetOsOsX(string runtimeIdentifier)
        {
            return GetTargetOS(runtimeIdentifier) == TargetOSEnum.OsX;
        }

        private static bool IsTargetOsWindows(string runtimeIdentifier)
        {
            return GetTargetOS(runtimeIdentifier) == TargetOSEnum.Windows;
        }

        private void TestProjectPublishing_Internal(string projectName,
            string targetFramework,
            bool makeExeProject = true,
            bool isSelfContained = true,
            bool emitNativeSymbols = false,
            bool useCrossgen2 = false,
            bool composite = true,
            [CallerMemberName] string callingMethod = "",
            string identifier = null,
            string runtimeIdentifier = null)
        {
            var testProject = CreateTestProjectForR2RTesting(
                targetFramework,
                projectName,
                "ClassLib",
                isExeProject: makeExeProject,
                runtimeIdentifier: runtimeIdentifier);

            testProject.AdditionalProperties["PublishReadyToRun"] = "True";
            testProject.AdditionalProperties["PublishReadyToRunEmitSymbols"] = emitNativeSymbols ? "True" : "False";
            testProject.AdditionalProperties["PublishReadyToRunUseCrossgen2"] = useCrossgen2 ? "True" : "False";
            testProject.AdditionalProperties["PublishReadyToRunComposite"] = composite ? "True" : "False";
            testProject.SelfContained = isSelfContained ? "True" : "False";

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

            NuGetFramework framework = NuGetFramework.Parse(targetFramework);
            if (emitNativeSymbols && (!IsTargetOsOsX(testProject.RuntimeIdentifier) || framework.Version.Major >= 6))
            {
                Log.WriteLine("Checking for symbol files");
                IEnumerable<string> pdbFiles;

                if (composite)
                {
                    pdbFiles = new[] { GetPDBFileName(Path.ChangeExtension(mainProjectDll, "r2r.dll"), framework, testProject.RuntimeIdentifier) };
                }
                else
                {
                    pdbFiles = new[] {
                        GetPDBFileName(mainProjectDll, framework, testProject.RuntimeIdentifier),
                        GetPDBFileName(classLibDll, framework, testProject.RuntimeIdentifier),
                    };
                }

                foreach (string s in pdbFiles)
                {
                    Log.WriteLine($"{publishDirectory.FullName} {s}");
                }

                publishDirectory.Should().HaveFiles(pdbFiles);
            }
        }

        private TestProject CreateTestProjectForR2RTesting(string targetFramework, string mainProjectName, string referenceProjectName, bool isExeProject = true, string runtimeIdentifier = null)
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
                RuntimeIdentifier = runtimeIdentifier ?? EnvironmentInfo.GetCompatibleRid(targetFramework),
                ReferencedProjects = { referenceProject },
                SelfContained = "true"
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

        public static string GetPDBFileName(string assemblyFile, NuGetFramework framework, string runtimeIdentifier)
        {
            if (IsTargetOsWindows(runtimeIdentifier))
            {
                return Path.GetFileName(Path.ChangeExtension(assemblyFile, "ni.pdb"));
            }
            else if (!IsTargetOsOsX(runtimeIdentifier) || framework.Version.Major >= 6)
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
