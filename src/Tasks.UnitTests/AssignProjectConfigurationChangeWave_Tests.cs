// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for the ChangeWave 18.6 behavior change in AssignProjectConfiguration:
    /// When project references are not found in the solution configuration blob,
    /// they should inherit the parent's Configuration and Platform rather than
    /// having them stripped via GlobalPropertiesToRemove.
    /// </summary>
    public sealed class AssignProjectConfigurationChangeWave_Tests
    {
        private readonly ITestOutputHelper _output;

        public AssignProjectConfigurationChangeWave_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Verifies that when ShouldUnsetParentConfigurationAndPlatform is true,
        /// unresolved project references do NOT get GlobalPropertiesToRemove=Configuration;Platform
        /// set on them. This ensures child projects inherit the parent's Configuration and Platform
        /// rather than falling back to their defaults (which breaks non-standard configuration names
        /// like "Debug Unicode").
        /// </summary>
        [Fact]
        public void UnresolvedReferencesDoNotStripConfigurationUnderChangeWave()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            var projectRefs = new ArrayList();
            // This reference is NOT in the solution config → will be unresolved
            var unresolvedRef = ResolveNonMSBuildProjectOutput_Tests.CreateReferenceItem(
                "Utility.vcxproj",
                "{AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA}",
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}",
                "Utility");
            projectRefs.Add(unresolvedRef);

            // Solution config only contains a DIFFERENT project
            var projectConfigurations = new Hashtable();
            projectConfigurations.Add("{BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB}", @"Debug Unicode|x64");

            string xmlString = ResolveNonMSBuildProjectOutput_Tests.CreatePregeneratedPathDoc(projectConfigurations);

            MockEngine engine = new MockEngine(_output);
            AssignProjectConfiguration task = new AssignProjectConfiguration();
            task.BuildEngine = engine;
            task.SolutionConfigurationContents = xmlString;
            task.ProjectReferences = (ITaskItem[])projectRefs.ToArray(typeof(ITaskItem));
            task.ShouldUnsetParentConfigurationAndPlatform = true;

            bool result = task.Execute();
            result.ShouldBeTrue();

            // The reference should be unresolved (not in solution config)
            task.UnassignedProjects.Length.ShouldBe(1);
            task.AssignedProjects.Length.ShouldBe(0);

            // Under ChangeWave 18.6, GlobalPropertiesToRemove should NOT include Configuration;Platform
            ITaskItem unresolved = task.UnassignedProjects[0];
            string globalPropertiesToRemove = unresolved.GetMetadata("GlobalPropertiesToRemove");
            globalPropertiesToRemove.ShouldNotContain("Configuration");
            globalPropertiesToRemove.ShouldNotContain("Platform");
        }

        /// <summary>
        /// Verifies that when ChangeWave 18.6 is disabled (opted out), unresolved references
        /// DO get GlobalPropertiesToRemove=Configuration;Platform set on them — the pre-18.6 behavior.
        /// </summary>
        [Fact]
        public void UnresolvedReferencesStripConfigurationWhenChangeWaveDisabled()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            // Disable ChangeWave 18.6 to get the old behavior
            ChangeWaves.ResetStateForTests();
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_6.ToString());
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            var projectRefs = new ArrayList();
            var unresolvedRef = ResolveNonMSBuildProjectOutput_Tests.CreateReferenceItem(
                "Utility.vcxproj",
                "{AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA}",
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}",
                "Utility");
            projectRefs.Add(unresolvedRef);

            var projectConfigurations = new Hashtable();
            projectConfigurations.Add("{BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB}", @"Debug Unicode|x64");

            string xmlString = ResolveNonMSBuildProjectOutput_Tests.CreatePregeneratedPathDoc(projectConfigurations);

            MockEngine engine = new MockEngine(_output);
            AssignProjectConfiguration task = new AssignProjectConfiguration();
            task.BuildEngine = engine;
            task.SolutionConfigurationContents = xmlString;
            task.ProjectReferences = (ITaskItem[])projectRefs.ToArray(typeof(ITaskItem));
            task.ShouldUnsetParentConfigurationAndPlatform = true;

            bool result = task.Execute();
            result.ShouldBeTrue();

            task.UnassignedProjects.Length.ShouldBe(1);
            ITaskItem unresolved = task.UnassignedProjects[0];
            string globalPropertiesToRemove = unresolved.GetMetadata("GlobalPropertiesToRemove");

            // With ChangeWave 18.6 disabled, the old behavior should apply:
            // Configuration;Platform should be appended to GlobalPropertiesToRemove
            globalPropertiesToRemove.ShouldContain("Configuration");
            globalPropertiesToRemove.ShouldContain("Platform");
        }

        /// <summary>
        /// Verifies that when ShouldUnsetParentConfigurationAndPlatform is false,
        /// unresolved references never had GlobalPropertiesToRemove set (this behavior
        /// is unchanged by the ChangeWave).
        /// </summary>
        [Fact]
        public void UnresolvedReferencesWithoutShouldUnsetDoNotStripConfiguration()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            var projectRefs = new ArrayList();
            var unresolvedRef = ResolveNonMSBuildProjectOutput_Tests.CreateReferenceItem(
                "Utility.vcxproj",
                "{AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA}",
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}",
                "Utility");
            projectRefs.Add(unresolvedRef);

            var projectConfigurations = new Hashtable();
            projectConfigurations.Add("{BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB}", @"Debug Unicode|x64");

            string xmlString = ResolveNonMSBuildProjectOutput_Tests.CreatePregeneratedPathDoc(projectConfigurations);

            MockEngine engine = new MockEngine(_output);
            AssignProjectConfiguration task = new AssignProjectConfiguration();
            task.BuildEngine = engine;
            task.SolutionConfigurationContents = xmlString;
            task.ProjectReferences = (ITaskItem[])projectRefs.ToArray(typeof(ITaskItem));
            task.ShouldUnsetParentConfigurationAndPlatform = false; // default for non-solution builds

            bool result = task.Execute();
            result.ShouldBeTrue();

            task.UnassignedProjects.Length.ShouldBe(1);
            ITaskItem unresolved = task.UnassignedProjects[0];
            string globalPropertiesToRemove = unresolved.GetMetadata("GlobalPropertiesToRemove");
            globalPropertiesToRemove.ShouldBeEmpty();
        }

        /// <summary>
        /// Verifies that resolved references still get correct SetConfiguration and SetPlatform
        /// metadata with configurations containing spaces.
        /// </summary>
        [Fact]
        public void ResolvedReferencesPreserveSpacesInConfigurationName()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            var projectRefs = new ArrayList();
            var resolvedRef = ResolveNonMSBuildProjectOutput_Tests.CreateReferenceItem(
                "Main.vcxproj",
                "{CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC}",
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}",
                "Main");
            projectRefs.Add(resolvedRef);

            // Solution config contains this project with a space in config name
            var projectConfigurations = new Hashtable();
            projectConfigurations.Add("{CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC}", @"Debug Unicode|x64");

            string xmlString = ResolveNonMSBuildProjectOutput_Tests.CreatePregeneratedPathDoc(projectConfigurations);

            MockEngine engine = new MockEngine(_output);
            AssignProjectConfiguration task = new AssignProjectConfiguration();
            task.BuildEngine = engine;
            task.SolutionConfigurationContents = xmlString;
            task.ProjectReferences = (ITaskItem[])projectRefs.ToArray(typeof(ITaskItem));
            task.ShouldUnsetParentConfigurationAndPlatform = true;

            bool result = task.Execute();
            result.ShouldBeTrue();

            task.AssignedProjects.Length.ShouldBe(1);
            task.UnassignedProjects.Length.ShouldBe(0);

            ITaskItem resolved = task.AssignedProjects[0];
            resolved.GetMetadata("SetConfiguration").ShouldBe("Configuration=Debug Unicode");
            resolved.GetMetadata("SetPlatform").ShouldBe("Platform=x64");
            resolved.GetMetadata("Configuration").ShouldBe("Debug Unicode");
            resolved.GetMetadata("Platform").ShouldBe("x64");
        }

        /// <summary>
        /// Verifies that existing GlobalPropertiesToRemove metadata on an unresolved reference
        /// is preserved (not appended to) under ChangeWave 18.6.
        /// </summary>
        [Fact]
        public void ExistingGlobalPropertiesToRemovePreservedForUnresolvedReferences()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            var projectRefs = new ArrayList();
            var unresolvedRef = ResolveNonMSBuildProjectOutput_Tests.CreateReferenceItem(
                "Utility.vcxproj",
                "{AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA}",
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}",
                "Utility");
            // Set some pre-existing GlobalPropertiesToRemove
            unresolvedRef.SetMetadata("GlobalPropertiesToRemove", "TargetFramework");
            projectRefs.Add(unresolvedRef);

            var projectConfigurations = new Hashtable();
            projectConfigurations.Add("{BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB}", @"Debug|x64");

            string xmlString = ResolveNonMSBuildProjectOutput_Tests.CreatePregeneratedPathDoc(projectConfigurations);

            MockEngine engine = new MockEngine(_output);
            AssignProjectConfiguration task = new AssignProjectConfiguration();
            task.BuildEngine = engine;
            task.SolutionConfigurationContents = xmlString;
            task.ProjectReferences = (ITaskItem[])projectRefs.ToArray(typeof(ITaskItem));
            task.ShouldUnsetParentConfigurationAndPlatform = true;

            bool result = task.Execute();
            result.ShouldBeTrue();

            task.UnassignedProjects.Length.ShouldBe(1);
            ITaskItem unresolved = task.UnassignedProjects[0];
            string globalPropertiesToRemove = unresolved.GetMetadata("GlobalPropertiesToRemove");

            // The pre-existing TargetFramework should still be there
            globalPropertiesToRemove.ShouldContain("TargetFramework");
            // But Configuration;Platform should NOT be appended
            globalPropertiesToRemove.ShouldNotContain("Configuration");
            globalPropertiesToRemove.ShouldNotContain("Platform");
        }

        /// <summary>
        /// End-to-end reproduction of issue #13453 (pre-18.6 behavior):
        ///
        /// In a solution build, Configuration flows as an inherited global property from the
        /// command line (/p:Configuration="Debug Unicode") through the solution metaproject to
        /// each project. For unresolved project references (not found in the .sln), the old
        /// AssignProjectConfiguration behavior adds "Configuration;Platform" to
        /// GlobalPropertiesToRemove. Microsoft.Common.CurrentVersion.targets then passes this
        /// as RemoveProperties="%(GlobalPropertiesToRemove)" to the MSBuild task. The MSBuild
        /// task strips Configuration from the child's inherited global properties, causing it
        /// to fall back to its default ("Debug") instead of the parent's "Debug Unicode".
        ///
        /// This test demonstrates the full causal chain:
        ///   1. AssignProjectConfiguration sets GlobalPropertiesToRemove=Configuration;Platform
        ///   2. A parent project built with /p:Configuration="Debug Unicode" calls MSBuild on
        ///      a child project with RemoveProperties=Configuration;Platform
        ///   3. The child project loses "Debug Unicode" and falls back to its default "Debug"
        /// </summary>
        [Fact]
        public void Issue13453_OldBehavior_UnresolvedReference_ChildLosesSpacedConfiguration()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            // Disable ChangeWave 18.6 to reproduce the old (buggy) behavior
            ChangeWaves.ResetStateForTests();
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_6.ToString());
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            // --- Step 1: Run AssignProjectConfiguration with an unresolved reference ---
            var projectRefs = new ArrayList();
            var unresolvedRef = ResolveNonMSBuildProjectOutput_Tests.CreateReferenceItem(
                "Utility.vcxproj",
                "{AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA}",
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}",
                "Utility");
            projectRefs.Add(unresolvedRef);

            // Solution config does NOT contain this project's GUID — it will be unresolved
            var projectConfigurations = new Hashtable();
            projectConfigurations.Add("{BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB}", @"Debug Unicode|x64");

            string xmlString = ResolveNonMSBuildProjectOutput_Tests.CreatePregeneratedPathDoc(projectConfigurations);

            MockEngine assignEngine = new MockEngine(_output);
            AssignProjectConfiguration assignTask = new AssignProjectConfiguration();
            assignTask.BuildEngine = assignEngine;
            assignTask.SolutionConfigurationContents = xmlString;
            assignTask.ProjectReferences = (ITaskItem[])projectRefs.ToArray(typeof(ITaskItem));
            assignTask.ShouldUnsetParentConfigurationAndPlatform = true;

            bool assignResult = assignTask.Execute();
            assignResult.ShouldBeTrue();
            assignTask.UnassignedProjects.Length.ShouldBe(1);

            // The old behavior: GlobalPropertiesToRemove includes Configuration;Platform
            ITaskItem unresolved = assignTask.UnassignedProjects[0];
            string globalPropertiesToRemove = unresolved.GetMetadata("GlobalPropertiesToRemove");
            globalPropertiesToRemove.ShouldContain("Configuration");
            globalPropertiesToRemove.ShouldContain("Platform");

            // --- Step 2: Demonstrate the consequence of that metadata.
            //     A parent project built with Configuration=Debug Unicode calls MSBuild on
            //     itself (child target) with RemoveProperties=Configuration;Platform.
            //     This mirrors the solution build pipeline in
            //     Microsoft.Common.CurrentVersion.targets:
            //       <MSBuild Projects="@(ProjectReference)"
            //                RemoveProperties="%(GlobalPropertiesToRemove)" />
            string projectFile = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <PropertyGroup>
                        <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    </PropertyGroup>
                    <Target Name=`ParentBuild`>
                        <MSBuild Projects=`$(MSBuildProjectFullPath)`
                                 RemoveProperties=`Configuration;Platform`
                                 Targets=`ReportChildConfig` />
                    </Target>
                    <Target Name=`ReportChildConfig`>
                        <Message Text=`ChildConfiguration=[$(Configuration)]` Importance=`High` />
                    </Target>
                </Project>
            ");

            try
            {
                // Build with Configuration=Debug Unicode as a global property,
                // simulating: msbuild /p:Configuration="Debug Unicode" /p:Platform=x64
                var globalProps = new Dictionary<string, string>
                {
                    { "Configuration", "Debug Unicode" },
                    { "Platform", "x64" }
                };

                MockLogger logger = new MockLogger(_output);
                bool buildResult = ObjectModelHelpers.BuildTempProjectFileWithTargets(
                    projectFile, new[] { "ParentBuild" }, globalProps, logger);
                buildResult.ShouldBeTrue();

                _output.WriteLine(logger.FullLog);

                // The child target received Configuration=Debug (the default) because
                // RemoveProperties=Configuration;Platform stripped the inherited
                // "Debug Unicode" global property. This is the bug: the user passed
                // Configuration="Debug Unicode" but the child project sees "Debug".
                logger.AssertLogContains("ChildConfiguration=[Debug]");
                logger.AssertLogDoesntContain("ChildConfiguration=[Debug Unicode]");
            }
            finally
            {
                File.Delete(projectFile);
            }
        }

        /// <summary>
        /// End-to-end verification that ChangeWave 18.6 fixes issue #13453:
        ///
        /// With the fix, AssignProjectConfiguration no longer adds "Configuration;Platform"
        /// to GlobalPropertiesToRemove for unresolved references. Without RemoveProperties,
        /// the MSBuild task preserves the parent's inherited Configuration="Debug Unicode"
        /// when building the child project.
        ///
        /// This test demonstrates the fixed causal chain:
        ///   1. AssignProjectConfiguration does NOT set GlobalPropertiesToRemove
        ///   2. A parent project built with /p:Configuration="Debug Unicode" calls MSBuild on
        ///      a child project with RemoveProperties="" (empty)
        ///   3. The child project correctly inherits "Debug Unicode"
        /// </summary>
        [Fact]
        public void Issue13453_NewBehavior_UnresolvedReference_ChildInheritsSpacedConfiguration()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);

            // ChangeWave 18.6 is enabled by default — this is the fixed behavior

            // --- Step 1: Run AssignProjectConfiguration with an unresolved reference ---
            var projectRefs = new ArrayList();
            var unresolvedRef = ResolveNonMSBuildProjectOutput_Tests.CreateReferenceItem(
                "Utility.vcxproj",
                "{AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA}",
                "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}",
                "Utility");
            projectRefs.Add(unresolvedRef);

            // Solution config does NOT contain this project's GUID — it will be unresolved
            var projectConfigurations = new Hashtable();
            projectConfigurations.Add("{BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB}", @"Debug Unicode|x64");

            string xmlString = ResolveNonMSBuildProjectOutput_Tests.CreatePregeneratedPathDoc(projectConfigurations);

            MockEngine assignEngine = new MockEngine(_output);
            AssignProjectConfiguration assignTask = new AssignProjectConfiguration();
            assignTask.BuildEngine = assignEngine;
            assignTask.SolutionConfigurationContents = xmlString;
            assignTask.ProjectReferences = (ITaskItem[])projectRefs.ToArray(typeof(ITaskItem));
            assignTask.ShouldUnsetParentConfigurationAndPlatform = true;

            bool assignResult = assignTask.Execute();
            assignResult.ShouldBeTrue();
            assignTask.UnassignedProjects.Length.ShouldBe(1);

            // The fix: GlobalPropertiesToRemove does NOT include Configuration;Platform
            ITaskItem unresolved = assignTask.UnassignedProjects[0];
            string globalPropertiesToRemove = unresolved.GetMetadata("GlobalPropertiesToRemove");
            globalPropertiesToRemove.ShouldNotContain("Configuration");
            globalPropertiesToRemove.ShouldNotContain("Platform");

            // --- Step 2: Demonstrate that without RemoveProperties, the child keeps
            //     "Debug Unicode". Same project as the old-behavior test, but WITHOUT
            //     RemoveProperties on the MSBuild task call. ---
            string projectFile = ObjectModelHelpers.CreateTempFileOnDisk(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                    <PropertyGroup>
                        <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
                    </PropertyGroup>
                    <Target Name=`ParentBuild`>
                        <MSBuild Projects=`$(MSBuildProjectFullPath)`
                                 Targets=`ReportChildConfig` />
                    </Target>
                    <Target Name=`ReportChildConfig`>
                        <Message Text=`ChildConfiguration=[$(Configuration)]` Importance=`High` />
                    </Target>
                </Project>
            ");

            try
            {
                // Build with Configuration=Debug Unicode as a global property
                var globalProps = new Dictionary<string, string>
                {
                    { "Configuration", "Debug Unicode" },
                    { "Platform", "x64" }
                };

                MockLogger logger = new MockLogger(_output);
                bool buildResult = ObjectModelHelpers.BuildTempProjectFileWithTargets(
                    projectFile, new[] { "ParentBuild" }, globalProps, logger);
                buildResult.ShouldBeTrue();

                _output.WriteLine(logger.FullLog);

                // The child correctly inherits "Debug Unicode" from the parent.
                // This is the fix for issue #13453.
                logger.AssertLogContains("ChildConfiguration=[Debug Unicode]");
            }
            finally
            {
                File.Delete(projectFile);
            }
        }
    }
}
