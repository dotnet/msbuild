// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

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
            _env = TestEnvironment.Create(_testOutput);

            if (NativeMethodsShared.IsOSX)
            {
                // OSX links /var into /private, which makes Path.GetTempPath() to return "/var..." but Directory.GetCurrentDirectory to return "/private/var..."
                // this discrepancy fails the msbuild undeclared reference enforcements due to failed path equality checks
                _env.SetTempPath(Path.Combine(Directory.GetCurrentDirectory(), Guid.NewGuid().ToString("N")), deleteTempDirectory:true);
            }

            // todo investigate why out of proc builds fail on macos https://github.com/Microsoft/msbuild/issues/3915
            var disableInProcNode = !NativeMethodsShared.IsOSX;

            _buildParametersPrototype = new BuildParameters
            {
                EnableNodeReuse = false,
                IsolateProjects = true,
                DisableInProcNode = disableInProcNode
            };
        }

        public void Dispose()
        {
            _env.Dispose();
        }

        

        [Theory]
        [InlineData(BuildResultCode.Success, new string[] { })]
        [InlineData(BuildResultCode.Success, new[] {"BuildSelf"})]
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
            AssertBuild(new []{"CallTarget"},
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Success);

                    logger.Errors.ShouldBeEmpty();
                });
        }

        [Fact(Skip = "https://github.com/Microsoft/msbuild/issues/3876")]
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
                new[] {"BuildDeclaredReference"},
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
                isolateProjects: false);
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
            AssertBuild(new []{ targetName },
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Success);

                    logger.Errors.ShouldBeEmpty();
                },
                buildDeclaredReference: true);
        }

        [Theory]
        [InlineData(false, "BuildUndeclaredReference")]
//        [InlineData(false, "BuildUndeclaredReferenceViaTask")] https://github.com/microsoft/msbuild/issues/4385
        [InlineData(true, "BuildUndeclaredReference")]
//        [InlineData(true, "BuildUndeclaredReferenceViaTask")] https://github.com/microsoft/msbuild/issues/4385
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
//        [InlineData("BuildUndeclaredReferenceViaTask")] https://github.com/microsoft/msbuild/issues/4385
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

                var targetNames = new []{"BuildDeclaredReference", /*"BuildDeclaredReferenceViaTask"*/};

                var functions = new[] {Preserve, FullToRelative, ToForwardSlash, ToBackSlash, ToDuplicateSlashes};

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
            AssertBuild(new []{targetName},
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
            bool isolateProjects = true,
            Func<string, string> projectReferenceModifier = null,
            Func<string, string> msbuildOnDeclaredReferenceModifier = null)
        {
            var rootProjectFile = _env.CreateFile().Path;
            var declaredReferenceFile = _env.CreateFile().Path;
            var undeclaredReferenceFile = _env.CreateFile().Path;

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
            buildParameters.IsolateProjects = isolateProjects;

            using (var buildManagerSession = new Helpers.BuildManagerSession(_env, buildParameters))
            {
                if (buildDeclaredReference)
                {
                    buildManagerSession.BuildProjectFile(declaredReferenceFile, new[] {"DeclaredReferenceTarget"})
                        .OverallResult.ShouldBe(BuildResultCode.Success);
                }

                if (buildUndeclaredReference)
                {
                    buildManagerSession.BuildProjectFile(undeclaredReferenceFile, new[] {"UndeclaredReferenceTarget"})
                        .OverallResult.ShouldBe(BuildResultCode.Success);
                }

                var result = buildManagerSession.BuildProjectFile(rootProjectFile, targets);

                assert(result, buildManagerSession.Logger);
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

            _buildParametersPrototype.IsolateProjects.ShouldBeTrue();

            using (var buildManagerSession = new Helpers.BuildManagerSession(_env, _buildParametersPrototype))
            {
                // seed caches with results from the reference
                buildManagerSession.BuildProjectFile(referenceFile).OverallResult.ShouldBe(BuildResultCode.Success);
                buildManagerSession.BuildProjectFile(referenceFile, new []{"A"}).OverallResult.ShouldBe(BuildResultCode.Success);

                buildManagerSession.BuildProjectFile(projectFile).OverallResult.ShouldBe(BuildResultCode.Success);

                buildManagerSession.Logger.WarningCount.ShouldBe(0);
                buildManagerSession.Logger.ErrorCount.ShouldBe(0);
                // twice for the initial target, once for A, once for DefaultTarget
                buildManagerSession.Logger.AssertMessageCount("Previously built successfully", 4);
            }
        }
    }
}
