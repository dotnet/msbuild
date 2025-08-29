﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using ExpectedNodeBuildOutput = System.Collections.Generic.Dictionary<Microsoft.Build.Graph.ProjectGraphNode, string[]>;
using OutputCacheDictionary = System.Collections.Generic.Dictionary<Microsoft.Build.Graph.ProjectGraphNode, string>;

#nullable disable

namespace Microsoft.Build.Graph.UnitTests
{
    public class IsolateProjectsTests : IDisposable
    {
        private readonly string _project = @"
                <Project DefaultTargets='BuildSelf'>

                    <ItemGroup>
                        <GraphIsolationExemptReference Condition=`'{4}'!=''` Include=`$([MSBuild]::Escape('{4}'))`/>
                    </ItemGroup>

                    <ItemGroup>
                        <ProjectReference Include='{0}'/>
                    </ItemGroup>

                    <Target Name='BuildDeclaredReference'>
                        <MSBuild
                            Projects='{1}'
                            Targets='DeclaredReferenceTarget'
                            {3}
                        />
                    </Target>

                    <Target Name='BuildUndeclaredReference'>
                        <MSBuild
                            Projects='{2}'
                            Targets='UndeclaredReferenceTarget'
                            {3}
                        />
                    </Target>

                    <Target Name='BuildSelf'>
                        <MSBuild
                            Projects='$(MSBuildThisFile)'
                            Targets='SelfTarget'
                            {3}
                        />
                    </Target>

                    <Target Name='CallTarget'>
                        <CallTarget Targets='SelfTarget'/>  
                    </Target>

                    <Target Name='SelfTarget'>
                    </Target>

                    <UsingTask TaskName='CustomMSBuild' TaskFactory='RoslynCodeTaskFactory' AssemblyFile='$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll'>
                        <ParameterGroup>
                          <Projects ParameterType='Microsoft.Build.Framework.ITaskItem[]' Required='true' />
                          <Targets ParameterType='Microsoft.Build.Framework.ITaskItem[]' Required='true' />
                        </ParameterGroup>
                        <Task>
                          <Code Type='Fragment' Language='cs'>
                    <![CDATA[

var projects = new string[Projects.Length];
var globalProperties = new IDictionary[Projects.Length];
var toolsVersions = new string[Projects.Length];

for (var i = 0; i < Projects.Length; i++)
{{
  projects[i] = Projects[i].ItemSpec;
  globalProperties[i] = new Dictionary<string, string>();
  toolsVersions[i] = ""Current"";
}}

var targets = new string[Targets.Length];
for (var i = 0; i < Targets.Length; i++)
{{
  targets[i] = Targets[i].ItemSpec;
}}

BuildEngine5.BuildProjectFilesInParallel(
  projects,
  targets,
  globalProperties,
  null,
  toolsVersions,
  false,
  false
  );
]]>
                          </Code>
                        </Task>
                    </UsingTask>

                    <Target Name='BuildDeclaredReferenceViaTask'>
                        <CustomMSBuild Projects='{1}' Targets='DeclaredReferenceTarget'/>
                    </Target>

                    <Target Name='BuildUndeclaredReferenceViaTask'>
                        <CustomMSBuild Projects='{2}' Targets='UndeclaredReferenceTarget'/>
                    </Target>
                </Project>";

        private readonly string _declaredReference = @"
                <Project>
                    <Target Name='DeclaredReferenceTarget'>
                        <Message Text='Message from reference' Importance='High' />
                    </Target>
                </Project>";

        private readonly string _undeclaredReference = @"
                <Project>
                    <Target Name='UndeclaredReferenceTarget'>
                        <Message Text='Message from reference' Importance='High' />
                    </Target>
                </Project>";

        private readonly ITestOutputHelper _testOutput;
        private TestEnvironment _env;
        private BuildParameters _buildParametersPrototype;

        public IsolateProjectsTests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
            _env = TestEnvironment.Create(_testOutput, ignoreBuildErrorFiles: true);

            if (NativeMethodsShared.IsOSX)
            {
                // OSX links /var into /private, which makes Path.GetTempPath() to return "/var..." but Directory.GetCurrentDirectory to return "/private/var..."
                // this discrepancy fails the msbuild undeclared reference enforcements due to failed path equality checks
                _env.SetTempPath(Path.Combine(Directory.GetCurrentDirectory(), Guid.NewGuid().ToString("N")), deleteTempDirectory: true);
            }

            // todo investigate why out of proc builds fail on macos https://github.com/dotnet/msbuild/issues/3915
            var disableInProcNode = !NativeMethodsShared.IsOSX;

            _buildParametersPrototype = new BuildParameters
            {
                EnableNodeReuse = false,
                ProjectIsolationMode = ProjectIsolationMode.True,
                DisableInProcNode = disableInProcNode
            };
        }

        public void Dispose()
        {
            _env.Dispose();
        }

        [Theory]
        [InlineData(BuildResultCode.Success, new string[] { })]
        [InlineData(BuildResultCode.Success, new[] { "BuildSelf" })]
        public void CacheAndUndeclaredReferenceEnforcementShouldAcceptSelfReferences(BuildResultCode expectedBuildResult, string[] targets)
        {
            AssertBuild(targets,
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(expectedBuildResult);

                    logger.Errors.ShouldBeEmpty();
                });
        }

        [Fact]
        public void CacheAndUndeclaredReferenceEnforcementShouldAcceptCallTarget()
        {
            AssertBuild(new[] { "CallTarget" },
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Success);

                    logger.Errors.ShouldBeEmpty();
                });
        }

        [Fact(Skip = "https://github.com/dotnet/msbuild/issues/3876")]
        public void CacheEnforcementShouldFailWhenReferenceWasNotPreviouslyBuiltAndOnContinueOnError()
        {
            CacheEnforcementImpl(addContinueOnError: true);
        }

        [Fact]
        public void CacheEnforcementShouldFailWhenReferenceWasNotPreviouslyBuiltWithoutContinueOnError()
        {
            CacheEnforcementImpl(addContinueOnError: false);
        }

        private void CacheEnforcementImpl(bool addContinueOnError)
        {
            AssertBuild(
                new[] { "BuildDeclaredReference" },
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Failure);

                    logger.ErrorCount.ShouldBe(1);

                    logger.Errors.First()
                        .Message.ShouldStartWith("MSB4252:");

                    logger.Errors.First().BuildEventContext.ShouldNotBe(BuildEventContext.Invalid);

                    logger.Errors.First().BuildEventContext.NodeId.ShouldNotBe(BuildEventContext.InvalidNodeId);
                    logger.Errors.First().BuildEventContext.ProjectInstanceId.ShouldNotBe(BuildEventContext.InvalidProjectInstanceId);
                    logger.Errors.First().BuildEventContext.ProjectContextId.ShouldNotBe(BuildEventContext.InvalidProjectContextId);
                    logger.Errors.First().BuildEventContext.TargetId.ShouldNotBe(BuildEventContext.InvalidTargetId);
                    logger.Errors.First().BuildEventContext.TaskId.ShouldNotBe(BuildEventContext.InvalidTaskId);
                },
                addContinueOnError: addContinueOnError);
        }

        [Fact]
        public void IsolationRelatedMessagesShouldNotBePresentInNonIsolatedBuilds()
        {
            AssertBuild(
                new[] { "BuildDeclaredReference", "BuildUndeclaredReference" },
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Success);

                    logger.ErrorCount.ShouldBe(0);
                    logger.Errors.ShouldBeEmpty();

                    // the references got built because isolation is turned off
                    logger.AssertMessageCount("Message from reference", 2);
                    logger.AllBuildEvents.OfType<ProjectStartedEventArgs>().Count().ShouldBe(3);

                    logger.AssertLogDoesntContain("MSB4260");
                },
                excludeReferencesFromConstraints: true,
                isolateProjects: ProjectIsolationMode.False);
        }

        [Fact]
        public void IsolationRelatedMessageShouldBePresentInIsolatedBuildsWithMessaging()
        {
            AssertBuild(
                new[] { "BuildDeclaredReference", "BuildUndeclaredReference" },
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Success);

                    logger.ErrorCount.ShouldBe(0);
                    logger.Errors.ShouldBeEmpty();

                    // The references got built because the isolation mode is set to ProjectIsolationMode.MessageUponIsolationViolation.
                    logger.AssertMessageCount("Message from reference", 2);
                    logger.AllBuildEvents.OfType<ProjectStartedEventArgs>().Count().ShouldBe(3);

                    logger.AssertMessageCount("MSB4260", 2);
                },
                isolateProjects: ProjectIsolationMode.MessageUponIsolationViolation);
        }

        [Fact]
        public void UndeclaredReferenceBuildResultNotPresentInOutputCache()
        {
            // Create the graph 1 -> 2 -> 3, where 2 is a declared project reference
            // and 3 is an undeclared project reference.
            // 3 outputs an item UndeclaredReferenceTargetItem that 2 outputs.
            // Run under ProjectIsolationMode.MessageUponIsolationViolation mode
            // and verify that 3's build result is not present in 2's output results
            // cache since, under this mode, only the results of the project
            // to build under isolation (2) should be serialized.
            // See CacheSerialization.SerializeCaches for more info.
            string undeclaredReferenceFile = GraphTestingUtilities.CreateProjectFile(
                _env,
                3,
                extraContent: @"
                    <Target Name='UndeclaredReferenceTarget' Outputs='@(UndeclaredReferenceTargetItem)'>
                        <ItemGroup>
                            <UndeclaredReferenceTargetItem Include='Foo.cs' />
                        </ItemGroup>
                        <Message Text='Message from undeclared reference' Importance='High' />
                    </Target>",
                defaultTargets: "UndeclaredReferenceTarget").Path;
            string declaredReferenceContents = string.Format(
                @"
                <Target Name='DeclaredReferenceTarget' Outputs='@(UndeclaredReferenceTargetItem)'>
                    <MSBuild
                        Projects='{0}'
                        Targets='UndeclaredReferenceTarget'>
                        <Output TaskParameter='TargetOutputs' ItemName='UndeclaredReferenceTargetItem' />
                    </MSBuild>
                </Target>".Cleanup(),
                undeclaredReferenceFile).Cleanup();
            string declaredReferenceFile = GraphTestingUtilities.CreateProjectFile(
                _env,
                2,
                extraContent: declaredReferenceContents,
                defaultTargets: "DeclaredReferenceTarget").Path;
            string rootProjectContents = string.Format(
                @"
                <ItemGroup>
                    <ProjectReference Include='{0}' />
                </ItemGroup>
                <Target Name='BuildDeclaredReference'>
                    <MSBuild
                        Projects='{1}'
                        Targets='DeclaredReferenceTarget'
                    />
                </Target>".Cleanup(),
                declaredReferenceFile,
                declaredReferenceFile).Cleanup();
            string rootFile = GraphTestingUtilities.CreateProjectFile(
                _env,
                1,
                extraContent: rootProjectContents,
                defaultTargets: "BuildDeclaredReference").Path;
            var projectGraph = new ProjectGraph(
                rootFile,
                new Dictionary<string, string>(),
                _env.CreateProjectCollection().Collection);
            var expectedOutput = new ExpectedNodeBuildOutput();
            var outputCaches = new OutputCacheDictionary();
            ProjectGraphNode[] topoSortedProjectGraphNodes = projectGraph.ProjectNodesTopologicallySorted.ToArray();
            Dictionary<string, (BuildResult Result, MockLogger Logger)> results = ResultCacheBasedBuilds_Tests.BuildUsingCaches(
                _env,
                topoSortedProjectGraphNodes,
                expectedOutput,
                outputCaches,
                generateCacheFiles: true,
                assertBuildResults: false,
                projectIsolationMode: ProjectIsolationMode.MessageUponIsolationViolation);
            var deserializedOutputCacheDeclaredReference = CacheSerialization.DeserializeCaches(outputCaches[topoSortedProjectGraphNodes[0]]);
            var deserializedOutputCacheRoot = CacheSerialization.DeserializeCaches(outputCaches[topoSortedProjectGraphNodes[1]]);
            deserializedOutputCacheDeclaredReference.exception.ShouldBeNull();
            deserializedOutputCacheRoot.exception.ShouldBeNull();
            BuildResult[] declaredReferenceBuildResults = deserializedOutputCacheDeclaredReference.ResultsCache.GetEnumerator().ToArray();
            BuildResult[] rootBuildResults = deserializedOutputCacheRoot.ResultsCache.GetEnumerator().ToArray();

            // Both the root and declared reference projects should only have one build result.
            declaredReferenceBuildResults.Length.ShouldBe(1);
            rootBuildResults.Length.ShouldBe(1);
            declaredReferenceBuildResults[0].OverallResult.ShouldBe(BuildResultCode.Success);
            rootBuildResults[0].OverallResult.ShouldBe(BuildResultCode.Success);
            MockLogger rootLogger = results["1"].Logger;
            MockLogger declaredReferenceLogger = results["2"].Logger;
            rootLogger.ErrorCount.ShouldBe(0);
            declaredReferenceLogger.ErrorCount.ShouldBe(0);
            rootLogger.Errors.ShouldBeEmpty();
            declaredReferenceLogger.Errors.ShouldBeEmpty();
            rootLogger.AllBuildEvents.OfType<ProjectStartedEventArgs>().Count().ShouldBe(2);
            declaredReferenceLogger.AllBuildEvents.OfType<ProjectStartedEventArgs>().Count().ShouldBe(2);

            // One undeclared reference was built in isolation violation.
            declaredReferenceLogger.AssertMessageCount("Message from undeclared reference", 1);
            declaredReferenceLogger.AssertMessageCount("MSB4260", 1);

            // The declared reference project's output item is that of the undeclared reference
            // project.
            declaredReferenceBuildResults[0]["DeclaredReferenceTarget"].Items.Length.ShouldBe(1);
            declaredReferenceBuildResults[0]["DeclaredReferenceTarget"].Items[0].ItemSpec.ShouldBe("Foo.cs");
            rootBuildResults[0]["BuildDeclaredReference"].Items.Length.ShouldBe(0);
        }

        [Theory]
        [InlineData("BuildDeclaredReference")]
        [InlineData("BuildDeclaredReferenceViaTask")]
        [InlineData("BuildUndeclaredReference")]
        [InlineData("BuildUndeclaredReferenceViaTask")]
        public void EnforcementsCanBeSkipped(string targetName)
        {
            AssertBuild(
                new[] { targetName },
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Success);

                    logger.ErrorCount.ShouldBe(0);
                    logger.Errors.ShouldBeEmpty();

                    // the reference got built because the constraints were skipped
                    logger.AssertMessageCount("Message from reference", 1);
                    logger.AllBuildEvents.OfType<ProjectStartedEventArgs>().Count().ShouldBe(2);

                    logger.AssertMessageCount("MSB4260", 1);
                },
                excludeReferencesFromConstraints: true);
        }

        [Theory]
        [InlineData("BuildDeclaredReference")]
        [InlineData("BuildDeclaredReferenceViaTask")]
        public void CacheEnforcementShouldAcceptPreviouslyBuiltReferences(string targetName)
        {
            AssertBuild(new[] { targetName },
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Success);

                    logger.Errors.ShouldBeEmpty();
                },
                buildDeclaredReference: true);
        }

        [Theory]
        [InlineData(false, "BuildUndeclaredReference")]
        // [InlineData(false, "BuildUndeclaredReferenceViaTask")] https://github.com/dotnet/msbuild/issues/4385
        [InlineData(true, "BuildUndeclaredReference")]
        // [InlineData(true, "BuildUndeclaredReferenceViaTask")] https://github.com/dotnet/msbuild/issues/4385
        public void UndeclaredReferenceEnforcementShouldFailOnUndeclaredReference(bool addContinueOnError, string targetName)
        {
            AssertBuild(new[] { targetName },
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Failure);

                    logger.ErrorCount.ShouldBe(1);

                    logger.Errors.First().Message.ShouldStartWith("MSB4254:");
                },
                addContinueOnError: addContinueOnError);
        }

        [Theory]
        [InlineData("BuildUndeclaredReference")]
        // [InlineData("BuildUndeclaredReferenceViaTask")] https://github.com/dotnet/msbuild/issues/4385
        public void UndeclaredReferenceEnforcementShouldFailOnPreviouslyBuiltButUndeclaredReferences(string targetName)
        {
            AssertBuild(new[] { targetName },
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Failure);

                    logger.ErrorCount.ShouldBe(1);

                    logger.Errors.First().Message.ShouldStartWith("MSB4254:");
                },
                buildUndeclaredReference: true);
        }

        public static IEnumerable<object[]> UndeclaredReferenceEnforcementShouldNormalizeFilePathsTestData
        {
            get
            {
                Func<string, string> Preserve = path => path;

                Func<string, string> FullToRelative = path =>
                {
                    var directory = Path.GetDirectoryName(path);
                    var file = Path.GetFileName(path);

                    return Path.Combine("..", directory, file);
                };

                Func<string, string> ToForwardSlash = path => path.ToSlash();

                Func<string, string> ToBackSlash = path => path.ToBackslash();

                Func<string, string> ToDuplicateSlashes = path => path.Replace("/", "//").Replace(@"\", @"\\");

                var targetNames = new[] { "BuildDeclaredReference", /*"BuildDeclaredReferenceViaTask"*/};

                var functions = new[] { Preserve, FullToRelative, ToForwardSlash, ToBackSlash, ToDuplicateSlashes };

                foreach (var projectReferenceModifier in functions)
                {
                    foreach (var msbuildProjectModifier in functions)
                    {
                        foreach (var targetName in targetNames)
                        {
                            yield return new object[]
                            {
                                projectReferenceModifier,
                                msbuildProjectModifier,
                                targetName
                            };
                        }
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(UndeclaredReferenceEnforcementShouldNormalizeFilePathsTestData))]
        public void UndeclaredReferenceEnforcementShouldNormalizeFilePaths(Func<string, string> projectReferenceModifier, Func<string, string> msbuildProjectModifier, string targetName)
        {
            AssertBuild(new[] { targetName },
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Success);

                    logger.Errors.ShouldBeEmpty();
                },
                buildDeclaredReference: true,
                buildUndeclaredReference: false,
                addContinueOnError: false,
                projectReferenceModifier: projectReferenceModifier,
                msbuildOnDeclaredReferenceModifier: msbuildProjectModifier);
        }

        private void AssertBuild(
            string[] targets,
            Action<BuildResult, MockLogger> assert,
            bool buildDeclaredReference = false,
            bool buildUndeclaredReference = false,
            bool addContinueOnError = false,
            bool excludeReferencesFromConstraints = false,
            ProjectIsolationMode isolateProjects = ProjectIsolationMode.True,
            Func<string, string> projectReferenceModifier = null,
            Func<string, string> msbuildOnDeclaredReferenceModifier = null)
        {
            var rootProjectFile = CreateTmpFile(_env).Path;
            var declaredReferenceFile = CreateTmpFile(_env).Path;
            var undeclaredReferenceFile = CreateTmpFile(_env).Path;

            var projectContents = string.Format(
                _project.Cleanup(),
                projectReferenceModifier?.Invoke(declaredReferenceFile) ?? declaredReferenceFile,
                msbuildOnDeclaredReferenceModifier?.Invoke(declaredReferenceFile) ?? declaredReferenceFile,
                undeclaredReferenceFile,
                addContinueOnError
                    ? "ContinueOnError='WarnAndContinue'"
                    : string.Empty,
                excludeReferencesFromConstraints
                    ? $"{declaredReferenceFile};{undeclaredReferenceFile}"
                    : string.Empty)
                .Cleanup();

            File.WriteAllText(rootProjectFile, projectContents);
            File.WriteAllText(declaredReferenceFile, _declaredReference);
            File.WriteAllText(undeclaredReferenceFile, _undeclaredReference);

            var buildParameters = _buildParametersPrototype.Clone();
            buildParameters.ProjectIsolationMode = isolateProjects;

            using (var buildManagerSession = new Helpers.BuildManagerSession(_env, buildParameters))
            {
                if (buildDeclaredReference)
                {
                    buildManagerSession.BuildProjectFile(declaredReferenceFile, new[] { "DeclaredReferenceTarget" })
                        .OverallResult.ShouldBe(BuildResultCode.Success);
                }

                if (buildUndeclaredReference)
                {
                    buildManagerSession.BuildProjectFile(undeclaredReferenceFile, new[] { "UndeclaredReferenceTarget" })
                        .OverallResult.ShouldBe(BuildResultCode.Success);
                }

                var result = buildManagerSession.BuildProjectFile(rootProjectFile, targets);

                assert(result, buildManagerSession.Logger);
            }

            TransientTestFile CreateTmpFile(TestEnvironment env)
            {
                return NativeMethodsShared.IsMono && NativeMethodsShared.IsOSX
                                                ? env.CreateFile(new TransientTestFolder(Path.Combine(Directory.GetCurrentDirectory(), Guid.NewGuid().ToString("N"))))
                                                : env.CreateFile();
            }
        }

        [Fact]
        public void SkippedTargetsShouldNotTriggerCacheMissEnforcement()
        {
            var referenceFile = _env.CreateFile(
                "reference",
                @"
<Project DefaultTargets=`DefaultTarget` InitialTargets=`InitialTarget`>

  <Target Name=`A` Condition=`true == false`/>

  <Target Name=`DefaultTarget` Condition=`true == false`/>

  <Target Name=`InitialTarget` Condition=`true == false`/>

</Project>
".Cleanup()).Path;

            var projectFile = _env.CreateFile(
                "proj",
                $@"
<Project DefaultTargets=`Build`>

  <ItemGroup>
    <ProjectReference Include=`{referenceFile}` />
  </ItemGroup>

  <Target Name=`Build`>
    <MSBuild Projects=`@(ProjectReference)` Targets=`A` />
    <MSBuild Projects=`@(ProjectReference)` />
  </Target>

</Project>
".Cleanup()).Path;

            _buildParametersPrototype.ProjectIsolationMode.ShouldBe(ProjectIsolationMode.True);
            var buildParameters = _buildParametersPrototype.Clone();

            using (var buildManagerSession = new Helpers.BuildManagerSession(_env, buildParameters))
            {
                // seed caches with results from the reference
                buildManagerSession.BuildProjectFile(referenceFile).OverallResult.ShouldBe(BuildResultCode.Success);
                buildManagerSession.BuildProjectFile(referenceFile, new[] { "A" }).OverallResult.ShouldBe(BuildResultCode.Success);

                buildManagerSession.BuildProjectFile(projectFile).OverallResult.ShouldBe(BuildResultCode.Success);

                buildManagerSession.Logger.WarningCount.ShouldBe(0);
                buildManagerSession.Logger.ErrorCount.ShouldBe(0);
                // twice for the initial target, once for A, once for DefaultTarget
                buildManagerSession.Logger.AssertMessageCount("Previously built successfully", 4);
            }
        }
    }
}
