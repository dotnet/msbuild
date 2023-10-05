// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Build.Tasks;
using NuGet.Frameworks;
using static Microsoft.NET.Publish.Tests.PublishTestUtils;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishASingleFileApp : SdkTest
    {
        private const string TestProjectName = "HelloWorldWithSubDirs";

        private const string PublishSingleFile = "/p:PublishSingleFile=true";
        private const string FrameworkDependent = "/p:SelfContained=false";
        private const string PlaceStamp = "/p:PlaceStamp=true";
        private const string ExcludeNewest = "/p:ExcludeNewest=true";
        private const string ExcludeAlways = "/p:ExcludeAlways=true";
        private const string DontUseAppHost = "/p:UseAppHost=false";
        private const string ReadyToRun = "/p:PublishReadyToRun=true";
        private const string ReadyToRunCompositeOn = "/p:PublishReadyToRunComposite=true";
        private const string ReadyToRunCompositeOff = "/p:PublishReadyToRunComposite=false";
        private const string ReadyToRunWithSymbols = "/p:PublishReadyToRunEmitSymbols=true";
        private const string UseAppHost = "/p:UseAppHost=true";
        private const string IncludeDefault = "/p:IncludeSymbolsInSingleFile=false";
        private const string IncludePdb = "/p:IncludeSymbolsInSingleFile=true";
        private const string IncludeNative = "/p:IncludeNativeLibrariesForSelfExtract=true";
        private const string DontIncludeNative = "/p:IncludeNativeLibrariesForSelfExtract=false";
        private const string IncludeAllContent = "/p:IncludeAllContentForSelfExtract=true";

        private readonly string RuntimeIdentifier = $"/p:RuntimeIdentifier={RuntimeInformation.RuntimeIdentifier}";
        private readonly string SingleFile = $"{TestProjectName}{Constants.ExeSuffix}";
        private readonly string PdbFile = $"{TestProjectName}.pdb";
        private const string NewestContent = "Signature.Newest.Stamp";
        private const string AlwaysContent = "Signature.Always.Stamp";

        private const string SmallNameDir = "SmallNameDir";
        private const string LargeNameDir = "This is a directory with a really long name for one that only contains a small file";
        private readonly string SmallNameDirWord = Path.Combine(SmallNameDir, "word").Replace('\\', '/'); // DirectoryInfoAssertions normalizes Path-Separator.
        private readonly string LargeNameDirWord = Path.Combine(SmallNameDir, LargeNameDir, ".word").Replace('\\', '/');

        public GivenThatWeWantToPublishASingleFileApp(ITestOutputHelper log) : base(log)
        {
        }

        private PublishCommand GetPublishCommand(string identifier = null, [CallerMemberName] string callingMethod = "", Action<XDocument> projectChanges = null)
        {
            if (projectChanges == null)
            {
                projectChanges = d => { };
            }

            var testAsset = _testAssetsManager
               .CopyTestAsset(TestProjectName, callingMethod, identifier)
               .WithSource()
               .WithProjectChanges(projectChanges);

            // Create the following content:
            //  <TestRoot>/SmallNameDir/This is a directory with a really long name for one that only contains a small file/.word
            //
            // This content is not checked in to the test assets, but generated during test execution
            // in order to circumvent certain issues like:
            // Git Clone: Cannot clone files with long names on Windows if long file name support is not enabled
            // Nuget Pack: By default ignores files starting with "."
            string longDirPath = Path.Combine(testAsset.TestRoot, SmallNameDir, LargeNameDir);
            Directory.CreateDirectory(longDirPath);
            using (var writer = File.CreateText(Path.Combine(longDirPath, ".word")))
            {
                writer.Write("World!");
            }

            return new PublishCommand(testAsset);
        }

        private string GetNativeDll(string baseName)
        {
            return RuntimeInformation.RuntimeIdentifier.StartsWith("win") ? baseName + ".dll" :
                   RuntimeInformation.RuntimeIdentifier.StartsWith("osx") ? "lib" + baseName + ".dylib" : "lib" + baseName + ".so";
        }

        private DirectoryInfo GetPublishDirectory(PublishCommand publishCommand, string targetFramework = ToolsetInfo.CurrentTargetFramework)
        {
            return publishCommand.GetOutputDirectory(targetFramework: targetFramework,
                                                     runtimeIdentifier: RuntimeInformation.RuntimeIdentifier);
        }

        [Fact]
        public void Incremental_add_single_file()
        {
            var testProject = new TestProject()
            {
                Name = "SingleFileTest",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };
            testProject.AdditionalProperties.Add("SelfContained", $"{true}");

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var cmd = new PublishCommand(testAsset);

            var singleFilePath = Path.Combine(GetPublishDirectory(cmd).FullName, $"SingleFileTest{Constants.ExeSuffix}");
            cmd.Execute(RuntimeIdentifier).Should().Pass();
            var time1 = File.GetLastWriteTimeUtc(singleFilePath);

            WaitForUtcNowToAdvance();

            cmd.Execute(PublishSingleFile, RuntimeIdentifier).Should().Pass();
            var time2 = File.GetLastWriteTimeUtc(singleFilePath);

            time2.Should().BeAfter(time1);

            var exeCommand = new RunExeCommand(Log, singleFilePath);
            exeCommand.Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void It_errors_when_publishing_single_file_without_apphost()
        {
            GetPublishCommand()
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent, DontUseAppHost)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotHaveSingleFileWithoutAppHost);
        }

        [Fact]
        public void It_generates_publishing_single_file_with_win7()
        {
            const string rid = "win7-x86";

            //  Retarget project to net7.0, as net8.0 and up by default use portable runtime graph which doesn't have win7-* RIDs
            var projectChanges = (XDocument doc) =>
            {
                var ns = doc.Root.Name.Namespace;
                doc.Root.Element(ns + "PropertyGroup")
                    .Element(ns + "TargetFramework")
                    .Value = "net7.0";
            };

            GetPublishCommand(projectChanges: projectChanges)
                .Execute($"/p:RuntimeIdentifier={rid}", PublishSingleFile)
                .Should()
                .Pass();
        }

        [Fact]
        public void It_errors_when_publishing_single_file_lib()
        {
            var testProject = new TestProject()
            {
                Name = "ClassLib",
                TargetFrameworks = "netstandard2.0",
                IsExe = false,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute(PublishSingleFile, RuntimeIdentifier)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotHaveSingleFileWithoutExecutable)
                .And
                .NotHaveStdOutContaining(Strings.CanOnlyHaveSingleFileWithNetCoreApp);
        }

        [Fact]
        public void It_errors_when_targetting_netstandard()
        {
            var testProject = new TestProject()
            {
                Name = "NetStandardExe",
                TargetFrameworks = "netstandard2.0",
                IsExe = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute(PublishSingleFile, RuntimeIdentifier, UseAppHost)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CanOnlyHaveSingleFileWithNetCoreApp)
                .And
                .NotHaveStdOutContaining(Strings.CannotHaveSingleFileWithoutExecutable);
        }

        [Fact]
        public void It_errors_when_targetting_netcoreapp_2_x()
        {
            var testProject = new TestProject()
            {
                Name = "ConsoleApp",
                TargetFrameworks = "netcoreapp2.2",
                IsExe = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute(PublishSingleFile, RuntimeIdentifier)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.PublishSingleFileRequiresVersion30);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_errors_when_including_all_content_but_not_native_libraries()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludeAllContent, DontIncludeNative)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotIncludeAllContentButNotNativeLibrariesInSingleFile);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_generates_a_single_file_for_framework_dependent_apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile, SmallNameDirWord, LargeNameDirWord };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_generates_a_single_file_for_self_contained_apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile, SmallNameDirWord, LargeNameDirWord };
            string[] unexpectedFiles = { GetNativeDll("hostfxr"), GetNativeDll("hostpolicy") };

            GetPublishDirectory(publishCommand)
                .Should()
                .HaveFiles(expectedFiles)
                .And
                .NotHaveFiles(unexpectedFiles);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void No_runtime_files()
        {
            var testProject = new TestProject()
            {
                Name = "SingleFileTest",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(testAsset);

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier)
                .Should()
                .Pass();

            string[] expectedFiles = { $"{testProject.Name}{Constants.ExeSuffix}", $"{testProject.Name}.pdb" };
            GetPublishDirectory(publishCommand, ToolsetInfo.CurrentTargetFramework)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }


        [RequiresMSBuildVersionTheory("17.0.0.32901", Skip = "https://github.com/dotnet/runtime/issues/60308")]
        [InlineData(true)]
        [InlineData(false)]
        public void It_supports_composite_r2r(bool extractAll)
        {
            var projName = "SingleFileTest";
            if (extractAll)
            {
                projName += "Extracted";
            }

            var testProject = new TestProject()
            {
                Name = projName,
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(testAsset);
            var extraArgs = new List<string>() { PublishSingleFile, ReadyToRun, ReadyToRunCompositeOn, RuntimeIdentifier };

            if (extractAll)
            {
                extraArgs.Add(IncludeAllContent);
            }

            publishCommand
                .Execute(extraArgs.ToArray())
                .Should()
                .Pass();

            var publishDir = GetPublishDirectory(publishCommand, targetFramework: ToolsetInfo.CurrentTargetFramework).FullName;
            var singleFilePath = Path.Combine(publishDir, $"{testProject.Name}{Constants.ExeSuffix}");

            var command = new RunExeCommand(Log, singleFilePath);
            command.Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World");
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_generates_a_single_file_with_native_binaries_for_framework_dependent_apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent, IncludeNative)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile, SmallNameDirWord, LargeNameDirWord };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_generates_a_single_file_with_native_binaries_for_self_contained_apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludeNative)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile, SmallNameDirWord, LargeNameDirWord };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_generates_a_single_file_with_all_content_for_framework_dependent_apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent, IncludeAllContent)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_generates_a_single_file_with_all_content_for_self_contained_apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludeAllContent)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionTheory("16.8.0")]
        [InlineData("netcoreapp3.0")]
        [InlineData("netcoreapp3.1")]
        public void It_generates_a_single_file_including_pdbs(string targetFramework)
        {
            var testProject = new TestProject()
            {
                Name = "SingleFileTest",
                TargetFrameworks = targetFramework,
                IsExe = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);
            var publishCommand = new PublishCommand(testAsset);

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludeAllContent, IncludePdb)
                .Should()
                .Pass();

            string[] expectedFiles = { $"{testProject.Name}{Constants.ExeSuffix}" };
            GetPublishDirectory(publishCommand, targetFramework)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_excludes_ni_pdbs_from_single_file()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // R2R doesn't produce ni pdbs on OSX.
                return;
            }

            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludeAllContent, ReadyToRun, ReadyToRunWithSymbols, ReadyToRunCompositeOff)
                .Should()
                .Pass();

            string targetFramework = ToolsetInfo.CurrentTargetFramework;
            NuGetFramework framework = NuGetFramework.Parse(targetFramework);

            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework, runtimeIdentifier: RuntimeInformation.RuntimeIdentifier);
            var mainProjectDll = Path.Combine(intermediateDirectory.FullName, $"{TestProjectName}.dll");
            var niPdbFile = GivenThatWeWantToPublishReadyToRun.GetPDBFileName(mainProjectDll, framework, RuntimeInformation.RuntimeIdentifier);

            string[] expectedFiles = { SingleFile, PdbFile, niPdbFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionTheory("16.8.0")]
        [InlineData("netcoreapp3.0")]
        [InlineData("netcoreapp3.1")]
        public void It_can_include_ni_pdbs_in_single_file(string targetFramework)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // R2R doesn't produce ni pdbs on OSX.
                return;
            }

            var testProject = new TestProject()
            {
                Name = "SingleFileTest",
                TargetFrameworks = targetFramework,
                IsExe = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);
            var publishCommand = new PublishCommand(testAsset);

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, ReadyToRun, ReadyToRunWithSymbols, IncludeAllContent, IncludePdb)
                .Should()
                .Pass();

            string[] expectedFiles = { $"{testProject.Name}{Constants.ExeSuffix}" };
            GetPublishDirectory(publishCommand, targetFramework)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionTheory("16.8.0")]
        [InlineData(ExcludeNewest, NewestContent)]
        [InlineData(ExcludeAlways, AlwaysContent)]
        public void It_generates_a_single_file_excluding_content(string exclusion, string content)
        {
            var publishCommand = GetPublishCommand(exclusion);
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludeAllContent, PlaceStamp, exclusion)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile, content };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_generates_a_single_file_for_R2R_compiled_Apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludeAllContent, ReadyToRun, ReadyToRunCompositeOff)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_does_not_rewrite_the_single_file_unnecessarily()
        {
            var publishCommand = GetPublishCommand();
            var singleFilePath = Path.Combine(GetPublishDirectory(publishCommand).FullName, SingleFile);

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            DateTime fileWriteTimeAfterFirstRun = File.GetLastWriteTimeUtc(singleFilePath);

            WaitForUtcNowToAdvance();

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            DateTime fileWriteTimeAfterSecondRun = File.GetLastWriteTimeUtc(singleFilePath);

            fileWriteTimeAfterSecondRun.Should().Be(fileWriteTimeAfterFirstRun);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_rewrites_the_apphost_for_single_file_publish()
        {
            var publishCommand = GetPublishCommand();
            var appHostPath = Path.Combine(GetPublishDirectory(publishCommand).FullName, SingleFile);
            var singleFilePath = appHostPath;

            publishCommand
                .Execute(RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            var appHostSize = new FileInfo(appHostPath).Length;

            WaitForUtcNowToAdvance();

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            var singleFileSize = new FileInfo(singleFilePath).Length;

            singleFileSize.Should().BeGreaterThan(appHostSize);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_rewrites_the_apphost_for_non_single_file_publish()
        {
            var publishCommand = GetPublishCommand();
            var appHostPath = Path.Combine(GetPublishDirectory(publishCommand).FullName, SingleFile);
            var singleFilePath = appHostPath;

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            var singleFileSize = new FileInfo(singleFilePath).Length;

            WaitForUtcNowToAdvance();

            publishCommand
                .Execute(RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            var appHostSize = new FileInfo(appHostPath).Length;

            appHostSize.Should().BeLessThan(singleFileSize);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_analyzer_warnings_are_produced(string targetFramework)
        {
            var projectName = "ILLinkAnalyzerWarningsApp";
            var testProject = CreateTestProjectWithAnalyzerWarnings(targetFramework, projectName, true);
            testProject.AdditionalProperties["PublishSingleFile"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand
                .Execute(RuntimeIdentifier)
                .Should().Pass()
                .And.HaveStdOutContaining("(9,13): warning IL3000")
                .And.HaveStdOutContaining("(10,13): warning IL3001");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_linker_analyzer_warnings_are_not_produced(string targetFramework)
        {
            var projectName = "ILLinkAnalyzerWarningsApp";
            var testProject = CreateTestProjectWithAnalyzerWarnings(targetFramework, projectName, true);
            // Inactive linker settings should have no effect on the linker analyzer,
            // unless PublishTrimmed is also set.
            testProject.AdditionalProperties["PublishSingleFile"] = "true";
            testProject.AdditionalProperties["SuppressTrimAnalysisWarnings"] = "false";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand
                .Execute(RuntimeIdentifier)
                .Should().Pass()
                .And.NotHaveStdOutContaining("IL2026");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_analyzer_warnings_are_produced_using_EnableSingleFileAnalyzer(string targetFramework)
        {
            var projectName = "ILLinkAnalyzerWarningsApp";
            var testProject = CreateTestProjectWithAnalyzerWarnings(targetFramework, projectName, true);
            testProject.AdditionalProperties["EnableSingleFileAnalyzer"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand
                .Execute(RuntimeIdentifier)
                .Should().Pass()
                .And.HaveStdOutContaining("(9,13): warning IL3000")
                .And.HaveStdOutContaining("(10,13): warning IL3001");
        }


        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("netcoreapp2.1", true)]
        [InlineData("netcoreapp3.0", false)]
        [InlineData("netcoreapp3.1", false)]
        [InlineData("net5.0", false)]
        [InlineData("net6.0", false)]
        [InlineData("net7.0", false)]
        public void PublishSingleFile_fails_for_unsupported_target_framework(string targetFramework, bool shouldFail)
        {
            var testProject = new TestProject()
            {
                Name = "HelloWorld",
                IsExe = true,
                TargetFrameworks = targetFramework
            };
            testProject.AdditionalProperties["PublishSingleFile"] = "true";
            testProject.AdditionalProperties["SelfContained"] = "true";
            testProject.AdditionalProperties["NoWarn"] = "NETSDK1138";  // Silence warning about targeting EOL TFMs
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            var result = publishCommand.Execute(RuntimeIdentifier);
            if (shouldFail)
            {
                result.Should().Fail()
                    .And.HaveStdOutContaining(Strings.PublishSingleFileRequiresVersion30);
            }
            else
            {
                result.Should().Pass()
                    .And.NotHaveStdOutContaining("warning");
            }
        }

        [RequiresMSBuildVersionTheory("17.8.0")]
        [InlineData("netstandard2.0", true)]
        [InlineData("net5.0", true)]
        [InlineData("net6.0", false)]
        [InlineData("netstandard2.0;net5.0", true)] // None of these TFMs are supported for single-file
        [InlineData("netstandard2.0;net6.0", false)] // Net6.0 is the min TFM supported for single-file and targeting.
        [InlineData("netstandard2.0;net8.0", true)] // Net8.0 is supported for single-file, but leaves a "gap" for the supported net6./net7.0 TFMs.
        [InlineData("alias-ns2", true)]
        [InlineData("alias-n6", false)]
        [InlineData("alias-n6;alias-n8", false)] // If all TFMs are supported, there's no warning even though the project uses aliases.
        [InlineData("alias-ns2;alias-n6", true)] // This is correctly multi-targeted, but the logic can't detect this due to the alias so it still warns.
        public void EnableSingleFile_warns_when_expected_for_not_correctly_multitargeted_libraries(string targetFrameworks, bool shouldWarn)
        {
            var testProject = new TestProject()
            {
                Name = "ClassLibTest",
                TargetFrameworks = targetFrameworks
            };
            testProject.AdditionalProperties["EnableSingleFileAnalyzer"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFrameworks)
                .WithProjectChanges(AddTargetFrameworkAliases);

            var buildCommand = new BuildCommand(testAsset);
            var resultAssertion = buildCommand.Execute("/bl:my.binlog")
                .Should().Pass();
            if (shouldWarn) {
                // Note: can't check for Strings.EnableSingleFileAnalyzerUnsupported because each line of
                // the message gets prefixed with a file path by MSBuild.
                resultAssertion
                    .And.HaveStdOutContaining($"warning NETSDK1211")
                    .And.HaveStdOutContaining($"<EnableSingleFileAnalyzer Condition=\"$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net6.0'))\">true</EnableSingleFileAnalyzer>");
            } else {
                resultAssertion.And.NotHaveStdOutContaining($"warning");
            }
        }

        private TestProject CreateTestProjectWithAnalyzerWarnings(string targetFramework, string projectName, bool isExecutable)
        {
            var testProject = new TestProject()
            {
                Name = projectName,
                TargetFrameworks = targetFramework,
                IsExe = isExecutable
            };

            testProject.SourceFiles[$"{projectName}.cs"] = @"
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
class C
{
    static void Main()
    {
        var a = Assembly.LoadFrom(""/some/path/not/in/bundle"");
        _ = a.Location;
        _ = a.GetFiles();
        ProduceLinkerAnalysisWarning();
    }

    [RequiresUnreferencedCode(""Linker analysis warning"")]
    static void ProduceLinkerAnalysisWarning()
    {
    }
}";

            return testProject;
        }

        [RequiresMSBuildVersionTheory("16.8.0")]
        [InlineData("netcoreapp3.0", false, IncludeDefault)]
        [InlineData("netcoreapp3.0", true, IncludeDefault)]
        [InlineData("netcoreapp3.0", false, IncludePdb)]
        [InlineData("netcoreapp3.0", true, IncludePdb)]
        [InlineData("netcoreapp3.1", false, IncludeDefault)]
        [InlineData("netcoreapp3.1", true, IncludeDefault)]
        [InlineData("netcoreapp3.1", false, IncludePdb)]
        [InlineData("netcoreapp3.1", true, IncludePdb)]
        [InlineData("net6.0", false, IncludeDefault)]
        [InlineData("net6.0", false, IncludeNative)]
        [InlineData("net6.0", false, IncludeAllContent)]
        [InlineData("net6.0", true, IncludeDefault)]
        [InlineData("net6.0", true, IncludeNative)]
        [InlineData("net6.0", true, IncludeAllContent)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, false, IncludeDefault)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, false, IncludeNative)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, false, IncludeAllContent)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, true, IncludeDefault)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, true, IncludeNative)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, true, IncludeAllContent)]
        public void It_runs_single_file_apps(string targetFramework, bool selfContained, string bundleOption)
        {
            var testProject = new TestProject()
            {
                Name = "SingleFileTest",
                TargetFrameworks = targetFramework,
                IsExe = true,
            };
            testProject.AdditionalProperties.Add("SelfContained", $"{selfContained}");

            var testAsset = _testAssetsManager.CreateTestProject(
                testProject,
                identifier: targetFramework + "_" + selfContained + "_" + bundleOption);
            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute(PublishSingleFile, RuntimeIdentifier, bundleOption)
                .Should()
                .Pass();

            var publishDir = GetPublishDirectory(publishCommand, targetFramework).FullName;
            var singleFilePath = Path.Combine(publishDir, $"{testProject.Name}{Constants.ExeSuffix}");

            var command = new RunExeCommand(Log, singleFilePath);
            command.Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World");
        }

        [RequiresMSBuildVersionTheory("16.8.0")]
        [InlineData(false)]
        [InlineData(true)]
        public void It_errors_when_including_symbols_targeting_net5(bool selfContained)
        {
            var testProject = new TestProject()
            {
                Name = "SingleFileTest",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };
            testProject.AdditionalProperties.Add("SelfContained", $"{selfContained}");

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: selfContained.ToString());
            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute(PublishSingleFile, RuntimeIdentifier, IncludePdb)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotIncludeSymbolsInSingleFile);
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void It_errors_when_enabling_compression_targeting_net5()
        {
            var testProject = new TestProject()
            {
                Name = "SingleFileTest",
                TargetFrameworks = "net5.0",
                IsExe = true,
            };

            testProject.AdditionalProperties.Add("EnableCompressionInSingleFile", "true");

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute(PublishSingleFile, RuntimeIdentifier)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CompressionInSingleFileRequires60);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_errors_when_enabling_compression_without_selfcontained()
        {
            var testProject = new TestProject()
            {
                Name = "SingleFileTest",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            testProject.AdditionalProperties.Add("SelfContained", "false");
            testProject.AdditionalProperties.Add("EnableCompressionInSingleFile", "true");

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute(PublishSingleFile, RuntimeIdentifier)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CompressionInSingleFileRequiresSelfContained);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_compresses_single_file_as_directed()
        {
            var testProject = new TestProject()
            {
                Name = "SingleFileTest",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(testAsset);
            var singleFilePath = Path.Combine(GetPublishDirectory(publishCommand, ToolsetInfo.CurrentTargetFramework).FullName, $"SingleFileTest{Constants.ExeSuffix}");

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludeNative, "/p:SelfContained=true", "/p:EnableCompressionInSingleFile=false")
                .Should()
                .Pass();
            var uncompressedSize = new FileInfo(singleFilePath).Length;

            WaitForUtcNowToAdvance();

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludeNative, "/p:SelfContained=true", "/p:EnableCompressionInSingleFile=true")
                .Should()
                .Pass();
            var compressedSize = new FileInfo(singleFilePath).Length;

            uncompressedSize.Should().BeGreaterThan(compressedSize);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void It_does_not_compress_single_file_by_default()
        {
            var testProject = new TestProject()
            {
                Name = "SingleFileTest",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(testAsset);
            var singleFilePath = Path.Combine(GetPublishDirectory(publishCommand, ToolsetInfo.CurrentTargetFramework).FullName, $"SingleFileTest{Constants.ExeSuffix}");

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludeNative, "/p:EnableCompressionInSingleFile=false")
                .Should()
                .Pass();
            var uncompressedSize = new FileInfo(singleFilePath).Length;

            WaitForUtcNowToAdvance();

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludeNative)
                .Should()
                .Pass();
            var compressedSize = new FileInfo(singleFilePath).Length;

            uncompressedSize.Should().Be(compressedSize);
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void User_can_get_bundle_info_before_bundling()
        {
            var testProject = new TestProject()
            {
                Name = "SingleFileTest",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };
            testProject.AdditionalProperties.Add("SelfContained", "true");

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project => VerifyPrepareForBundle(project));

            var publishCommand = new PublishCommand(testAsset);
            var singleFilePath = Path.Combine(GetPublishDirectory(publishCommand, ToolsetInfo.CurrentTargetFramework).FullName, $"SingleFileTest{Constants.ExeSuffix}");

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier)
                .Should()
                .Pass();

            var command = new RunExeCommand(Log, singleFilePath);
            command.Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World");

            static void VerifyPrepareForBundle(XDocument project)
            {
                var ns = project.Root.Name.Namespace;
                var targetName = "CheckPrepareForBundleData";

                var target = new XElement(ns + "Target",
                        new XAttribute("Name", targetName),
                        new XAttribute("BeforeTargets", "GenerateSingleFileBundle"),
                        new XAttribute("DependsOnTargets", "PrepareForBundle"));

                project.Root.Add(target);

                //     <Error Condition = "'@(FilesToBundle->AnyHaveMetadataValue('RelativePath', 'System.Private.CoreLib.dll'))' != 'true'" Text="System.Private.CoreLib.dll is not in FilesToBundle list">
                target.Add(
                    new XElement(ns + "Error",
                        new XAttribute("Condition", "'@(FilesToBundle->AnyHaveMetadataValue('RelativePath', 'System.Private.CoreLib.dll'))' != 'true'"),
                        new XAttribute("Text", "System.Private.CoreLib.dll is not in FilesToBundle list")));


                var host = $"SingleFileTest{Constants.ExeSuffix}";

                //     <Error Condition="'$(AppHostFile)' != 'SingleFileTest.exe'" Text="AppHostFile expected to be: 'SingleFileTest.exe' actually: '$(AppHostFile)'" />
                target.Add(
                    new XElement(ns + "Error",
                        new XAttribute("Condition", $"'$(AppHostFile)' != '{host}'"),
                        new XAttribute("Text", $"AppHostFile expected to be: '{host}' actually: '$(AppHostFile)'")));
            }
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void User_can_move_file_before_bundling()
        {
            var testProject = new TestProject()
            {
                Name = "SingleFileTest",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };
            testProject.AdditionalProperties.Add("SelfContained", "true");

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project => VerifyPrepareForBundle(project));

            var publishCommand = new PublishCommand(testAsset);

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier)
                .Should()
                .Pass();

            static void VerifyPrepareForBundle(XDocument project)
            {
                var ns = project.Root.Name.Namespace;
                var targetName = "CheckPrepareForBundleData";

                var target = new XElement(ns + "Target",
                        new XAttribute("Name", targetName),
                        new XAttribute("BeforeTargets", "GenerateSingleFileBundle"),
                        new XAttribute("DependsOnTargets", "PrepareForBundle"));

                project.Root.Add(target);

                // Rename SingleFileTest.dll --> SingleFileTest.dll.renamed
                //
                //     <Move
                //         SourceFiles="@(FilesToBundle)"
                //         DestinationFiles="@(FilesToBundle->'%(FullPath).renamed')"
                //         Condition = "'%(FilesToBundle.RelativePath)' == 'SingleFileTest.dll'" />

                target.Add(
                    new XElement(ns + "Move",
                        new XAttribute("SourceFiles", "@(FilesToBundle)"),
                        new XAttribute("DestinationFiles", "@(FilesToBundle->'%(FullPath).renamed')"),
                        new XAttribute("Condition", "'%(FilesToBundle.RelativePath)' == 'SingleFileTest.dll'")));

                // Modify the FilesToBundle to not have SingleFileTest.dll, so that publish could pass.
                //
                //         <ItemGroup>
                //              <FilesToBundle Remove="@(FilesToBundle)"
                //              Condition="'%(FilesToBundle.RelativePath)' == 'SingleFileTest.dll'" />
                //         </ItemGroup >
                //

                target.Add(
                    new XElement(ns + "ItemGroup",
                        new XElement(ns + "FilesToBundle",
                            new XAttribute("Remove", "@(FilesToBundle)"),
                            new XAttribute("Condition", "'%(FilesToBundle.RelativePath)' == 'SingleFileTest.dll'"))));
            }
        }
    }
}
