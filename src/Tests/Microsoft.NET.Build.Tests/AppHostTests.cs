// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.NET.Build.Tests
{
    public class AppHostTests : SdkTest
    {
        private static string[] GetExpectedFilesFromBuild(TestAsset testAsset, string targetFramework)
        {
            var testProjectName = testAsset.TestProject?.Name ?? testAsset.Name;
            var expectedFiles = new List<string>()
            {
                $"{testProjectName}{Constants.ExeSuffix}",
                $"{testProjectName}.dll",
                $"{testProjectName}.pdb",
                $"{testProjectName}.deps.json",
                $"{testProjectName}.runtimeconfig.json"
            };

            if (!string.IsNullOrEmpty(targetFramework))
            {
                var parsedTargetFramework = NuGetFramework.Parse(targetFramework);

                if (parsedTargetFramework.Version.Major < 6)
                    expectedFiles.Add($"{testProjectName}.runtimeconfig.dev.json");
            }

            return expectedFiles.ToArray();
        }

        public AppHostTests(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionTheory("17.1.0.60101")]
        [InlineData("netcoreapp3.1")]
        [InlineData("net5.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_builds_a_runnable_apphost_by_default(string targetFramework)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework)
                // Windows Server requires setting on preview features for
                // global using directives.
                .WithProjectChanges((path, project) =>
                {
                    var ns = project.Root.Name.Namespace;

                    project.Root.Add(
                        new XElement(ns + "PropertyGroup",
                            new XElement(ns + "LangVersion", "preview")));
                });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory();
            var hostExecutable = $"HelloWorld{Constants.ExeSuffix}";
            outputDirectory.Should().OnlyHaveFiles(GetExpectedFilesFromBuild(testAsset, targetFramework));
            new RunExeCommand(Log, Path.Combine(outputDirectory.FullName, hostExecutable))
                .WithEnvironmentVariable(
                    Environment.Is64BitProcess ? "DOTNET_ROOT" : "DOTNET_ROOT(x86)",
                    Path.GetDirectoryName(TestContext.Current.ToolsetUnderTest.DotNetHostPath))
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        [PlatformSpecificTheory(TestPlatforms.OSX)]
        [InlineData("netcoreapp3.1")]
        [InlineData("net5.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_can_disable_codesign_if_opt_out(string targetFramework)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute(new string[] {
                    "/p:_EnableMacOSCodeSign=false;ProduceReferenceAssembly=false",
                })
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);
            var appHostFullPath = Path.Combine(outputDirectory.FullName, "HelloWorld");

            // Check that the apphost was not signed
            var codesignPath = @"/usr/bin/codesign";
            new RunExeCommand(Log, codesignPath, new string[] { "-d", appHostFullPath })
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining($"{appHostFullPath}: code object is not signed at all");

            outputDirectory.Should().OnlyHaveFiles(GetExpectedFilesFromBuild(testAsset, targetFramework));
        }

        [PlatformSpecificTheory(TestPlatforms.OSX)]
        [InlineData("netcoreapp3.1", "win-x64")]
        [InlineData("net5.0", "win-x64")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "win-x64")]
        [InlineData("netcoreapp3.1", "linux-x64")]
        [InlineData("net5.0", "linux-x64")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "linux-x64")]
        public void It_does_not_try_to_codesign_non_osx_app_hosts(string targetFramework, string rid)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework, allowCopyIfPresent: true)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute(new string[] {
                    $"/p:RuntimeIdentifier={rid}",
                })
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: rid);
            var hostExecutable = $"HelloWorld{(rid.StartsWith("win") ? ".exe" : string.Empty)}";
            var appHostFullPath = Path.Combine(outputDirectory.FullName, hostExecutable);

            // Check that the apphost was not signed
            var codesignPath = @"/usr/bin/codesign";
            new RunExeCommand(Log, codesignPath, new string[] { "-d", appHostFullPath })
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining($"{appHostFullPath}: code object is not signed at all");

            var buildProjDir = Path.Combine(outputDirectory.FullName, "../..");
            Directory.Delete(buildProjDir, true);
        }

        [PlatformSpecificTheory(TestPlatforms.OSX)]
        [InlineData("netcoreapp3.1")]
        [InlineData("net5.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void It_codesigns_a_framework_dependent_app(string targetFramework)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);
            var appHostFullPath = Path.Combine(outputDirectory.FullName, "HelloWorld");

            // Check that the apphost is signed
            var codesignPath = @"/usr/bin/codesign";
            new RunExeCommand(Log, codesignPath, new string[] { "-s", "-", appHostFullPath })
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining($"{appHostFullPath}: is already signed");
        }

        [PlatformSpecificTheory(TestPlatforms.OSX)]
        [InlineData("netcoreapp3.1", false)]
        [InlineData("netcoreapp3.1", true)]
        [InlineData("net5.0", false)]
        [InlineData("net5.0", true)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, false)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, true)]
        public void It_codesigns_an_app_targeting_osx(string targetFramework, bool selfContained)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework, allowCopyIfPresent: true)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            var buildArgs = new List<string>() { $"/p:RuntimeIdentifier={RuntimeInformation.RuntimeIdentifier}" };
            if (!selfContained)
                buildArgs.Add("/p:PublishSingleFile=true");

            buildCommand
                .Execute(buildArgs.ToArray())
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: RuntimeInformation.RuntimeIdentifier);
            var appHostFullPath = Path.Combine(outputDirectory.FullName, "HelloWorld");

            // Check that the apphost is signed
            var codesignPath = @"/usr/bin/codesign";
            new RunExeCommand(Log, codesignPath, new string[] { "-s", "-", appHostFullPath })
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining($"{appHostFullPath}: is already signed");
        }

        [Theory]
        [InlineData("netcoreapp2.1")]
        [InlineData("netcoreapp2.2")]
        public void It_does_not_build_with_an_apphost_by_default_before_netcoreapp_3(string targetFramework)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "HelloWorld.dll",
                "HelloWorld.pdb",
                "HelloWorld.deps.json",
                "HelloWorld.runtimeconfig.dev.json",
                "HelloWorld.runtimeconfig.json",
            });
        }

        [WindowsOnlyTheory]
        [InlineData("x86")]
        [InlineData("x64")]
        [InlineData("AnyCPU")]
        [InlineData("")]
        public void It_uses_an_apphost_based_on_platform_target(string target)
        {
            var targetFramework = "netcoreapp3.1";

            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: target)
                .WithTargetFramework(targetFramework)
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute(new string[] {
                    $"/p:PlatformTarget={target}",
                    $"/p:NETCoreSdkRuntimeIdentifier={EnvironmentInfo.GetCompatibleRid(targetFramework)}"
                })
                .Should()
                .Pass();

            var apphostPath = Path.Combine(buildCommand.GetOutputDirectory().FullName, "HelloWorld.exe");
            if (target == "x86")
            {
                IsPE32(apphostPath).Should().BeTrue();
            }
            else if (target == "x64")
            {
                IsPE32(apphostPath).Should().BeFalse();
            }
            else
            {
                IsPE32(apphostPath).Should().Be(!Environment.Is64BitProcess);
            }
        }

        [WindowsOnlyFact]
        public void AppHost_contains_resources_from_the_managed_dll()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var version = "5.6.7.8";
            var testProject = new TestProject()
            {
                Name = "ResourceTest",
                TargetFrameworks = targetFramework,
                RuntimeIdentifier = runtimeIdentifier,
                IsExe = true,
            };
            testProject.AdditionalProperties.Add("AssemblyVersion", version);

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(runtimeIdentifier: runtimeIdentifier);
            outputDirectory.Should().HaveFiles(new[] { testProject.Name + ".exe" });

            string apphostPath = Path.Combine(outputDirectory.FullName, testProject.Name + ".exe");
            var apphostVersion = FileVersionInfo.GetVersionInfo(apphostPath).FileVersion;
            apphostVersion.Should().Be(version);
        }

        [WindowsOnlyFact]
        public void FSharp_app_can_customize_the_apphost()
        {
            var targetFramework = "netcoreapp3.1";
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorldFS")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Element(ns + "TargetFramework").SetValue(targetFramework);
                });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute("/p:CopyLocalLockFileAssemblies=false")
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestApp.deps.json",
                "TestApp.dll",
                "TestApp.exe",
                "TestApp.pdb",
                "TestApp.runtimeconfig.dev.json",
                "TestApp.runtimeconfig.json",
            });
        }

        [Fact]
        public void If_UseAppHost_is_false_it_does_not_try_to_find_an_AppHost()
        {
            var testProject = new TestProject()
            {
                Name = "NoAppHost",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                //  Use "any" as RID so that it will fail to find AppHost
                RuntimeIdentifier = "any",
                IsExe = true,
                SelfContained = "false"
            };
            testProject.AdditionalProperties["UseAppHost"] = "false";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Pass();

        }

        [Fact]
        public void It_retries_on_failure_to_create_apphost()
        {
            var testProject = new TestProject()
            {
                Name = "RetryAppHost",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            // enable generating apphost even on macOS
            testProject.AdditionalProperties.Add("UseApphost", "true");

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Pass();

            var intermediateDirectory = buildCommand.GetIntermediateDirectory().FullName;

            File.SetLastWriteTimeUtc(
                Path.Combine(
                    intermediateDirectory,
                    testProject.Name + ".dll"),
                DateTime.UtcNow.AddSeconds(5));

            var intermediateAppHost = Path.Combine(intermediateDirectory, "apphost" + Constants.ExeSuffix);

            using (var stream = new FileStream(intermediateAppHost, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                const int Retries = 1;

                var result = buildCommand.Execute(
                    "/clp:NoSummary",
                    $"/p:CopyRetryCount={Retries}",
                    "/warnaserror",
                    "/p:CopyRetryDelayMilliseconds=0");

                result
                    .Should()
                    .Fail()
                    .And
                    .HaveStdOutContaining("NETSDK1113");

                Regex.Matches(result.StdOut, "NETSDK1113", RegexOptions.None).Count.Should().Be(Retries);
            }
        }

        private static bool IsPE32(string path)
        {
            using (var reader = new PEReader(File.OpenRead(path)))
            {
                return reader.PEHeaders.PEHeader.Magic == PEMagic.PE32;
            }
        }
    }
}
