// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.NET.Publish.Tests.PublishTestUtils;
using System.Security.Permissions;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToRunILLink : SdkTest
    {
        public GivenThatWeWantToRunILLink(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_only_runs_when_switch_is_enabled(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}").Should().Pass();

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

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("netcoreapp3.0", true)]
        [InlineData("netcoreapp3.0", false)]
        [InlineData("net5.0", false)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, false)]
        public void ILLink_runs_and_creates_linked_app(string targetFramework, bool referenceClassLibAsPackage)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName, referenceClassLibAsPackage);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework + referenceClassLibAsPackage)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

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

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_links_simple_app_without_analysis_warnings_and_it_runs(string targetFramework)
        {
            foreach (var trimMode in new[] { "copyused", "link" })
            {
                var projectName = "HelloWorld";
                var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

                var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName);
                var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework + trimMode);

                var publishCommand = new PublishCommand(testAsset);
                publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", $"/p:TrimMode={trimMode}", "/p:SuppressTrimAnalysisWarnings=true")
                    .Should().Pass()
                    .And.NotHaveStdOutContaining("warning IL2075")
                    .And.NotHaveStdOutContaining("warning IL2026");

                var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid);
                var exe = Path.Combine(publishDirectory.FullName, $"{testProject.Name}{Constants.ExeSuffix}");

                var command = new RunExeCommand(Log, exe)
                    .Execute().Should().Pass()
                    .And.HaveStdOutContaining("Hello world");
            }
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void PrepareForILLink_can_set_IsTrimmable(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => SetMetadata(project, referenceProjectName, "IsTrimmable", "True"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedIsTrimmableDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            File.Exists(publishedDll).Should().BeTrue();
            // Check that the unused trimmable assembly was removed
            File.Exists(unusedIsTrimmableDll).Should().BeFalse();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void PrepareForILLink_can_set_TrimMode(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName, referenceProjectIdentifier: targetFramework);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => SetMetadata(project, referenceProjectName, "TrimMode", "link"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedTrimModeLinkDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            File.Exists(publishedDll).Should().BeTrue();
            // Check that the unused "link" assembly was removed.
            File.Exists(unusedTrimModeLinkDll).Should().BeFalse();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("net5.0", "link")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "copyused")]
        [InlineData("net6.0", "full")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "full")]
        [InlineData("net6.0", "partial")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "partial")]
        public void ILLink_respects_global_TrimMode(string targetFramework, string trimMode)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName, referenceProjectIdentifier: targetFramework);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework + trimMode)
                .WithProjectChanges(project => SetGlobalTrimMode(project, trimMode))
                .WithProjectChanges(project => SetMetadata(project, referenceProjectName, "IsTrimmable", "True"))
                .WithProjectChanges(project => AddRootDescriptor(project, $"{referenceProjectName}.xml"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var isTrimmableDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(isTrimmableDll).Should().BeTrue();
            DoesImageHaveMethod(isTrimmableDll, "UnusedMethodToRoot").Should().BeTrue();
            if (trimMode is "link" or "full" or "partial") {
                // Check that the assembly was trimmed at the member level
                DoesImageHaveMethod(isTrimmableDll, "UnusedMethod").Should().BeFalse();
            } else {
                // Check that the assembly was trimmed at the assembxly level
                DoesImageHaveMethod(isTrimmableDll, "UnusedMethod").Should().BeTrue();
            }
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_roots_IntermediateAssembly(string targetFramework)
        {
             var projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => SetGlobalTrimMode(project, "link"))
                .WithProjectChanges(project => SetMetadata(project, projectName, "IsTrimmable", "True"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");

            // The assembly is trimmed but its entry point is kept
            DoesImageHaveMethod(publishedDll, "UnusedMethod").Should().BeFalse();
            DoesImageHaveMethod(publishedDll, "Main").Should().BeTrue();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_respects_TrimmableAssembly(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            testProject.AddItem("TrimmableAssembly", "Include", referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedTrimmableDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            File.Exists(publishedDll).Should().BeTrue();
            // Check that the unused assembly was removed.
            File.Exists(unusedTrimmableDll).Should().BeFalse();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("net6.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_respects_IsTrimmable_attribute(string targetFramework)
        {
            string projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = CreateTestProjectWithIsTrimmableAttributes(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var unusedTrimmableDll = Path.Combine(publishDirectory, "UnusedTrimmableAssembly.dll");
            var unusedNonTrimmableDll = Path.Combine(publishDirectory, "UnusedNonTrimmableAssembly.dll");

            // Only unused non-trimmable assemblies are kept
            File.Exists(unusedTrimmableDll).Should().BeFalse();
            if (targetFramework == "net6.0")
            {
                // In net6.0 the default is to keep assemblies not marked trimmable
                DoesImageHaveMethod(unusedNonTrimmableDll, "UnusedMethod").Should().BeTrue();
            }
            else
            {
                // In net7.0+ the default is to keep assemblies not marked trimmable
                File.Exists(unusedNonTrimmableDll).Should().BeFalse();
            }
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net6Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_IsTrimmable_metadata_can_override_attribute(string targetFramework)
        {
            string projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = CreateTestProjectWithIsTrimmableAttributes(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => SetGlobalTrimMode(project, "partial"))
                .WithProjectChanges(project => SetMetadata(project, "UnusedTrimmableAssembly", "IsTrimmable", "false"))
                .WithProjectChanges(project => SetMetadata(project, "UnusedNonTrimmableAssembly", "IsTrimmable", "true"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/v:n").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var unusedTrimmableDll = Path.Combine(publishDirectory, "UnusedTrimmableAssembly.dll");
            var unusedNonTrimmableDll = Path.Combine(publishDirectory, "UnusedNonTrimmableAssembly.dll");

            // Attributed IsTrimmable assembly with IsTrimmable=false metadata should be kept
            DoesImageHaveMethod(unusedTrimmableDll, "UnusedMethod").Should().BeTrue();
            // Unattributed assembly with IsTrimmable=true should be trimmed
            File.Exists(unusedNonTrimmableDll).Should().BeFalse();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("net6.0")]
        public void ILLink_TrimMode_applies_to_IsTrimmable_assemblies(string targetFramework)
        {
            string projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = CreateTestProjectWithIsTrimmableAttributes(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var trimmableDll = Path.Combine(publishDirectory, "TrimmableAssembly.dll");
            var nonTrimmableDll = Path.Combine(publishDirectory, "NonTrimmableAssembly.dll");
            var unusedTrimmableDll = Path.Combine(publishDirectory, "UnusedTrimmableAssembly.dll");
            var unusedNonTrimmableDll = Path.Combine(publishDirectory, "UnusedNonTrimmableAssembly.dll");

            // Trimmable assemblies are trimmed at member level
            DoesImageHaveMethod(trimmableDll, "UnusedMethod").Should().BeFalse();
            DoesImageHaveMethod(trimmableDll, "UsedMethod").Should().BeTrue();
            File.Exists(unusedTrimmableDll).Should().BeFalse();
            // Non-trimmable assemblies still get copied
            DoesImageHaveMethod(nonTrimmableDll, "UnusedMethod").Should().BeTrue();
            DoesImageHaveMethod(unusedNonTrimmableDll, "UnusedMethod").Should().BeTrue();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "full")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "partial")]
        public void ILLink_TrimMode_new_options(string targetFramework, string trimMode)
        {
            string projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = CreateTestProjectWithIsTrimmableAttributes(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework + trimMode)
                .WithProjectChanges(project => SetGlobalTrimMode(project, trimMode));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "-bl").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var trimmableDll = Path.Combine(publishDirectory, "TrimmableAssembly.dll");
            var nonTrimmableDll = Path.Combine(publishDirectory, "NonTrimmableAssembly.dll");
            var unusedTrimmableDll = Path.Combine(publishDirectory, "UnusedTrimmableAssembly.dll");
            var unusedNonTrimmableDll = Path.Combine(publishDirectory, "UnusedNonTrimmableAssembly.dll");

            // Trimmable assemblies are trimmed at member level
            DoesImageHaveMethod(trimmableDll, "UnusedMethod").Should().BeFalse();
            DoesImageHaveMethod(trimmableDll, "UsedMethod").Should().BeTrue();
            File.Exists(unusedTrimmableDll).Should().BeFalse();
            if (trimMode is "full")
            {
                DoesImageHaveMethod(nonTrimmableDll, "UnusedMethod").Should().BeFalse();
                File.Exists(unusedNonTrimmableDll).Should().BeFalse();
            }
            else if (trimMode is "partial")
            {
                DoesImageHaveMethod(nonTrimmableDll, "UnusedMethod").Should().BeTrue();
                DoesImageHaveMethod(unusedNonTrimmableDll, "UnusedMethod").Should().BeTrue();
            }
            else
            {
                Assert.True(false, "unexpected value");
            }
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_can_set_TrimmerDefaultAction(string targetFramework)
        {
            string projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = CreateTestProjectWithIsTrimmableAttributes(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => SetTrimmerDefaultAction(project, "link"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var trimmableDll = Path.Combine(publishDirectory, "TrimmableAssembly.dll");
            var nonTrimmableDll = Path.Combine(publishDirectory, "NonTrimmableAssembly.dll");
            var unusedTrimmableDll = Path.Combine(publishDirectory, "UnusedTrimmableAssembly.dll");
            var unusedNonTrimmableDll = Path.Combine(publishDirectory, "UnusedNonTrimmableAssembly.dll");

            // Trimmable assemblies are trimmed at member level
            DoesImageHaveMethod(trimmableDll, "UnusedMethod").Should().BeFalse();
            File.Exists(unusedTrimmableDll).Should().BeFalse();
            // Unattributed assemblies are trimmed at member level
            DoesImageHaveMethod(nonTrimmableDll, "UnusedMethod").Should().BeFalse();
            File.Exists(unusedNonTrimmableDll).Should().BeFalse();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("net5.0")]
        public void ILLink_analysis_warnings_are_disabled_by_default(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}")
                .Should().Pass()
                // trim analysis warnings are disabled
                .And.NotHaveStdOutMatching(@"warning IL\d\d\d\d");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net6Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_analysis_warnings_are_enabled_by_default(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}")
                .Should().Pass()
                // trim analysis warnings are enabled
                .And.HaveStdOutMatching("warning IL2075.*Program.IL_2075")
                .And.HaveStdOutMatching("warning IL2026.*Program.IL_2026.*Testing analysis warning IL2026")
                .And.HaveStdOutMatching("warning IL2043.*Program.IL_2043.get")
                .And.HaveStdOutMatching("warning IL2046.*Program.Derived.IL_2046")
                .And.HaveStdOutMatching("warning IL2093.*Program.Derived.IL_2093");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_accepts_option_to_enable_analysis_warnings(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            testProject.AdditionalProperties["SuppressTrimAnalysisWarnings"] = "false";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}")
                .Should().Pass()
                .And.HaveStdOutMatching("warning IL2075.*Program.IL_2075")
                .And.HaveStdOutMatching("warning IL2026.*Program.IL_2026.*Testing analysis warning IL2026")
                .And.HaveStdOutMatching("warning IL2043.*Program.IL_2043.get")
                .And.HaveStdOutMatching("warning IL2046.*Program.Derived.IL_2046")
                .And.HaveStdOutMatching("warning IL2093.*Program.Derived.IL_2093");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_accepts_option_to_disable_analysis_warnings(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            testProject.AdditionalProperties["SuppressTrimAnalysisWarnings"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}")
                .Should().Pass()
                .And.NotHaveStdOutContaining("warning IL2075")
                .And.NotHaveStdOutContaining("warning IL2026")
                .And.NotHaveStdOutContaining("warning IL2043")
                .And.NotHaveStdOutContaining("warning IL2046")
                .And.NotHaveStdOutContaining("warning IL2093");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_accepts_option_to_enable_analysis_warnings_without_PublishTrimmed(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["EnableTrimAnalyzer"] = "true";
            testProject.AdditionalProperties["EnableNETAnalyzers"] = "false";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}")
                .Should().Pass()
                .And.HaveStdOutMatching("warning IL2026.*Program.IL_2026.*Testing analysis warning IL2026");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_shows_single_warning_for_packagereferences_only(string targetFramework)
        {
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testAssetName = "TrimmedAppWithReferences";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName, identifier: targetFramework)
                .WithSource();

            var publishCommand = new PublishCommand(testAsset, "App");
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}")
                .Should().Pass()
                .And.HaveStdOutMatching("IL2026: App.Program.Main.*Program.RUC")
                .And.HaveStdOutMatching("IL2026: ProjectReference.ProjectReferenceLib.Method.*ProjectReferenceLib.RUC")
                .And.HaveStdOutMatching("IL2026: TransitiveProjectReference.TransitiveProjectReferenceLib.Method.*TransitiveProjectReferenceLib.RUC")
                .And.NotHaveStdOutMatching("IL2026:.*PackageReference.PackageReferenceLib")
                .And.HaveStdOutMatching("IL2104.*'PackageReference'")
                .And.NotHaveStdOutMatching("IL2104.*'App'")
                .And.NotHaveStdOutMatching("IL2104.*'ProjectReference'")
                .And.NotHaveStdOutMatching("IL2104.*'TransitiveProjectReference'");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_accepts_option_to_show_all_warnings(string targetFramework)
        {
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testAssetName = "TrimmedAppWithReferences";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName, identifier: targetFramework)
                .WithSource();

            var publishCommand = new PublishCommand(testAsset, "App");
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:TrimmerSingleWarn=false")
                .Should().Pass()
                .And.HaveStdOutMatching("IL2026: App.Program.Main.*Program.RUC")
                .And.HaveStdOutMatching("IL2026: ProjectReference.ProjectReferenceLib.Method.*ProjectReferenceLib.RUC")
                .And.HaveStdOutMatching("IL2026: TransitiveProjectReference.TransitiveProjectReferenceLib.Method.*TransitiveProjectReferenceLib.RUC")
                .And.HaveStdOutMatching("IL2026:.*PackageReference.PackageReferenceLib")
                .And.NotHaveStdOutContaining("IL2104");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_can_show_single_warning_per_assembly(string targetFramework)
        {
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testAssetName = "TrimmedAppWithReferences";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName, identifier: targetFramework)
                .WithSource()
                .WithProjectChanges(project => {
                    SetMetadata(project, "PackageReference", "TrimmerSingleWarn", "false");
                    SetMetadata(project, "ProjectReference", "TrimmerSingleWarn", "true");
                    SetMetadata(project, "App", "TrimmerSingleWarn", "true");
                });

            var publishCommand = new PublishCommand(testAsset, "App");
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:TrimmerSingleWarn=false")
                .Should().Pass()
                .And.NotHaveStdOutMatching("IL2026: App.Program.Main.*Program.RUC")
                .And.NotHaveStdOutMatching("IL2026: ProjectReference.ProjectReferenceLib.Method.*ProjectReferenceLib.RUC")
                .And.HaveStdOutMatching("IL2026: TransitiveProjectReference.TransitiveProjectReferenceLib.Method.*TransitiveProjectReferenceLib.RUC")
                .And.HaveStdOutMatching("IL2026:.*PackageReference.PackageReferenceLib")
                .And.NotHaveStdOutMatching("IL2104.*'PackageReference'")
                .And.HaveStdOutMatching("IL2104.*'App'")
                .And.HaveStdOutMatching("IL2104.*'ProjectReference'")
                .And.NotHaveStdOutMatching("IL2104.*'TransitiveProjectReference'");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_errors_fail_the_build(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            // Set up a project with an invalid feature substitution, just to produce an error.
            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName);
            testProject.SourceFiles[$"{projectName}.xml"] = $@"
<linker>
  <assembly fullname=""{projectName}"">
    <type fullname=""Program"" feature=""featuremissingvalue"" />
  </assembly>
</linker>
";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => AddRootDescriptor(project, $"{projectName}.xml"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:SuppressTrimAnalysisWarnings=false")
                .Should().Fail()
                .And.HaveStdOutContaining("error IL1001")
                .And.HaveStdOutContaining(Strings.ILLinkFailed);

            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateLinkDir = Path.Combine(intermediateDirectory, "linked");

            var linkSemaphore = Path.Combine(intermediateLinkDir, "Link.semaphore");
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");

            File.Exists(linkSemaphore).Should().BeFalse();
            File.Exists(publishedDll).Should().BeFalse();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_verify_analysis_warnings_hello_world_app_trim_mode_copyused(string targetFramework)
        {
            var projectName = "AnalysisWarningsOnHelloWorldApp";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            // Please keep list below sorted and de-duplicated
            var expectedOutput = new string[] {
                "ILLink : Trim analysis warning IL2026: Internal.Runtime.InteropServices.ComponentActivator.GetFunctionPointer(IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr",
                "ILLink : Trim analysis warning IL2026: Internal.Runtime.InteropServices.InMemoryAssemblyLoader.LoadInMemoryAssembly(IntPtr, IntPtr",
                "ILLink : Trim analysis warning IL2026: System.ComponentModel.Design.DesigntimeLicenseContextSerializer.DeserializeUsingBinaryFormatter(DesigntimeLicenseContextSerializer.StreamWrapper, String, RuntimeLicenseContext",
                "ILLink : Trim analysis warning IL2026: System.Resources.ManifestBasedResourceGroveler.CreateResourceSet(Stream, Assembly",
                "ILLink : Trim analysis warning IL2026: System.StartupHookProvider.ProcessStartupHooks(",
                "ILLink : Trim analysis warning IL2063: System.RuntimeType.GetInterface(String, Boolean",
                "ILLink : Trim analysis warning IL2065: System.Runtime.Serialization.FormatterServices.InternalGetSerializableMembers(Type",
            };

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            var result = publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:TrimMode=copyused", "/p:TrimmerSingleWarn=false");
            result.Should().Pass();
            ValidateWarningsOnHelloWorldApp(publishCommand, result, expectedOutput, targetFramework, rid);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_verify_analysis_warnings_hello_world_app_trim_mode_link(string targetFramework)
        {
            var projectName = "AnalysisWarningsOnHelloWorldApp";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            var result = publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:TrimmerSingleWarn=false");
            result.Should().Pass();
            ValidateWarningsOnHelloWorldApp(publishCommand, result, Array.Empty<string>(), targetFramework, rid);
        }

        private void ValidateWarningsOnHelloWorldApp (PublishCommand publishCommand, CommandResult result, string[] expectedOutput, string targetFramework, string rid)
        {
            // This checks that there are no unexpected warnings, but does not cause failures for missing expected warnings.
            var warnings = result.StdOut.Split('\n', '\r').Where(line => line.Contains("warning IL"));
            var extraWarnings = warnings.Where(warning => !expectedOutput.Any(expected => warning.Contains(expected)));

            StringBuilder errorMessage = new StringBuilder();

            if (extraWarnings.Any())
            {
                // Print additional information to recognize which framework assemblies are being used.
                errorMessage.AppendLine($"Target framework from test: {targetFramework}");
                errorMessage.AppendLine($"Runtime identifier: {rid}");

                // Get the array of runtime assemblies inside the publish folder.
                string[] runtimeAssemblies = Directory.GetFiles(publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName, "*.dll");
                var paths = new List<string>(runtimeAssemblies);
                var resolver = new PathAssemblyResolver(paths);
                var mlc = new MetadataLoadContext(resolver, "System.Private.CoreLib");
                using (mlc)
                {
                    Assembly assembly = mlc.LoadFromAssemblyPath(Path.Combine(publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName, "System.Private.CoreLib.dll"));
                    string assemblyVersionInfo = (string)assembly.CustomAttributes.Where(ca => ca.AttributeType.Name == "AssemblyInformationalVersionAttribute").Select(ca => ca.ConstructorArguments[0].Value).FirstOrDefault();
                    errorMessage.AppendLine($"Runtime Assembly Informational Version: {assemblyVersionInfo}");
                }
                errorMessage.AppendLine($"The execution of a hello world app generated a diff in the number of warnings the app produces{Environment.NewLine}");
                errorMessage.AppendLine("Test output contained the following extra linker warnings:");
                foreach (var extraWarning in extraWarnings)
                    errorMessage.AppendLine($"+ {extraWarning}");
            }
            Assert.True(!extraWarnings.Any(), errorMessage.ToString());
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void TrimmingOptions_are_defaulted_correctly_on_trimmed_apps(string targetFramework)
        {
            var projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: projectName + targetFramework);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true")
                .Should().Pass();

            string outputDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            string runtimeConfigFile = Path.Combine(outputDirectory, $"{projectName}.runtimeconfig.json");
            string runtimeConfigContents = File.ReadAllText(runtimeConfigFile);


            if (Version.TryParse(targetFramework.TrimStart("net".ToCharArray()), out Version parsedVersion) &&
                parsedVersion.Major >= 6)
            {
                JObject runtimeConfig = JObject.Parse(runtimeConfigContents);
                JToken configProperties = runtimeConfig["runtimeOptions"]["configProperties"];
                configProperties["Microsoft.Extensions.DependencyInjection.VerifyOpenGenericServiceTrimmability"].Value<bool>()
                    .Should().BeTrue();
                configProperties["System.ComponentModel.TypeConverter.EnableUnsafeBinaryFormatterInDesigntimeLicenseContextSerialization"].Value<bool>()
                    .Should().BeFalse();
                configProperties["System.Resources.ResourceManager.AllowCustomResourceTypes"].Value<bool>()
                    .Should().BeFalse();
                configProperties["System.Runtime.InteropServices.BuiltInComInterop.IsSupported"].Value<bool>()
                    .Should().BeFalse();
                configProperties["System.Runtime.InteropServices.EnableConsumingManagedCodeFromNativeHosting"].Value<bool>()
                    .Should().BeFalse();
                configProperties["System.Runtime.InteropServices.EnableCppCLIHostActivation"].Value<bool>()
                    .Should().BeFalse();
                configProperties["System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization"].Value<bool>()
                    .Should().BeFalse();
                configProperties["System.StartupHookProvider.IsSupported"].Value<bool>()
                    .Should().BeFalse();
                configProperties["System.Text.Encoding.EnableUnsafeUTF7Encoding"].Value<bool>()
                    .Should().BeFalse();
                configProperties["System.Threading.Thread.EnableAutoreleasePool"].Value<bool>()
                    .Should().BeFalse();
            }
            else
            {
                runtimeConfigContents.Should().NotContain("Microsoft.Extensions.DependencyInjection.VerifyOpenGenericServiceTrimmability");
                runtimeConfigContents.Should().NotContain("System.ComponentModel.TypeConverter.EnableUnsafeBinaryFormatterInDesigntimeLicenseContextSerialization");
                runtimeConfigContents.Should().NotContain("System.Resources.ResourceManager.AllowCustomResourceTypes");
                runtimeConfigContents.Should().NotContain("System.Runtime.InteropServices.BuiltInComInterop.IsSupported");
                runtimeConfigContents.Should().NotContain("System.Runtime.InteropServices.EnableConsumingManagedCodeFromNativeHosting");
                runtimeConfigContents.Should().NotContain("System.Runtime.InteropServices.EnableCppCLIHostActivation");
                runtimeConfigContents.Should().NotContain("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization");
                runtimeConfigContents.Should().NotContain("System.StartupHookProvider.IsSupported");
                runtimeConfigContents.Should().NotContain("System.Text.Encoding.EnableUnsafeUTF7Encoding");
                runtimeConfigContents.Should().NotContain("System.Threading.Thread.EnableAutoreleasePool");
            }
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_accepts_root_descriptor(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project))
                .WithProjectChanges(project => AddRootDescriptor(project, $"{referenceProjectName}.xml"));

            var publishCommand = new PublishCommand(testAsset);
            // Inject extra arguments to prevent the linker from
            // keeping the entire referenceProject assembly. The
            // linker by default runs in a conservative mode that
            // keeps all used assemblies, but in this case we want to
            // check whether the root descriptor actually roots only
            // the specified method.
            var extraArgs = $"--action link {referenceProjectName}";
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true",
                                   $"/p:_ExtraTrimmerArgs={extraArgs}", "/v:n").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            // With root descriptor, linker keeps specified roots but removes unused methods
            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(unusedDll).Should().BeTrue();
            DoesImageHaveMethod(unusedDll, "UnusedMethod").Should().BeFalse();
            DoesImageHaveMethod(unusedDll, "UnusedMethodToRoot").Should().BeTrue();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("_TrimmerBeforeFieldInit")]
        [InlineData("_TrimmerOverrideRemoval")]
        [InlineData("_TrimmerUnreachableBodies")]
        [InlineData("_TrimmerUnusedInterfaces")]
        [InlineData("_TrimmerIPConstProp")]
        [InlineData("_TrimmerSealer")]
        public void ILLink_error_on_nonboolean_optimization_flag(string property)
        {
            var projectName = "HelloWorld";
            var targetFramework = "net5.0";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: property);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", $"/p:{property}=NonBool")
                .Should().Fail().And.HaveStdOutContaining("MSB4030");
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void ILLink_respects_feature_settings_from_host_config()
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var targetFramework = "net5.0";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName,
                // Reference the classlib to ensure its XML is processed.
                addAssemblyReference: true,
                // Set up a conditional feature substitution for the "FeatureDisabled" property
                modifyReferencedProject: (referencedProject) => AddFeatureDefinition(referencedProject, referenceProjectName));
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project))
                // Set a matching RuntimeHostConfigurationOption, with Trim = "true"
                .WithProjectChanges(project => AddRuntimeConfigOption(project, trim: true))
                .WithProjectChanges(project => AddRootDescriptor(project, $"{referenceProjectName}.xml"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true",
                                    $"/p:_ExtraTrimmerArgs=--action link {referenceProjectName}").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var referenceDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            File.Exists(referenceDll).Should().BeTrue();
            DoesImageHaveMethod(referenceDll, "FeatureAPI").Should().BeTrue();
            DoesImageHaveMethod(referenceDll, "get_FeatureDisabled").Should().BeFalse();
            // Check that this method is removed when the feature is disabled
            DoesImageHaveMethod(referenceDll, "FeatureImplementation").Should().BeFalse();
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void ILLink_ignores_host_config_settings_with_link_false()
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var targetFramework = "net5.0";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName,
                // Reference the classlib to ensure its XML is processed.
                addAssemblyReference: true,
                // Set up a conditional feature substitution for the "FeatureDisabled" property
                modifyReferencedProject: (referencedProject) => AddFeatureDefinition(referencedProject, referenceProjectName));
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project))
                // Set a matching RuntimeHostConfigurationOption, with Trim = "false"
                .WithProjectChanges(project => AddRuntimeConfigOption(project, trim: false))
                .WithProjectChanges(project => AddRootDescriptor(project, $"{referenceProjectName}.xml"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true",
                                    $"/p:_ExtraTrimmerArgs=--action link {referenceProjectName}").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var referenceDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            File.Exists(referenceDll).Should().BeTrue();
            DoesImageHaveMethod(referenceDll, "FeatureAPI").Should().BeTrue();
            DoesImageHaveMethod(referenceDll, "get_FeatureDisabled").Should().BeTrue();
            // Check that the feature substitution did not apply
            DoesImageHaveMethod(referenceDll, "FeatureImplementation").Should().BeTrue();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_runs_incrementally(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);

            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateLinkDir = Path.Combine(intermediateDirectory, "linked");

            var linkSemaphore = Path.Combine(intermediateLinkDir, "Link.semaphore");

            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();
            DateTime semaphoreFirstModifiedTime = File.GetLastWriteTimeUtc(linkSemaphore);

            WaitForUtcNowToAdvance();

            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();
            DateTime semaphoreSecondModifiedTime = File.GetLastWriteTimeUtc(linkSemaphore);

            semaphoreFirstModifiedTime.Should().Be(semaphoreSecondModifiedTime);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("netcoreapp3.1")]
        [InlineData("net5.0")]
        [InlineData("net6.0")]
        public void ILLink_old_defaults_keep_nonframework(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute("/v:n", $"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            Directory.Exists(linkedDirectory).Should().BeTrue();

            var linkedDll = Path.Combine(linkedDirectory, $"{projectName}.dll");
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");
            var unusedFrameworkDll = Path.Combine(publishDirectory, $"{unusedFrameworkAssembly}.dll");

            File.Exists(linkedDll).Should().BeTrue();
            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(unusedDll).Should().BeTrue();
            File.Exists(unusedFrameworkDll).Should().BeFalse();

            var depsFile = Path.Combine(publishDirectory, $"{projectName}.deps.json");
            DoesDepsFileHaveAssembly(depsFile, projectName).Should().BeTrue();
            DoesDepsFileHaveAssembly(depsFile, referenceProjectName).Should().BeTrue();
            DoesDepsFileHaveAssembly(depsFile, unusedFrameworkAssembly).Should().BeFalse();
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void ILLink_net7_defaults_trim_nonframework()
        {
            string targetFramework = "net7.0";
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute("/v:n", $"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            Directory.Exists(linkedDirectory).Should().BeTrue();

            var linkedDll = Path.Combine(linkedDirectory, $"{projectName}.dll");
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");
            var unusedFrameworkDll = Path.Combine(publishDirectory, $"{unusedFrameworkAssembly}.dll");

            File.Exists(linkedDll).Should().BeTrue();
            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(unusedDll).Should().BeFalse();
            File.Exists(unusedFrameworkDll).Should().BeFalse();

            var depsFile = Path.Combine(publishDirectory, $"{projectName}.deps.json");
            DoesDepsFileHaveAssembly(depsFile, projectName).Should().BeTrue();
            DoesDepsFileHaveAssembly(depsFile, referenceProjectName).Should().BeFalse();
            DoesDepsFileHaveAssembly(depsFile, unusedFrameworkAssembly).Should().BeFalse();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_does_not_include_leftover_artifacts_on_second_run(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName, referenceProjectIdentifier: targetFramework);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project))
                .WithProjectChanges(project => AddRootDescriptor(project, $"{referenceProjectName}.xml"));

            var publishCommand = new PublishCommand(testAsset);

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateLinkDir = Path.Combine(intermediateDirectory, "linked");
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            var linkSemaphore = Path.Combine(intermediateLinkDir, "Link.semaphore");

            // Link, keeping classlib
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();
            DateTime semaphoreFirstModifiedTime = File.GetLastWriteTimeUtc(linkSemaphore);

            var publishedDllKeptFirstTimeOnly = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");
            var linkedDllKeptFirstTimeOnly = Path.Combine(linkedDirectory, $"{referenceProjectName}.dll");
            File.Exists(linkedDllKeptFirstTimeOnly).Should().BeTrue();
            File.Exists(publishedDllKeptFirstTimeOnly).Should().BeTrue();

            // Delete kept dll from publish output (works around lack of incremental publish)
            File.Delete(publishedDllKeptFirstTimeOnly);

            // Remove root descriptor to change the linker behavior.
            WaitForUtcNowToAdvance();
            // File.SetLastWriteTimeUtc(Path.Combine(testAsset.TestRoot, testProject.Name, $"{projectName}.cs"), DateTime.UtcNow);
            testAsset = testAsset.WithProjectChanges(project => RemoveRootDescriptor(project));

            // Link, discarding classlib
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();
            DateTime semaphoreSecondModifiedTime = File.GetLastWriteTimeUtc(linkSemaphore);

            // Check that the linker actually ran again
            semaphoreFirstModifiedTime.Should().NotBe(semaphoreSecondModifiedTime);

            File.Exists(linkedDllKeptFirstTimeOnly).Should().BeFalse();
            File.Exists(publishedDllKeptFirstTimeOnly).Should().BeFalse();

            // "linked" intermediate directory does not pollute the publish output
            Directory.Exists(Path.Combine(publishDirectory, "linked")).Should().BeFalse();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_keeps_symbols_by_default(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            var intermediatePdb = Path.Combine(intermediateDirectory, $"{projectName}.pdb");
            var linkedPdb = Path.Combine(linkedDirectory, $"{projectName}.pdb");
            var publishedPdb = Path.Combine(publishDirectory, $"{projectName}.pdb");

            File.Exists(linkedPdb).Should().BeTrue();

            var intermediatePdbSize = new FileInfo(intermediatePdb).Length;
            var linkedPdbSize = new FileInfo(linkedPdb).Length;
            var publishPdbSize = new FileInfo(publishedPdb).Length;

            linkedPdbSize.Should().BeLessThan(intermediatePdbSize);
            publishPdbSize.Should().Be(linkedPdbSize);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_removes_symbols_when_debugger_support_is_disabled(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:DebuggerSupport=false").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            var intermediatePdb = Path.Combine(intermediateDirectory, $"{projectName}.pdb");
            var linkedPdb = Path.Combine(linkedDirectory, $"{projectName}.pdb");
            var publishedPdb = Path.Combine(publishDirectory, $"{projectName}.pdb");

            File.Exists(intermediatePdb).Should().BeTrue();
            File.Exists(linkedPdb).Should().BeFalse();
            File.Exists(publishedPdb).Should().BeFalse();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_accepts_option_to_remove_symbols(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:TrimmerRemoveSymbols=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            var intermediatePdb = Path.Combine(intermediateDirectory, $"{projectName}.pdb");
            var linkedPdb = Path.Combine(linkedDirectory, $"{projectName}.pdb");
            var publishedPdb = Path.Combine(publishDirectory, $"{projectName}.pdb");

            File.Exists(intermediatePdb).Should().BeTrue();
            File.Exists(linkedPdb).Should().BeFalse();
            File.Exists(publishedPdb).Should().BeFalse();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_symbols_option_can_override_defaults_from_debugger_support(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true",
                                    "/p:DebuggerSupport=false", "/p:TrimmerRemoveSymbols=false").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            var intermediatePdb = Path.Combine(intermediateDirectory, $"{projectName}.pdb");
            var linkedPdb = Path.Combine(linkedDirectory, $"{projectName}.pdb");
            var publishedPdb = Path.Combine(publishDirectory, $"{projectName}.pdb");

            File.Exists(linkedPdb).Should().BeTrue();

            var intermediatePdbSize = new FileInfo(intermediatePdb).Length;
            var linkedPdbSize = new FileInfo(linkedPdb).Length;
            var publishPdbSize = new FileInfo(publishedPdb).Length;

            linkedPdbSize.Should().BeLessThan(intermediatePdbSize);
            publishPdbSize.Should().Be(linkedPdbSize);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_can_treat_warnings_as_errors(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:SuppressTrimAnalysisWarnings=false",
                                    "/p:WarningsAsErrors=IL2075")
                .Should().Fail()
                .And.HaveStdOutContaining("error IL2075")
                .And.HaveStdOutContaining("warning IL2026");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_can_treat_warnings_not_as_errors(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["WarningsNotAsErrors"] = "IL2026;IL2046;IL2075";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:SuppressTrimAnalysisWarnings=false",
                                    "/p:TreatWarningsAsErrors=true", "/p:EnableTrimAnalyzer=false")
                .Should().Fail()
                .And.HaveStdOutContaining("warning IL2026")
                .And.HaveStdOutContaining("warning IL2046")
                .And.HaveStdOutContaining("warning IL2075")
                .And.HaveStdOutContaining("error IL2043")
                .And.NotHaveStdOutContaining("error IL2026")
                .And.NotHaveStdOutContaining("error IL2046")
                .And.NotHaveStdOutContaining("error IL2075")
                .And.NotHaveStdOutContaining("warning IL2043");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_can_ignore_warnings(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:SuppressTrimAnalysisWarnings=false",
                                    "/p:NoWarn=IL2075", "/p:WarnAsError=IL2075")
                .Should().Pass()
                .And.NotHaveStdOutContaining("warning IL2075")
                .And.NotHaveStdOutContaining("error IL2075")
                .And.HaveStdOutContaining("warning IL2026");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_respects_analysis_level(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:SuppressTrimAnalysisWarnings=false",
                                    "/p:AnalysisLevel=0.0")
                .Should().Pass().And.NotHaveStdOutMatching(@"warning IL\d\d\d\d");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_respects_warning_level_independently(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:SuppressTrimAnalysisWarnings=false",
                                    // This tests the linker only. The analyzer doesn't respect ILLinkWarningLevel.
                                    "/p:EnableTrimAnalyzer=false",
                                    "/p:ILLinkWarningLevel=0")
                .Should().Pass()
                .And.NotHaveStdOutContaining("warning IL2075");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_can_treat_warnings_as_errors_independently(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:SuppressTrimAnalysisWarnings=false",
                                    "/p:TreatWarningsAsErrors=true", "/p:ILLinkTreatWarningsAsErrors=false", "/p:EnableTrimAnalyzer=false")
                .Should().Pass()
                .And.HaveStdOutContaining("warning IL2026")
                .And.HaveStdOutContaining("warning IL2046")
                .And.HaveStdOutContaining("warning IL2075")
                .And.NotHaveStdOutContaining("error IL2026")
                .And.NotHaveStdOutContaining("error IL2046")
                .And.NotHaveStdOutContaining("error IL2075");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_error_on_portable_app(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute("/p:PublishTrimmed=true")
                .Should().Fail()
                .And.HaveStdOutContaining(Strings.ILLinkNotSupportedError);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("net5.0")]
        [InlineData("netcoreapp3.1")]
        public void ILLink_displays_informational_warning_up_to_net5_by_default(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute("/p:PublishTrimmed=true", $"/p:RuntimeIdentifier={rid}")
                .Should().Pass().And.HaveStdOutContainingIgnoreCase("https://aka.ms/dotnet-illink");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net6Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_displays_informational_warning_when_trim_analysis_warnings_are_suppressed_on_net6plus(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute("/p:PublishTrimmed=true", $"/p:RuntimeIdentifier={rid}", "/p:SuppressTrimAnalysisWarnings=true")
                .Should().Pass().And.HaveStdOutContainingIgnoreCase("https://aka.ms/dotnet-illink")
                .And.HaveStdOutContainingIgnoreCase("This process might take a while");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net6Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_dont_display_informational_warning_by_default_on_net6plus(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute("/p:PublishTrimmed=true", $"/p:RuntimeIdentifier={rid}")
                .Should().Pass().And.NotHaveStdErrContaining("https://aka.ms/dotnet-illink")
                .And.HaveStdOutContainingIgnoreCase("This process might take a while");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_dont_display_time_awareness_message_on_incremental_build(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute("/p:PublishTrimmed=true", $"/p:RuntimeIdentifier={rid}")
                .Should().Pass().And.HaveStdOutContainingIgnoreCase("This process might take a while");

            publishCommand.Execute("/p:PublishTrimmed=true", $"/p:RuntimeIdentifier={rid}")
                .Should().Pass().And.NotHaveStdErrContaining("This process might take a while");
        }

        [Fact()]
        public void ILLink_and_crossgen_process_razor_assembly()
        {
            var targetFramework = "netcoreapp3.0";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = new TestProject
            {
                Name = "TestWeb",
                IsExe = true,
                ProjectSdk = "Microsoft.NET.Sdk.Web",
                TargetFrameworks = targetFramework,
                SourceFiles =
                {
                    ["Program.cs"] = @"
                        class Program
                        {
                            static void Main() {}
                        }",
                    ["Test.cshtml"] = @"
                        @page
                        @{
                            System.IO.Compression.ZipFile.OpenRead(""test.zip"");
                        }
                    ",
                },
                AdditionalProperties =
                {
                    ["RuntimeIdentifier"] = rid,
                    ["PublishTrimmed"] = "true",
                    ["PublishReadyToRun"] = "true",
                }
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var publishDir = publishCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: rid);
            publishDir.Should().HaveFile("System.IO.Compression.ZipFile.dll");
            GivenThatWeWantToPublishReadyToRun.DoesImageHaveR2RInfo(publishDir.File("TestWeb.Views.dll").FullName);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, true)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, false)]
        public void Build_respects_IsTrimmable_property(string targetFramework, bool isExe)
        {
            var projectName = "AnalysisWarnings";

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName, isExe);
            testProject.AdditionalProperties["IsTrimmable"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework + isExe);

            var buildCommand = new BuildCommand(testAsset);
            // IsTrimmable enables analysis warnings during build
            buildCommand.Execute()
                .Should().Pass()
                .And.HaveStdOutMatching("warning IL2026.*Program.IL_2026.*Testing analysis warning IL2026");

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework).FullName;
            var assemblyPath = Path.Combine(outputDirectory, $"{projectName}.dll");
            var runtimeConfigPath = Path.Combine(outputDirectory, $"{projectName}.runtimeconfig.json");

            // injects the IsTrimmable attribute
            AssemblyInfo.Get(assemblyPath)["AssemblyMetadataAttribute"].Should().Be("IsTrimmable:True");

            // just setting IsTrimmable doesn't enable feature settings
            // (these only affect apps, and wouldn't make sense for libraries either)
            if (isExe) {
                JObject runtimeConfig = JObject.Parse(File.ReadAllText(runtimeConfigPath));
                JToken configProperties = runtimeConfig["runtimeOptions"]["configProperties"];
                if (configProperties != null)
                    configProperties["System.StartupHookProvider.IsSupported"].Should().BeNull();
            }
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void Build_respects_PublishTrimmed_property(string targetFramework)
        {
            var projectName = "AnalysisWarnings";

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            // PublishTrimmed enables analysis warnings during build
            buildCommand.Execute()
                .Should().Pass()
                .And.HaveStdOutMatching("warning IL2026.*Program.IL_2026.*Testing analysis warning IL2026");

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework).FullName;
            var assemblyPath = Path.Combine(outputDirectory, $"{projectName}.dll");
            var runtimeConfigPath = Path.Combine(outputDirectory, $"{projectName}.runtimeconfig.json");

            // runtimeconfig has trim settings
            JObject runtimeConfig = JObject.Parse(File.ReadAllText(runtimeConfigPath));
            JToken startupHookSupport = runtimeConfig["runtimeOptions"]["configProperties"]["System.StartupHookProvider.IsSupported"];
            startupHookSupport.Value<bool>().Should().BeFalse();

            // just setting PublishTrimmed doesn't inject the IsTrimmable attribute
            AssemblyInfo.Get(assemblyPath).ContainsKey("AssemblyMetadataAttribute").Should().BeFalse();
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

        private TestAsset GetProjectReference(TestProject project, string callingMethod, string identifier)
        {
            var asset = _testAssetsManager.CreateTestProject(project, callingMethod: callingMethod, identifier: identifier);
            return asset;
        }

        private void AddRootDescriptor(XDocument project, string rootDescriptorFileName)
        {
            var ns = project.Root.Name.Namespace;

            var itemGroup = new XElement(ns + "ItemGroup");
            project.Root.Add(itemGroup);
            itemGroup.Add(new XElement(ns + "TrimmerRootDescriptor",
                                       new XAttribute("Include", rootDescriptorFileName)));
        }

        private void RemoveRootDescriptor(XDocument project)
        {
            var ns = project.Root.Name.Namespace;

            project.Root.Elements(ns + "ItemGroup")
                .Where(ig => ig.Elements(ns + "TrimmerRootDescriptor").Any())
                .First().Remove();
        }

        [Fact]
        public void It_warns_when_targetting_netcoreapp_2_x_illink()
        {
            var testProject = new TestProject()
            {
                Name = "ConsoleApp",
                TargetFrameworks = "netcoreapp2.2",
                IsExe = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: "GivenThatWeWantToRunILLink");

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute($"/p:PublishTrimmed=true")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(Strings.PublishTrimmedRequiresVersion30);
        }

        private void SetMetadata(XDocument project, string assemblyName, string key, string value)
        {
            var ns = project.Root.Name.Namespace;
            var targetName = "SetTrimmerMetadata";
            var target = project.Root.Elements(ns + "Target")
                .Where(e => e.Attribute("Name")?.Value == targetName)
                .FirstOrDefault();

            if (target == null) {
                target = new XElement(ns + "Target",
                    new XAttribute("BeforeTargets", "PrepareForILLink"),
                    new XAttribute("Name", targetName));
                project.Root.Add(target);
            }

            target.Add(new XElement(ns + "ItemGroup",
                new XElement("ManagedAssemblyToLink",
                    new XAttribute("Condition", $"'%(FileName)' == '{assemblyName}'"),
                    new XAttribute(key, value))));
        }

        private void SetGlobalTrimMode(XDocument project, string trimMode)
        {
            var ns = project.Root.Name.Namespace;

            var properties = new XElement(ns + "PropertyGroup");
            project.Root.Add(properties);
            properties.Add(new XElement(ns + "TrimMode",
                                        trimMode));
        }

        private void SetTrimmerDefaultAction(XDocument project, string action)
        {
            var ns = project.Root.Name.Namespace;

            var properties = new XElement(ns + "PropertyGroup");
            project.Root.Add(properties);
            properties.Add(new XElement(ns + "TrimmerDefaultAction", action));
        }

        private void EnableNonFrameworkTrimming(XDocument project)
        {
            // Used to override the default linker options for testing
            // purposes. The default roots non-framework assemblies,
            // but we want to ensure that the linker is running
            // end-to-end by checking that it strips code from our
            // test projects.
            SetGlobalTrimMode(project, "link");
            var ns = project.Root.Name.Namespace;

            var target = new XElement(ns + "Target",
                                      new XAttribute("BeforeTargets", "PrepareForILLink"),
                                      new XAttribute("Name", "_EnableNonFrameworkTrimming"));
            project.Root.Add(target);
            var items = new XElement(ns + "ItemGroup");
            target.Add(items);
            items.Add(new XElement("ManagedAssemblyToLink",
                                   new XElement("Condition", "true"),
                                   new XElement("IsTrimmable", "true")));
            items.Add(new XElement(ns + "TrimmerRootAssembly",
                                   new XAttribute("Include", "@(IntermediateAssembly->'%(FullPath)')")));
        }

        static readonly string substitutionsFilename = "ILLink.Substitutions.xml";

        private void AddFeatureDefinition(TestProject testProject, string assemblyName)
        {
            // Add a feature definition that replaces the FeatureDisabled property when DisableFeature is true.
            testProject.EmbeddedResources[substitutionsFilename] = $@"
<linker>
  <assembly fullname=""{assemblyName}"" feature=""DisableFeature"" featurevalue=""true"">
    <type fullname=""ClassLib"">
      <method signature=""System.Boolean get_FeatureDisabled()"" body=""stub"" value=""true"" />
    </type>
  </assembly>
</linker>
";
            
            testProject.AddItem("EmbeddedResource", new Dictionary<string, string> {
                ["Include"] = substitutionsFilename,
                ["LogicalName"] = substitutionsFilename
            });
        }

        private void AddRuntimeConfigOption(XDocument project, bool trim)
        {
            var ns = project.Root.Name.Namespace;

            project.Root.Add(new XElement(ns + "ItemGroup",
                                new XElement("RuntimeHostConfigurationOption",
                                    new XAttribute("Include", "DisableFeature"),
                                    new XAttribute("Value", "true"),
                                    new XAttribute("Trim", trim.ToString()))));
        }

        private TestProject CreateTestProjectWithAnalysisWarnings(string targetFramework, string projectName, bool isExe = true)
        {
            var testProject = new TestProject()
            {
                Name = projectName,
                TargetFrameworks = targetFramework,
                IsExe = isExe
            };
            
            testProject.SourceFiles[$"{projectName}.cs"] = @"
using System;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
public class Program
{
    public static void Main()
    {
        IL_2075();
        IL_2026();
        _ = IL_2043;
        new Derived().IL_2046();
        new Derived().IL_2093();
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)]
    public static string typeName;

    public static void IL_2075()
    {
        _ = Type.GetType(typeName).GetMethod(""SomeMethod"");
    }

    [RequiresUnreferencedCode(""Testing analysis warning IL2026"")]
    public static void IL_2026()
    {
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)]
    public static string IL_2043 {
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)]
        get => null;
    }

    public class Base
    {
        [RequiresUnreferencedCode(""Testing analysis warning IL2046"")]
        public virtual void IL_2046() {}

        public virtual string IL_2093() => null;
    }

    public class Derived : Base
    {
        public override void IL_2046() {}

        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
        public override string IL_2093() => null;
    }
}
";

            return testProject;
        }

        private TestProject CreateTestProjectWithIsTrimmableAttributes (
            string targetFramework,
            string projectName)
        {
            var testProject = new TestProject()
            {
                Name = projectName,
                TargetFrameworks = targetFramework,
                IsExe = true
            };
            testProject.AdditionalProperties["PublishTrimmed"] = "true";

            testProject.SourceFiles[$"{projectName}.cs"] = @"
using System;
public class Program
{
    public static void Main()
    {
        TrimmableAssembly.UsedMethod();
        NonTrimmableAssembly.UsedMethod();
    }
}";

            var trimmableProject = new TestProject()
            {
                Name = "TrimmableAssembly",
                TargetFrameworks = targetFramework
            };

            trimmableProject.SourceFiles["TrimmableAssembly.cs"] = @"
using System.Reflection;

[assembly: AssemblyMetadata(""IsTrimmable"", ""True"")]

public static class TrimmableAssembly
{
    public static void UsedMethod()
    {
    }

    public static void UnusedMethod()
    {
    }
}";
            testProject.ReferencedProjects.Add (trimmableProject);

            var nonTrimmableProject = new TestProject()
            {
                Name = "NonTrimmableAssembly",
                TargetFrameworks = targetFramework
            };

            nonTrimmableProject.SourceFiles["NonTrimmableAssembly.cs"] = @"
public static class NonTrimmableAssembly
{
    public static void UsedMethod()
    {
    }

    public static void UnusedMethod()
    {
    }
}";
            testProject.ReferencedProjects.Add (nonTrimmableProject);

            var unusedTrimmableProject = new TestProject()
            {
                Name = "UnusedTrimmableAssembly",
                TargetFrameworks = targetFramework
            };

            unusedTrimmableProject.SourceFiles["UnusedTrimmableAssembly.cs"] = @"
using System.Reflection;

[assembly: AssemblyMetadata(""IsTrimmable"", ""True"")]

public static class UnusedTrimmableAssembly
{
    public static void UnusedMethod()
    {
    }
}
";
            testProject.ReferencedProjects.Add (unusedTrimmableProject);

            var unusedNonTrimmableProject = new TestProject()
            {
                Name = "UnusedNonTrimmableAssembly",
                TargetFrameworks = targetFramework
            };

            unusedNonTrimmableProject.SourceFiles["UnusedNonTrimmableAssembly.cs"] = @"
public static class UnusedNonTrimmableAssembly
{
    public static void UnusedMethod()
    {
    }
}
";
            testProject.ReferencedProjects.Add (unusedNonTrimmableProject);

            return testProject;
        }

        private TestProject CreateTestProjectForILLinkTesting(
            string targetFramework,
            string mainProjectName,
            string referenceProjectName = null,
            bool usePackageReference = true,
            [CallerMemberName] string callingMethod = "",
            string referenceProjectIdentifier = null,
            Action<TestProject> modifyReferencedProject = null,
            bool addAssemblyReference = false)
        {
            var testProject = new TestProject()
            {
                Name = mainProjectName,
                TargetFrameworks = targetFramework,
                IsExe = true
            };

            testProject.SourceFiles[$"{mainProjectName}.cs"] = @"
using System;
public class Program
{
    public static void Main()
    {
        Console.WriteLine(""Hello world"");
    }

    public static void UnusedMethod()
    {
    }
";

            if (addAssemblyReference)
            {
                testProject.SourceFiles[$"{mainProjectName}.cs"] += @"
    public static void UseClassLib()
    {
        ClassLib.UsedMethod();
    }
}";
            } else {
                testProject.SourceFiles[$"{mainProjectName}.cs"] += @"}";
            }

            if (referenceProjectName == null)
            {
                if (addAssemblyReference)
                    throw new ArgumentException("Adding an assembly reference requires a project to reference.");
                return testProject;
            }

            var referenceProject = new TestProject()
            {
                Name = referenceProjectName,
                // NOTE: If using a package reference for the reference project, it will be retrieved
                // from the nuget cache. Set the reference project TFM to the lowest common denominator
                // of these tests to prevent conflicts.
                TargetFrameworks = usePackageReference ? "netcoreapp3.0" : targetFramework,
            };
            referenceProject.SourceFiles[$"{referenceProjectName}.cs"] = @"
using System;

public class ClassLib
{
    public static void UsedMethod()
    {
    }

    public void UnusedMethod()
    {
    }

    public void UnusedMethodToRoot()
    {
    }

    public static bool FeatureDisabled { get; }

    public static void FeatureAPI()
    {
        if (FeatureDisabled)
            return;

        FeatureImplementation();
    }

    public static void FeatureImplementation()
    {
    }
}
";
            if (modifyReferencedProject != null)
                modifyReferencedProject(referenceProject);

            if (usePackageReference)
            {
                var referenceAsset = GetProjectReference(referenceProject, callingMethod, referenceProjectIdentifier ?? targetFramework);
                testProject.ReferencedProjects.Add(referenceAsset.TestProject);
            }
            else
            {
                testProject.ReferencedProjects.Add(referenceProject);
            }


            testProject.SourceFiles[$"{referenceProjectName}.xml"] = $@"
<linker>
  <assembly fullname=""{referenceProjectName}"">
    <type fullname=""ClassLib"">
      <method name=""UnusedMethodToRoot"" />
      <method name=""FeatureAPI"" />
    </type>
  </assembly>
</linker>
";

            return testProject;
        }
    }
}
