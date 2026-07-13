// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Profiler;
using Microsoft.Build.Logging;
using Microsoft.Build.UnitTests;
using Shouldly;
using static Microsoft.Build.UnitTests.ObjectModelHelpers;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests
{
    /// <summary>
    /// Integration tests that run a build with the profiler turned on and validates the profiler report
    /// </summary>
    [TestClass]
    public class EvaluationProfiler_Tests : IDisposable
    {
        private readonly BuildManager _buildManager;
        private readonly TestEnvironment _env;

        private const string SpecData = @"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
    <PropertyGroup>
        <appname>HelloWorldCS</appname>
    </PropertyGroup>

    <ItemDefinitionGroup>
        <CSFile>
            <encoding>utf8</encoding>
        </CSFile>
    </ItemDefinitionGroup>

    <ItemGroup>
        <CSFile Include = 'consolehwcs1.cs'/>
        <CSFile Include = 'consolehwcs2.cs' Condition='true'/>
    </ItemGroup>

    <UsingTask TaskName='DummyTask' AssemblyName='Microsoft.Build.Engine.UnitTests' TaskFactory='DummyTask'/>

    <Target Name = 'FakeCompile'>
        <Message Text = 'The output assembly is $(appname).exe'/>
        <Message Text = 'The sources are @(CSFile)'/>
    </Target>
</Project>";
        /// <nodoc/>
        public EvaluationProfiler_Tests(TestContext output)
        {
            // Ensure that any previous tests which may have been using the default BuildManager do not conflict with us.
            BuildManager.DefaultBuildManager.Dispose();

            _buildManager = new BuildManager();

            _env = TestEnvironment.Create(output);
            _env.SetEnvironmentVariable("MSBUILDINPROCENVCHECK", "1");
        }

        /// <nodoc/>
        public void Dispose()
        {
            try
            {
                _buildManager.Dispose();
            }
            finally
            {
                _env.Dispose();
            }
        }

        /// <summary>
        /// Verifies that a given element name shows up in a profiled MSBuild project
        /// </summary>
        [DataRow("Target", "<Target Name='test'/>")]
        [DataRow("Message",
@"<Target Name='echo'>
    <Message text='echo!'/>
</Target>")]
        [DataRow("appname",
@"<Target Name='test'/>
<PropertyGroup>
    <appname>Hello</appname>
</PropertyGroup>")]
        [DataRow("CSFile",
@"<Target Name='test'/>
<ItemGroup>
    <CSFile Include='file.cs'/>
</ItemGroup>")]
        [MSBuildTestMethod]
        public void VerifySimpleProfiledData(string elementName, string body)
        {
            string contents = $@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
    {body}
</Project>
";
            var result = BuildAndGetProfilerResult(contents);
            var profiledElements = result.ProfiledLocations.Keys.ToList();

            profiledElements.ShouldContain(location => location.ElementName == elementName);
        }

        /// <summary>
        /// Verifies that a given element name shows up in a profiled MSBuild project
        /// </summary>
        [DataRow("Target", "<Target Name='test'/>")]
        [DataRow("Message",
            @"<Target Name='echo'>
    <Message text='echo!'/>
</Target>")]
        [DataRow("appname",
            @"<Target Name='test'/>
<PropertyGroup>
    <appname>Hello</appname>
</PropertyGroup>")]
        [DataRow("CSFile",
            @"<Target Name='test'/>
<ItemGroup>
    <CSFile Include='file.cs'/>
</ItemGroup>")]
        [MSBuildTestMethod]
        public void VerifySimpleProfiledDataWithoutProjectLoadSetting(string elementName, string body)
        {
            string contents = $@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
    {body}
</Project>
";
            var result = BuildAndGetProfilerResult(contents, false);
            var profiledElements = result.ProfiledLocations.Keys.ToList();

            profiledElements.ShouldContain(location => location.ElementName == elementName);
        }

        [MSBuildTestMethod]
        public void VerifyProfiledData()
        {
            var result = BuildAndGetProfilerResult(SpecData);
            var profiledElements = result.ProfiledLocations.Keys.ToList();

            // Initial properties (pass 0)
            // There are no XML elements representing initial properties, so just checking the pass is triggered
            Assert.ContainsSingle(profiledElements.Where(location => location.EvaluationPass == EvaluationPass.InitialProperties));

            // Properties (pass 1)
            Assert.ContainsSingle(profiledElements.Where(location => location.ElementName == "PropertyGroup"));
            Assert.ContainsSingle(profiledElements.Where(location => location.ElementName == "appname"));

            // Item definition group (pass 2)
            Assert.ContainsSingle(profiledElements.Where(location => location.ElementName == "ItemDefinitionGroup"));
            Assert.ContainsSingle(profiledElements.Where(location => location.ElementName == "CSFile" & location.EvaluationPass == EvaluationPass.ItemDefinitionGroups));

            // Item groups (pass 3 and 3.1)
            Assert.ContainsSingle(profiledElements.Where(location => location.ElementName == "ItemGroup"));
            Assert.AreEqual(2, profiledElements.Count(location => location.ElementName == "CSFile" & location.EvaluationPass == EvaluationPass.Items));
            Assert.ContainsSingle(profiledElements.Where(location => location.ElementName == "Condition" & location.EvaluationPass == EvaluationPass.Items));
            Assert.AreEqual(2, profiledElements.Count(location => location.ElementName == "CSFile" & location.EvaluationPass == EvaluationPass.LazyItems));

            // Using tasks (pass 4)
            // The using element itself is evaluated as part of pass 0, so just checking the overall pass is triggered by the corresponding element
            Assert.ContainsSingle(profiledElements.Where(location => location.EvaluationPass == EvaluationPass.UsingTasks));

            // Targets (pass 5)
            Assert.AreEqual(2, profiledElements.Count(location => location.ElementName == "Message"));
            Assert.ContainsSingle(profiledElements.Where(location => location.ElementName == "Target"));
        }

        [MSBuildTestMethod]
        public void VerifyProfiledGlobData()
        {
            string contents = @"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
    <ItemGroup>
        <TestGlob Include='wwwroot\dist\**' />
        <TestGlob Include='ClientApp\dist\**' />
    </ItemGroup>
    <Target Name='echo'>
        <Message text='echo!'/>
    </Target>
</Project>";

            var result = BuildAndGetProfilerResult(contents);
            var profiledElements = result.ProfiledLocations.Keys.ToList();

            // Item groups (pass 3 and 3.1)
            Assert.AreEqual(2, profiledElements.Count(location => location.ElementName == "TestGlob" & location.EvaluationPass == EvaluationPass.Items));
            Assert.AreEqual(2, profiledElements.Count(location => location.ElementName == "TestGlob" & location.EvaluationPass == EvaluationPass.LazyItems));

            // There should be one aggregated entry representing the total glob time
            Assert.ContainsSingle(profiledElements.Where(location => location.EvaluationPass == EvaluationPass.TotalGlobbing));
            var totalGlob = profiledElements.Find(evaluationLocation =>
                evaluationLocation.EvaluationPass == EvaluationPass.TotalGlobbing);
            // And it should aggregate the result of the 2 glob locations
            var totalGlobLocation = result.ProfiledLocations[totalGlob];
            Assert.AreEqual(2, totalGlobLocation.NumberOfHits);
        }

        [MSBuildTestMethod]
        public void VerifyParentIdData()
        {
            string contents = @"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
    <ItemGroup>
        <Test Include='ClientApp\dist\**' />
    </ItemGroup>
    <Target Name='echo'>
        <Message text='echo!'/>
    </Target>
</Project>";
            var result = BuildAndGetProfilerResult(contents);
            var profiledElements = result.ProfiledLocations.Keys.ToList();

            // The total evaluation should be the parent of all other passes (but total globbing, which is an aggregate item)
            var totalEvaluation = profiledElements.Find(e => e.IsEvaluationPass && e.EvaluationPass == EvaluationPass.TotalEvaluation);
            Assert.IsTrue(profiledElements.Where(e => e.IsEvaluationPass && e.EvaluationPass != EvaluationPass.TotalGlobbing && !e.Equals(totalEvaluation))
                .All(e => e.ParentId == totalEvaluation.Id));

            // Check the test item has the right parent
            var itemPass = profiledElements.Find(e => e.IsEvaluationPass && e.EvaluationPass == EvaluationPass.Items);
            var itemGroup = profiledElements.Find(e => e.ElementName == "ItemGroup");
            var testItem = profiledElements.Find(e => e.ElementName == "Test" && e.EvaluationPass == EvaluationPass.Items);
            Assert.AreEqual(itemPass.Id, itemGroup.ParentId);
            Assert.AreEqual(itemGroup.Id, testItem.ParentId);

            // Check the lazy test item has the right parent
            var lazyItemPass = profiledElements.Find(e => e.IsEvaluationPass && e.EvaluationPass == EvaluationPass.LazyItems);
            var lazyTestItem = profiledElements.Find(e => e.ElementName == "Test" && e.EvaluationPass == EvaluationPass.LazyItems);
            Assert.AreEqual(lazyItemPass.Id, lazyTestItem.ParentId);

            // Check the target item has the right parent
            var targetPass = profiledElements.Find(e => e.IsEvaluationPass && e.EvaluationPass == EvaluationPass.Targets);
            var target = profiledElements.Find(e => e.ElementName == "Target");
            var messageTarget = profiledElements.Find(e => e.ElementName == "Message");
            Assert.AreEqual(targetPass.Id, target.ParentId);
            Assert.AreEqual(target.Id, messageTarget.ParentId);
        }

        [MSBuildTestMethod]
        public void VerifyIdsSanity()
        {
            var result = BuildAndGetProfilerResult(SpecData);
            var profiledElements = result.ProfiledLocations.Keys.ToList();

            // All ids must be unique
            var allIds = profiledElements.Select(e => e.Id).ToList();
            var allUniqueIds = allIds.ToImmutableHashSet();
            Assert.AreEqual(allIds.Count, allUniqueIds.Count);

            // Every element with a parent id must point to a valid item
            Assert.IsTrue(profiledElements.All(e => e.ParentId == null || allUniqueIds.Contains(e.ParentId.Value)));
        }

        /// <summary>
        /// Runs a build for a given project content with the profiler option on and returns the result of profiling it
        /// </summary>
        private ProfilerResult BuildAndGetProfilerResult(string projectContent, bool setProjectLoadSetting = true)
        {
            var content = CleanupFileContents(projectContent);

            var profilerLogger = ProfilerLogger.CreateForTesting();
            var parameters = new BuildParameters
            {
                ShutdownInProcNodeOnBuildFinish = true,
                Loggers = new ILogger[] { profilerLogger },
                DisableInProcNode = true, // This is actually important since we also want to test the serialization of the events
                EnableNodeReuse = false,
                ProjectLoadSettings = setProjectLoadSetting ? ProjectLoadSettings.ProfileEvaluation : 0
            };

            using (var projectCollection = new ProjectCollection())
            {
                var project = CreateProject(content, MSBuildDefaultToolsVersion, projectCollection);
                var projectInstance = _buildManager.GetProjectInstanceForBuild(project);

                var buildRequestData = new BuildRequestData(
                    projectInstance,
                    Array.Empty<string>(),
                    projectCollection.HostServices);

                var result = _buildManager.Build(parameters, buildRequestData);

                File.Delete(project.FullPath);

                Assert.AreEqual(BuildResultCode.Success, result.OverallResult);
            }

            return profilerLogger.GetAggregatedResult(pruneSmallItems: false);
        }

        /// <summary>
        /// Retrieves a Project with the specified contents using the specified projectCollection
        /// </summary>
        private Project CreateProject(string contents, string toolsVersion, ProjectCollection projectCollection)
        {
            using ProjectFromString projectFromString = new(contents, null, toolsVersion, projectCollection);
            Project project = projectFromString.Project;
            project.FullPath = _env.CreateFile().Path;

            project.Save();

            return project;
        }
    }
}
