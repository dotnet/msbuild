// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;

using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;

using FluentAssertions;
using Xunit;

using Xunit.Abstractions;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Reflection.Metadata;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildADesktopExe : SdkTest
    {
        public GivenThatWeWantToBuildADesktopExe(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void It_builds_a_simple_desktop_app()
        {
            var targetFramework = "net45";
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithTargetFramework(targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "HelloWorld.exe",
                "HelloWorld.pdb",
                "HelloWorld.exe.config"
            });
        }

        //  Windows only because default RuntimeIdentifier only applies when current OS is Windows
        [WindowsOnlyTheory]
        [InlineData("Microsoft.DiasymReader.Native/1.7.0", false, "AnyCPU")]
        [InlineData("Microsoft.DiasymReader.Native/1.7.0", true, "x86")]
        [InlineData("SQLite/3.13.0", false, "x86")]
        [InlineData("SQLite/3.13.0", true, "x86")]

        public void PlatformTargetInferredCorrectly(string packageToReference, bool referencePlatformPackage, string expectedPlatform)
        {
            var testProject = new TestProject()
            {
                Name = "AutoRuntimeIdentifierTest",
                TargetFrameworks = "net472",
                IsExe = true
            };

            var packageElements = packageToReference.Split('/');
            string packageName = packageElements[0];
            string packageVersion = packageElements[1];

            testProject.PackageReferences.Add(new TestPackageReference(packageName, packageVersion));
            if (referencePlatformPackage)
            {
                testProject.PackageReferences.Add(new TestPackageReference("Microsoft.NETCore.Platforms", "2.1.0"));
            }

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: packageName + "_" + referencePlatformPackage.ToString());

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Pass();

            var getValueCommand = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), testProject.TargetFrameworks, "PlatformTarget");

            getValueCommand.Execute().Should().Should();
            getValueCommand.GetValues().Single().Should().Be(expectedPlatform);
        }

        [WindowsOnlyTheory]
        // If we don't set platformTarget and don't use native dependency, we get working AnyCPU app.
        [InlineData("defaults", null, false, "Native code was not used (MSIL)")]
        // If we don't set platformTarget and do use native dependency, we get working x86 app.
        [InlineData("defaultsNative", null, true, "Native code was used (X86)")]
        // If we set x86 and don't use native dependency, we get working x86 app.
        [InlineData("x86", "x86", false, "Native code was not used (X86)")]
        // If we set x86 and do use native dependency, we get working x86 app.
        [InlineData("x86Native", "x86", true, "Native code was used (X86)")]
        // If we set x64 and don't use native dependency, we get working x64 app.
        [InlineData("x64", "x64", false, "Native code was not used (Amd64)")]
        // If we set x64 and do use native dependency, we get working x64 app.
        [InlineData("x64Native", "x64", true, "Native code was used (Amd64)")]
        // If we set AnyCPU and don't use native dependency, we get working  AnyCPU app.
        [InlineData("AnyCPU", "AnyCPU", false, "Native code was not used (MSIL)")]
        // If we set AnyCPU and do use native dependency, we get any CPU app that can't find its native dependency.
        // Tests current behavior, but ideally we'd also raise a build diagnostic in this case: https://github.com/dotnet/sdk/issues/843
        [InlineData("AnyCPUNative", "AnyCPU", true, "Native code failed (MSIL)")]
        public void It_handles_native_dependencies_and_platform_target(
             string identifier,
             string platformTarget,
             bool useNativeCode,
             string expectedProgramOutput)
        {
            foreach (bool multiTarget in new[] { false, true })
            {
                var testAsset = _testAssetsManager
                   .CopyTestAsset("DesktopMinusRid", identifier: Path.DirectorySeparatorChar + identifier + (multiTarget ? "Multi" : ""))
                   .WithSource()
                   .WithProjectChanges(project =>
                   {
                       var ns = project.Root.Name.Namespace;
                       var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                       propertyGroup.Add(new XElement(ns + "UseNativeCode", useNativeCode));

                       if (platformTarget != null)
                       {
                           propertyGroup.Add(new XElement(ns + "PlatformTarget", platformTarget));
                       }

                       if (multiTarget)
                       {
                           propertyGroup.Element(ns + "TargetFramework").Remove();
                           propertyGroup.Add(new XElement(ns + "TargetFrameworks", "net46;netcoreapp1.1"));
                       }
                   });

                var buildCommand = new BuildCommand(testAsset);
                buildCommand
                    .Execute()
                    .Should()
                    .Pass();

                var exe = Path.Combine(buildCommand.GetOutputDirectory("net46").FullName, "DesktopMinusRid.exe");
                var runCommand = new RunExeCommand(Log, exe);
                runCommand
                    .Execute()
                    .Should()
                    .Pass()
                    .And
                    .HaveStdOutContaining(expectedProgramOutput);
            }
        }

        [WindowsOnlyTheory]
        // implicit rid with option to append rid to output path off -> do not append
        [InlineData("implicitOff", "", false, false)]
        // implicit rid with option to append rid to output path on -> do not append (never append implicit rid irrespective of option)
        [InlineData("implicitOn", "", true, false)]
        // explicit  rid with option to append rid to output path off -> do not append
        [InlineData("explicitOff", $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x86", false, false)]
        // explicit rid with option to append rid to output path on -> append
        [InlineData("explicitOn", $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x64", true, true)]
        public void It_appends_rid_to_outdir_correctly(string identifier, string rid, bool useAppendOption, bool shouldAppend)
        {
            foreach (bool multiTarget in new[] { false, true })
            {
                var testAsset = _testAssetsManager
                    .CopyTestAsset("DesktopMinusRid", identifier: Path.DirectorySeparatorChar + identifier + (multiTarget ? "Multi" : ""))
                    .WithSource()
                    .WithProjectChanges(project =>
                    {
                        var ns = project.Root.Name.Namespace;
                        var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                        propertyGroup.Add(new XElement(ns + "RuntimeIdentifier", rid));
                        propertyGroup.Add(new XElement(ns + "AppendRuntimeIdentifierToOutputPath", useAppendOption.ToString()));

                        if (multiTarget)
                        {
                            propertyGroup.Element(ns + "RuntimeIdentifier").Add(new XAttribute("Condition", "'$(TargetFramework)' == 'net46'"));
                            propertyGroup.Element(ns + "TargetFramework").Remove();
                            propertyGroup.Add(new XElement(ns + "TargetFrameworks", "net46;netcoreapp1.1"));
                        }
                    });

                var buildCommand = new BuildCommand(testAsset);
                buildCommand
                    .Execute()
                    .Should()
                    .Pass();

                var publishCommand = new PublishCommand(testAsset);
                publishCommand
                    .Execute(multiTarget ? new[] { "/p:TargetFramework=net46" } : Array.Empty<string>())
                    .Should()
                    .Pass();

                string expectedOutput;
                switch (rid)
                {
                    case "":
                        expectedOutput = "Native code was not used (MSIL)";
                        break;

                    case $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x86":
                        expectedOutput = "Native code was not used (X86)";
                        break;

                    case $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x64":
                        expectedOutput = "Native code was not used (Amd64)";
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(rid));
                }

                var outputDirectory = buildCommand.GetOutputDirectory("net46", runtimeIdentifier: shouldAppend ? rid : "");
                var publishDirectory = publishCommand.GetOutputDirectory("net46", runtimeIdentifier: rid);

                foreach (var directory in new[] { outputDirectory, publishDirectory })
                {
                    var exe = Path.Combine(directory.FullName, "DesktopMinusRid.exe");

                    var runCommand = new RunExeCommand(Log, exe);
                    runCommand
                        .Execute()
                        .Should()
                        .Pass()
                        .And
                        .HaveStdOutContaining(expectedOutput);
                }
            }
        }

        [WindowsOnlyTheory]
        [InlineData("win7-x86", "x86")]
        [InlineData("win8-x86-aot", "x86")]
        [InlineData("win7-x64", "x64")]
        [InlineData("win8-x64-aot", "x64")]
        [InlineData($"{ToolsetInfo.LatestWinRuntimeIdentifier}-arm", "arm")]
        [InlineData($"{ToolsetInfo.LatestWinRuntimeIdentifier}-arm-aot", "arm")]
        [InlineData($"{ToolsetInfo.LatestWinRuntimeIdentifier}-arm64", "arm64")]
        [InlineData($"{ToolsetInfo.LatestWinRuntimeIdentifier}-arm64-aot", "arm64")]
        // cpu architecture is never expected at the front
        [InlineData("x86-something", "AnyCPU")]
        [InlineData("x64-something", "AnyCPU")]
        [InlineData("arm-something", "AnyCPU")]
        public void It_builds_with_inferred_platform_target(string runtimeIdentifier, string expectedPlatformTarget)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopMinusRid", identifier: Path.DirectorySeparatorChar + runtimeIdentifier)
                .WithSource();

            var getValuesCommand = new GetValuesCommand(Log, testAsset.TestRoot,
                "net46", "PlatformTarget", GetValuesCommand.ValueType.Property);

            getValuesCommand
                .Execute($"/p:RuntimeIdentifier={runtimeIdentifier}")
                .Should()
                .Pass();

            getValuesCommand
                .GetValues()
                .Should()
                .BeEquivalentTo(expectedPlatformTarget);
        }

        [WindowsOnlyFact]
        public void It_respects_explicit_platform_target()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopMinusRid")
                .WithSource();

            var getValuesCommand = new GetValuesCommand(Log, testAsset.TestRoot,
                "net46", "PlatformTarget", GetValuesCommand.ValueType.Property);

            getValuesCommand
                .Execute($"/p:RuntimeIdentifier={ToolsetInfo.LatestWinRuntimeIdentifier}-x86", "/p:PlatformTarget=x64")
                .Should()
                .Pass();

            getValuesCommand
                .GetValues()
                .Should()
                .BeEquivalentTo("x64");
        }

        [WindowsOnlyFact]
        public void It_includes_default_framework_references()
        {
            var testProject = new TestProject()
            {
                Name = "DefaultReferences",
                //  TODO: Add net35 to the TargetFrameworks list once https://github.com/Microsoft/msbuild/issues/1333 is fixed
                TargetFrameworks = "net40;net45;net462",
                IsExe = true
            };

            string sourceFile =
@"using System;

namespace DefaultReferences
{
    public class TestClass
    {
        public static void Main(string [] args)
        {
            var uri = new System.Uri(""http://github.com/dotnet/corefx"");
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        }
    }
}";
            testProject.SourceFiles.Add("TestClass.cs", sourceFile);

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset, "DefaultReferences");

            buildCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutMatching("Could not resolve this reference", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        }

        [WindowsOnlyFact]
        public void It_reports_a_single_failure_if_reference_assemblies_are_not_found()
        {
            var testProject = new TestProject()
            {
                Name = "MissingReferenceAssemblies",
                //  A version of .NET we don't expect to exist
                TargetFrameworks = "net469",
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var buildCommand = new BuildCommand(testAsset);

            //  Pass "/clp:summary" so that we can check output for string "1 Error(s)"
            var result = buildCommand.Execute("/clp:summary");

            result.Should().Fail();

            //  Error code for reference assemblies not found
            result.StdOut.Should().Contain("MSB3644");

            //  Error code for exception generated from task
            result.StdOut.Should().NotContain("MSB4018");

            //  Ensure no other errors are generated
            result.StdOut.Should().Contain("1 Error(s)");
        }

        [WindowsOnlyFact]
        public void It_does_not_report_conflicts_if_the_same_framework_assembly_is_referenced_multiple_times()
        {
            var testProject = new TestProject()
            {
                Name = "DuplicateFrameworkReferences",
                TargetFrameworks = "net462",
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    p.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "Reference", new XAttribute("Include", "System")));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And
                .NotHaveStdOutMatching("Encountered conflict", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        [WindowsOnlyFact]
        public void It_does_not_report_conflicts_when_referencing_a_nuget_package()
        {
            var testProject = new TestProject()
            {
                Name = "DesktopConflictsNuGet",
                TargetFrameworks = "net462",
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    p.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "PackageReference",
                                    new XAttribute("Include", "NewtonSoft.Json"),
                                    new XAttribute("Version", ToolsetInfo.GetNewtonsoftJsonPackageVersion())));
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And
                .NotHaveStdOutMatching("Encountered conflict", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        [Fact]
        public void It_does_not_report_conflicts_when_with_http_4_1_package()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testProject = new TestProject()
            {
                Name = "DesktopConflictsHttp4_1",
                TargetFrameworks = "net462",
                IsExe = true
            };

            testProject.PackageReferences.Add(new TestPackageReference("System.Net.Http", "4.1.0"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var buildCommand = new BuildCommand(testAsset);

            //  Verify that ResolveAssemblyReference doesn't generate any conflicts
            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And
                .NotHaveStdOutMatching("MSB3243", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        [WindowsOnlyFact]
        public void It_does_not_report_conflicts_with_runtime_specific_items()
        {
            var testProject = new TestProject()
            {
                Name = "DesktopConflictsRuntimeTargets",
                TargetFrameworks = "net462",
                IsExe = true
            };

            testProject.AdditionalProperties["PlatformTarget"] = "AnyCPU";

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "ItemGroup");
                    p.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "PackageReference",
                                    new XAttribute("Include", "System.Security.Cryptography.Algorithms"),
                                    new XAttribute("Version", "4.3.0")));
                });

            var buildCommand = new BuildCommand(testAsset);

            var buildResult = buildCommand
                .Execute("/v:normal");

            buildResult.Should().Pass();

            //  Correct asset should be copied to output folder. Before fixing https://github.com/dotnet/sdk/issues/1510,
            //  the runtimeTargets items would win conflict resolution, and then would not be copied to the output folder,
            //  so there'd be no copy of the DLL in the output folder.
            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().HaveFile("System.Security.Cryptography.Algorithms.dll");

            //  There should be no conflicts
            buildResult.Should().NotHaveStdOutMatching("Encountered conflict", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        [FullMSBuildOnlyTheory]
        [InlineData("4.3.3")]
        [InlineData("4.1.0")]
        public void It_builds_successfully_if_inbox_assembly_wins_conflict_resolution(string httpPackageVersion)
        {
            Test_inbox_assembly_wins_conflict_resolution(false, httpPackageVersion);
        }

        [WindowsOnlyTheory]
        [InlineData("4.3.3")]
        [InlineData("4.1.0")]
        public void It_builds_successfully_if_inbox_assembly_wins_conflict_resolution_sdk(string httpPackageVersion)
        {
            Test_inbox_assembly_wins_conflict_resolution(true, httpPackageVersion);
        }
        void Test_inbox_assembly_wins_conflict_resolution(bool useSdkProject, string httpPackageVersion, bool useAlias = false)
        {
            var testProject = new TestProject()
            {
                Name = "DesktopInBoxConflictResolution",
                IsExe = true
            };

            if (useSdkProject)
            {
                testProject.TargetFrameworks = "net472";
            }
            else
            {
                testProject.IsSdkProject = false;
                testProject.TargetFrameworkVersion = "v4.7.2";
            }

            testProject.PackageReferences.Add(new TestPackageReference("System.Net.Http", httpPackageVersion));

            testProject.SourceFiles["Program.cs"] =
                (useAlias ? "extern alias snh;" + Environment.NewLine : "") +

                @"using System;


class Program
{
    static void Main(string[] args)
    {
" +
        (useAlias ? "var client = new snh::System.Net.Http.HttpClient();" :
                    "var client = new System.Net.Http.HttpClient();") +
@"
    }
}";

            string identifier = (useSdkProject ? "_SDK_" : "_") +
                                (useAlias ? "alias" : "") +
                                httpPackageVersion;

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name, identifier)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "ItemGroup");
                    p.Root.Add(itemGroup);

                    var httpReference = new XElement(ns + "Reference",
                                                     new XAttribute("Include", "System.Net.Http"));

                    if (useAlias)
                    {
                        httpReference.SetAttributeValue("Aliases", "snh");
                    }

                    itemGroup.Add(httpReference);
                });

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And.NotHaveStdOutContaining("MSB3277") // MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
                .And.NotHaveStdOutContaining("MSB3243") // MSB3243: No way to resolve conflict between...
                .And.NotHaveStdOutContaining("Could not determine");
        }

        [FullMSBuildOnlyTheory(Skip = "https://github.com/NuGet/Home/issues/8238")]
        [InlineData("4.3.3")]
        [InlineData("4.1.0")]
        public void Aliases_are_preserved_if_inbox_assembly_wins_conflict_resolution(string httpPackageVersion)
        {
            Test_inbox_assembly_wins_conflict_resolution(false, httpPackageVersion, useAlias: true);
        }

        [WindowsOnlyTheory]
        [InlineData("4.3.3")]
        [InlineData("4.1.0")]
        public void Aliases_are_preserved_if_inbox_assembly_wins_conflict_resolution_sdk(string httpPackageVersion)
        {
            Test_inbox_assembly_wins_conflict_resolution(true, httpPackageVersion, useAlias: true);
        }

        [WindowsOnlyFact]
        public void Aliases_are_preserved_if_framework_reference_is_overridden_by_package()
        {
            var testProject = new TestProject()
            {
                Name = "OverriddenAlias",
                IsExe = true,
                TargetFrameworks = "net462"
            };

            testProject.PackageReferences.Add(new TestPackageReference("System.Net.Http", "4.3.3"));

            testProject.SourceFiles["Program.cs"] = @"
extern alias snh;
using System;

class Program
{
    static void Main(string[] args)
    {
        var client = new snh::System.Net.Http.HttpClient();
    }
}";

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .WithProjectChanges(p =>
                {
                    var ns = p.Root.Name.Namespace;
                    var itemGroup = new XElement(ns + "ItemGroup");
                    p.Root.Add(itemGroup);

                    var httpReference = new XElement(ns + "Reference",
                                                     new XAttribute("Include", "System.Net.Http"));

                    httpReference.SetAttributeValue("Aliases", "snh");
                    itemGroup.Add(httpReference);
                });


            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [WindowsOnlyFact]
        public void It_generates_binding_redirects_if_needed()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopNeedsBindingRedirects")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("net452", runtimeIdentifier: $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x86");

            outputDirectory.Should().HaveFiles(new[] {
                "DesktopNeedsBindingRedirects.exe",
                "DesktopNeedsBindingRedirects.exe.config"
            });

            XElement root = XElement.Load(outputDirectory.GetFiles("DesktopNeedsBindingRedirects.exe.config").Single().FullName);
            root.Elements("runtime").Single().Elements().Should().Contain(e => e.Name.LocalName == "assemblyBinding");
        }

        [WindowsOnlyFact]
        public void It_generates_supportedRuntime_when_no_appconfig_in_source_require_binding_redirect()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopNeedsBindingRedirects")
                .WithSource();

            XElement root = BuildTestAssetGetAppConfig(testAsset);
            root.Elements("startup").Single().Elements().Should().Contain(e => e.Name.LocalName == "supportedRuntime");
        }

        [WindowsOnlyFact]
        public void It_generates_appconfig_incrementally()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopNeedsBindingRedirects")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            FileInfo outputfile = buildCommand
                .GetIntermediateDirectory("net452", runtimeIdentifier: $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x86")
                .GetFiles("DesktopNeedsBindingRedirects.exe.withSupportedRuntime.config").Single();

            DateTime firstBuildWriteTime = File.GetLastWriteTimeUtc(outputfile.FullName);

            buildCommand
               .Execute()
               .Should()
               .Pass();

            DateTime secondBuildBuildWriteTime = File.GetLastWriteTimeUtc(outputfile.FullName);

            secondBuildBuildWriteTime.Should().Be(firstBuildWriteTime);
        }

        [WindowsOnlyFact]
        public void It_generates_supportedRuntime_when_no_appconfig_in_source_does_not_require_binding_redirect()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopNeedsBindingRedirects")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "ItemGroup").First();

                    propertyGroup.Elements(ns + "PackageReference").Remove();
                });

            XElement root = BuildTestAssetGetAppConfig(testAsset);
            root.Elements("startup").Single()
                .Elements().Should()
                .Contain(e => e.Name.LocalName == "supportedRuntime");
        }

        [WindowsOnlyFact]
        public void It_generates_supportedRuntime_when_there_is_appconfig_with_supportedRuntime_in_source_require_binding_redirect()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopNeedsBindingRedirects")
                .WithSource();

            var appconfigWithoutSupportedRuntime = new XDocument(
                    new XDeclaration("1.0", "utf-8", "true"),
                    new XElement("configuration",
                        new XElement("startup",
                             new XElement("supportedRuntime",
                           new XAttribute("version", "v999")))));

            appconfigWithoutSupportedRuntime.Save(
                Path.Combine(testAsset.TestRoot, "App.Config"));

            XElement root = BuildTestAssetGetAppConfig(testAsset);
            root.Elements("startup").Single()
                .Elements("supportedRuntime").Single()
                .Should().HaveAttribute("version", "v999", "It should keep existing supportedRuntime");
        }

        [WindowsOnlyFact]
        public void It_generates_supportedRuntime_when_there_is_appconfig_without_supportedRuntime_in_source_require_binding_redirect()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("DesktopNeedsBindingRedirects")
                .WithSource();

            var appconfigWithoutSupportedRuntime = new XDocument(
                    new XDeclaration("1.0", "utf-8", "true"),
                        new XElement("configuration",
                            new XElement("appSettings")));

            appconfigWithoutSupportedRuntime.Save(
                Path.Combine(testAsset.TestRoot, "App.Config"));

            XElement root = BuildTestAssetGetAppConfig(testAsset);
            root.Elements("startup").Single().Elements().Should().Contain(e => e.Name.LocalName == "supportedRuntime");
            root.Should().HaveElement("appSettings",
                "It should keep existing appconfig's setting");
        }

        private XElement BuildTestAssetGetAppConfig(TestAsset testAsset)
        {
            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            DirectoryInfo outputDirectory = buildCommand.GetOutputDirectory("net452", runtimeIdentifier: $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x86");

            return XElement.Load(outputDirectory.GetFiles("DesktopNeedsBindingRedirects.exe.config").Single().FullName);
        }

        [WindowsOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_places_package_satellites_correctly(bool crossTarget)
        {
            var testProject = new TestProject()
            {
                Name = "DesktopUsingPackageWithSatellites",
                TargetFrameworks = "net46",
                IsExe = true
            };

            if (crossTarget)
            {
                testProject.Name += "_cross";
            }

            testProject.PackageReferences.Add(new TestPackageReference("FluentValidation", "5.5.0"));

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name)
                .WithProjectChanges(project =>
                {
                    if (crossTarget)
                    {
                        var ns = project.Root.Name.Namespace;
                        var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                        propertyGroup.Element(ns + "TargetFramework").Name += "s";
                    }
                });

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute("/v:normal")
                .Should()
                .Pass()
                .And
                .NotHaveStdOutMatching("Encountered conflict", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);
            outputDirectory.Should().NotHaveFile("FluentValidation.resources.dll");
            outputDirectory.Should().HaveFile(@"fr\FluentValidation.resources.dll");
        }

        [WindowsOnlyFact(Skip = "https://github.com/NuGet/Home/issues/6823")]
        public void It_allows_TargetFrameworkVersion_to_be_capitalized()
        {
            var testProject = new TestProject()
            {
                Name = "UppercaseTargetFrameworkVersion",
                IsSdkProject = false,
                IsExe = true,
                TargetFrameworkVersion = "V4.6.2"
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }

        [WindowsOnlyFact]
        public void It_places_package_xml_in_ref_folder_in_output_directory()
        {
            var testProject = new TestProject()
            {
                Name = "DesktopUsingPackageWithdXmlInRefFolder",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            // This package contains an xml in ref, but not in lib.
            testProject.PackageReferences.Add(new TestPackageReference("system.diagnostics.debug", "4.3.0"));

            testProject.AdditionalProperties.Add("CopyDebugSymbolFilesFromPackages", "true");
            testProject.AdditionalProperties.Add("CopyDocumentationFilesFromPackages", "true");

            TestAsset testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            // this xml is coming from ref folder
            outputDirectory.Should().HaveFile("System.Diagnostics.Debug.xml");
        }

        [WindowsOnlyTheory]
        [InlineData("true",  "true")]
        [InlineData("true",  "false")]
        [InlineData("false", "true")]
        [InlineData("false", "false")]
        public void It_places_package_pdb_and_xml_files_in_output_directory(string enableCopyDebugSymbolFilesFromPackages, string enableDocumentationFilesFromPackages)
        {
            var testProject = new TestProject()
            {
                Name = "DesktopUsingPackageWithPdbAndXml",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.Build", "17.3.1"));
            
            testProject.AdditionalProperties.Add("CopyDebugSymbolFilesFromPackages", enableCopyDebugSymbolFilesFromPackages);
            testProject.AdditionalProperties.Add("CopyDocumentationFilesFromPackages", enableDocumentationFilesFromPackages);

            string testPath = enableCopyDebugSymbolFilesFromPackages + enableDocumentationFilesFromPackages;
            TestAsset testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name, identifier: testPath);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            HelperCheckPdbAndDocumentation(outputDirectory, "Microsoft.Build", enableCopyDebugSymbolFilesFromPackages, enableDocumentationFilesFromPackages);
        }

        [WindowsOnlyTheory]
        [InlineData("true",  "true")]
        [InlineData("true",  "false")]
        [InlineData("false", "true")]
        [InlineData("false", "false")]
        public void It_places_package_pdb_and_xml_files_from_project_references_in_output_directory(string enableCopyDebugSymbolFilesFromPackages, string enableDocumentationFilesFromPackages)
        {
            var libraryProject = new TestProject()
            {
                Name = "ProjectWithPackage",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = false,
            };

            libraryProject.PackageReferences.Add(new TestPackageReference("Microsoft.Build", "17.3.1"));

            var consumerProject = new TestProject()
            {
                Name = "ConsumerProject",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            consumerProject.AdditionalProperties.Add("CopyDebugSymbolFilesFromPackages", enableCopyDebugSymbolFilesFromPackages);
            consumerProject.AdditionalProperties.Add("CopyDocumentationFilesFromPackages", enableDocumentationFilesFromPackages);

            consumerProject.ReferencedProjects.Add(libraryProject);

            string testPath = enableCopyDebugSymbolFilesFromPackages + enableDocumentationFilesFromPackages;
            TestAsset testAsset = _testAssetsManager.CreateTestProject(consumerProject, consumerProject.Name, identifier: testPath);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(libraryProject.TargetFrameworks);

            HelperCheckPdbAndDocumentation(outputDirectory, "Microsoft.Build", enableCopyDebugSymbolFilesFromPackages, enableDocumentationFilesFromPackages);
        }

        [WindowsOnlyTheory]
        [InlineData("true",  "true")]
        [InlineData("true",  "false")]
        [InlineData("false", "true")]
        [InlineData("false", "false")]
        public void It_places_package_pdb_and_xml_files_in_publish_directory(string enableCopyDebugSymbolFilesFromPackages, string enableDocumentationFilesFromPackages)
        {
            var testProject = new TestProject()
            {
                Name = "DesktopPublishPackageWithPdbAndXml",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.Build", "17.3.1"));

            testProject.AdditionalProperties.Add("CopyDebugSymbolFilesFromPackages", enableCopyDebugSymbolFilesFromPackages);
            testProject.AdditionalProperties.Add("CopyDocumentationFilesFromPackages", enableDocumentationFilesFromPackages);

            string testPath = enableCopyDebugSymbolFilesFromPackages + enableDocumentationFilesFromPackages;
            TestAsset testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name, identifier: testPath);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var publishCommand = new PublishCommand(testAsset);

            publishCommand
                .Execute()
                .Should()
            .Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            HelperCheckPdbAndDocumentation(publishDirectory, "Microsoft.Build", enableCopyDebugSymbolFilesFromPackages, enableDocumentationFilesFromPackages);
        }

        [WindowsOnlyFact]
        public void It_places_package_xml_files_in_output_directory_but_not_in_publish()
        {
            var testProject = new TestProject()
            {
                Name = "DesktopPackageWithXmlNotPublished",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };

            testProject.PackageReferences.Add(new TestPackageReference("Microsoft.Build", "17.3.1"));

            testProject.AdditionalProperties.Add("PublishReferencesDocumentationFiles", "false");
            testProject.AdditionalProperties.Add("CopyDocumentationFilesFromPackages", "true");

            TestAsset testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var publishCommand = new PublishCommand(testAsset);

            publishCommand
                .Execute()
                .Should()
            .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            var publishDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks);

            outputDirectory.Should().HaveFile("Microsoft.Build.xml");
            publishDirectory.Should().NotHaveFile("Microsoft.Build.xml");
        }

        void HelperCheckPdbAndDocumentation(
            DirectoryInfo directoryInfo,
            string packageName,
            string enableCopyDebugSymbolFilesFromPackages,
            string enableDocumentationFilesFromPackages)
        {
            string assemblyFile = packageName + ".dll";
            string pdbFile = packageName + ".pdb";
            string docFile = packageName + ".xml";

            ShouldHave(assemblyFile);
            if (enableCopyDebugSymbolFilesFromPackages == "true")
            {
                ShouldHave(pdbFile);
            }
            else
            {
                ShouldNotHave(pdbFile);
            }

            if (enableDocumentationFilesFromPackages == "true")
            {
                ShouldHave(docFile);
            }
            else
            {
                ShouldNotHave(docFile);
            }

            void ShouldHave(string file) => directoryInfo.Should().HaveFile(file);

            void ShouldNotHave(string file) => directoryInfo.Should().NotHaveFile(file);
        }
    }
}
