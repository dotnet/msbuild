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

namespace Microsoft.Build.Experimental.Graph.UnitTests
{
    public class IsolateProjectsTests
    {
        private readonly string _project = @"
                <Project DefaultTargets='BuildSelf'>

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
                </Project>";

        private readonly string _declaredReference = @"
                <Project>
                    <Target Name='DeclaredReferenceTarget'>
                    </Target>
                </Project>";

        private readonly string _undeclaredReference = @"
                <Project>
                    <Target Name='UndeclaredReferenceTarget'>
                    </Target>
                </Project>";

        private readonly ITestOutputHelper _testOutput;

        public IsolateProjectsTests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        [Theory]
        [InlineData(BuildResultCode.Success, new string[] { })]
        [InlineData(BuildResultCode.Success, new[] {"BuildSelf"})]
        public void CacheAndTaskEnforcementShouldAcceptSelfReferences(BuildResultCode expectedBuildResult, string[] targets)
        {
            AssertBuild(targets,
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(expectedBuildResult);

                    logger.Errors.ShouldBeEmpty();
                });
        }

        [Fact]
        public void CacheAndTaskEnforcementShouldAcceptCallTarget()
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
            CacheEnforcementShouldFailWhenReferenceWasNotPreviouslyBuilt2(addContinueOnError: true);
        }

        [Fact]
        public void CacheEnforcementShouldFailWhenReferenceWasNotPreviouslyBuiltWithoutContinueOnError()
        {
            CacheEnforcementShouldFailWhenReferenceWasNotPreviouslyBuilt2(addContinueOnError: false);
        }

        private void CacheEnforcementShouldFailWhenReferenceWasNotPreviouslyBuilt2(bool addContinueOnError)
        {
            AssertBuild(
                new[] {"BuildDeclaredReference"},
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Failure);

                    logger.ErrorCount.ShouldBe(1);

                    logger.Errors.First()
                        .Message.ShouldStartWith("MSB4252:");
                },
                addContinueOnError: addContinueOnError);
        }

        [Fact]
        public void CacheEnforcementShouldAcceptPreviouslyBuiltReferences()
        {
            AssertBuild(new []{"BuildDeclaredReference"},
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Success);

                    logger.Errors.ShouldBeEmpty();
                },
                buildDeclaredReference: true);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TaskEnforcementShouldFailOnUndeclaredReference(bool addContinueOnError)
        {
            AssertBuild(new[] { "BuildUndeclaredReference" },
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Failure);

                    logger.ErrorCount.ShouldBe(1);

                    logger.Errors.First().Message.ShouldStartWith("MSB4254:");
                },
                addContinueOnError: addContinueOnError);
        }

        [Fact]
        public void TaskEnforcementShouldFailOnPreviouslyBuiltButUndeclaredReferences()
        {
            AssertBuild(new[] { "BuildUndeclaredReference" },
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Failure);

                    logger.ErrorCount.ShouldBe(1);

                    logger.Errors.First().Message.ShouldStartWith("MSB4254:");
                },
                buildUndeclaredReference: true);
        }

        public static IEnumerable<object[]> TaskEnforcementShouldNormalizeFilePathsTestData
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

                var functions = new[] {Preserve, FullToRelative, ToForwardSlash, ToBackSlash, ToDuplicateSlashes};

                foreach (var projectReferenceModifier in functions)
                {
                    foreach (var msbuildProjectModifier in functions)
                    {
                        yield return new object[]
                        {
                            projectReferenceModifier,
                            msbuildProjectModifier
                        };
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(TaskEnforcementShouldNormalizeFilePathsTestData))]
        public void TaskEnforcementShouldNormalizeFilePaths(Func<string, string> projectReferenceModifier, Func<string, string> msbuildProjectModifier)
        {
            AssertBuild(new []{"BuildDeclaredReference"},
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Success);

                    logger.Errors.ShouldBeEmpty();
                },
                buildDeclaredReference: true,
                buildUndeclaredReference: false,
                addContinueOnError: false,
                projectReferenceModifier,
                msbuildProjectModifier);
        }

        private void AssertBuild(
            string[] targets,
            Action<BuildResult, MockLogger> assert,
            bool buildDeclaredReference = false,
            bool buildUndeclaredReference = false,
            bool addContinueOnError = false,
            Func<string, string> projectReferenceModifier = null,
            Func<string, string> msbuildOnDeclaredReferenceModifier = null)
        {
            using (var env = TestEnvironment.Create())
            using (var buildManager = new BuildManager())
            {
                if (NativeMethodsShared.IsOSX)
                {
                    // OSX links /var into /private, which makes Path.GetTempPath() to return "/var..." but Directory.GetCurrentDirectory to return "/private/var..."
                    // this discrepancy fails the msbuild task enforcements due to failed path equality checks
                    env.SetTempPath(Path.Combine(Directory.GetCurrentDirectory(), Guid.NewGuid().ToString("N")), deleteTempDirectory:true);
                }

                var projectFile = CreateTmpFile(env).Path;
                var declaredReferenceFile = CreateTmpFile(env).Path;
                var undeclaredReferenceFile = CreateTmpFile(env).Path;

                File.WriteAllText(
                    projectFile,
                    string.Format(
                        _project,
                        projectReferenceModifier?.Invoke(declaredReferenceFile) ?? declaredReferenceFile,
                        msbuildOnDeclaredReferenceModifier?.Invoke(declaredReferenceFile) ?? declaredReferenceFile,
                        undeclaredReferenceFile,
                        addContinueOnError ? "ContinueOnError='WarnAndContinue'" : string.Empty));

                File.WriteAllText(declaredReferenceFile, _declaredReference);
                File.WriteAllText(undeclaredReferenceFile, _undeclaredReference);

                var logger = new MockLogger(_testOutput);

                // todo investigate why out of proc builds fail on macos https://github.com/Microsoft/msbuild/issues/3915
                var disableInProcNode = !NativeMethodsShared.IsOSX;

                var buildParameters = new BuildParameters
                {
                    IsolateProjects = true,
                    Loggers = new ILogger[] {logger},
                    EnableNodeReuse = false,
                    DisableInProcNode = disableInProcNode
                };

                var rootRequest = new BuildRequestData(
                    projectFile,
                    new Dictionary<string, string>(),
                    MSBuildConstants.CurrentToolsVersion,
                    targets,
                    null);

                try
                {
                    buildManager.BeginBuild(buildParameters);

                    if (buildDeclaredReference)
                    {
                        buildManager.BuildRequest(
                            new BuildRequestData(
                                declaredReferenceFile,
                                new Dictionary<string, string>(),
                                MSBuildConstants.CurrentToolsVersion,
                                new[] {"DeclaredReferenceTarget"},
                                null))
                            .OverallResult.ShouldBe(BuildResultCode.Success);
                    }

                    if (buildUndeclaredReference)
                    {
                        buildManager.BuildRequest(
                            new BuildRequestData(
                                undeclaredReferenceFile,
                                new Dictionary<string, string>(),
                                MSBuildConstants.CurrentToolsVersion,
                                new[] {"UndeclaredReferenceTarget"},
                                null))
                            .OverallResult.ShouldBe(BuildResultCode.Success);
                    }

                    var result = buildManager.BuildRequest(rootRequest);

                    assert(result, logger);
                }
                finally
                {
                    buildManager.EndBuild();
                }
            }

            TransientTestFile CreateTmpFile(TestEnvironment env)
            {
                return NativeMethodsShared.IsMono && NativeMethodsShared.IsOSX
                                                ? env.CreateFile(new TransientTestFolder(Path.Combine(Directory.GetCurrentDirectory(), Guid.NewGuid().ToString("N"))))
                                                : env.CreateFile();
            }
        }
    }
}
