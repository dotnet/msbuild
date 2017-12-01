// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Profiler;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.Build.UnitTests.ObjectModelHelpers;

namespace Microsoft.Build.Engine.UnitTests
{
    /// <summary>
    /// Integration tests that run a build with the profiler turned on and validates the profiler report
    /// </summary>
    public class EvaluationProfiler_Tests : IDisposable
    {
        private readonly BuildManager _buildManager;
        private readonly TestEnvironment _env;

        /// <nodoc/>
        public EvaluationProfiler_Tests(ITestOutputHelper output)
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
            _buildManager.Dispose();
            _env.Dispose();
        }

        /// <summary>
        /// Verifies that a given element name shows up in a profiled MSBuild project
        /// </summary>
        [InlineData("Target", "<Target Name='test'/>")]
        [InlineData("Message", 
@"<Target Name='echo'>
    <Message text='echo!'/>
</Target>")]
        [InlineData("appname", 
@"<Target Name='test'/>
<PropertyGroup>
    <appname>Hello</appname>
</PropertyGroup>")]
        [InlineData("CSFile",
@"<Target Name='test'/>
<ItemGroup>
    <CSFile Include='file.cs'/>
</ItemGroup>")]
        [Theory]
        public void VerifySimpleProfiledData(string elementName, string body)
        {
            string contents = $@"
<Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
    {body}
</Project>
";
            var result = BuildAndGetProfilerResult(contents);
            var profiledElements = result.ProfiledLocations.Keys.ToList();

            Assert.True(profiledElements.Any(location => location.ElementName == elementName));
        }

        [Fact]
        public void VerifyProfiledData()
        {
            string contents = @"
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

            var result = BuildAndGetProfilerResult(contents);
            var profiledElements = result.ProfiledLocations.Keys.ToList();

            // Initial properties (pass 0)
            // There are no XML elements representing initial properties, so just checking the pass is triggered
            Assert.Equal(1, profiledElements.Count(location => location.EvaluationPass == EvaluationPass.InitialProperties));

            // Properties (pass 1)
            Assert.Equal(1, profiledElements.Count(location => location.ElementName == "PropertyGroup"));
            Assert.Equal(1, profiledElements.Count(location => location.ElementName == "appname"));

            // Item definition group (pass 2)
            Assert.Equal(1, profiledElements.Count(location => location.ElementName == "ItemDefinitionGroup"));
            Assert.Equal(1, profiledElements.Count(location => location.ElementName == "CSFile" & location.EvaluationPass == EvaluationPass.ItemDefintionGroups));

            // Item groups (pass 3 and 3.1)
            Assert.Equal(1, profiledElements.Count(location => location.ElementName == "ItemGroup"));
            Assert.Equal(2, profiledElements.Count(location => location.ElementName == "CSFile" & location.EvaluationPass == EvaluationPass.Items));
            Assert.Equal(1, profiledElements.Count(location => location.ElementName == "Condition" & location.EvaluationPass == EvaluationPass.Items));
            Assert.Equal(2, profiledElements.Count(location => location.ElementName == "CSFile" & location.EvaluationPass == EvaluationPass.LazyItems));

            // Using tasks (pass 4)
            // The using element itself is evaluated as part of pass 0, so just checking the overall pass is triggered by the corresponding element
            Assert.Equal(1, profiledElements.Count(location => location.EvaluationPass == EvaluationPass.UsingTasks));

            // Targets (pass 5)
            Assert.Equal(2, profiledElements.Count(location => location.ElementName == "Message"));
            Assert.Equal(1, profiledElements.Count(location => location.ElementName == "Target"));
        }

        /// <summary>
        /// Runs a build for a given project content with the profiler option on and returns the result of profiling it
        /// </summary>
        private ProfilerResult BuildAndGetProfilerResult(string projectContent)
        {
            var content = CleanupFileContents(projectContent);

            var profilerLogger = new ProfilerLogger("dummyFile");
            var parameters = new BuildParameters
            {
                ShutdownInProcNodeOnBuildFinish = true,
                Loggers = new ILogger[] { profilerLogger },
                DisableInProcNode = true, // This is actually important since we also want to test the serialization of the events
                ProjectLoadSettings = ProjectLoadSettings.ProfileEvaluation
            };

            using (var projectCollection = new ProjectCollection())
            {
                var project = CreateProject(content, MSBuildDefaultToolsVersion, projectCollection);
                var projectInstance = _buildManager.GetProjectInstanceForBuild(project);

                var buildRequestData = new BuildRequestData(
                    projectInstance,
                    new string[]{},
                    projectCollection.HostServices);

                var result = _buildManager.Build(parameters, buildRequestData);

                File.Delete(project.FullPath);

                Assert.Equal(BuildResultCode.Success, result.OverallResult);
            }

            return profilerLogger.GetAggregatedResult();
        }

        /// <summary>
        /// Retrieves a Project with the specified contents using the specified projectCollection
        /// </summary>
        private Project CreateProject(string contents, string toolsVersion, ProjectCollection projectCollection)
        {
            Project project = new Project(XmlReader.Create(new StringReader(contents)), null, toolsVersion, projectCollection)
            {
                FullPath = _env.CreateFile().Path
            };

            project.Save();

            return project;
        }
    }
}
