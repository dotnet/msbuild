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
    public class IsolateProjectsTests
    {
        private readonly string _project = @"
                <Project DefaultTargets='BuildSelf'>
                    <Target Name='BuildReference'>
                        <MSBuild Projects='{0}' Targets='ReferenceTarget'/>
                    </Target>

                    <Target Name='BuildSelf'>
                        <MSBuild Projects='$(MSBuildThisFile)' Targets='SelfTarget'/>
                    </Target>

                    <Target Name='SelfTarget'>
                    </Target>
                </Project>";

        private readonly string _reference = @"
                <Project>
                    <Target Name='ReferenceTarget'>
                    </Target>
                </Project>";

        private readonly ITestOutputHelper _testOutput;

        public IsolateProjectsTests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        [Theory]
        [InlineData(BuildResultCode.Success, new string[] {})]
        [InlineData(BuildResultCode.Success, new[] {"BuildSelf"})]
        public void CacheEnforcementShouldAcceptSelfReferences(BuildResultCode expectedBuildResult, string[] targets)
        {
            AssertBuild(targets,
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Success);

                    logger.Errors.ShouldBeEmpty();
                });
        }

        [Fact]
        public void CacheEnforcementShouldFailWhenReferenceWasNotPreviouslyBuilt()
        {
            AssertBuild(new []{"BuildReference"},
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Failure);

                    logger.ErrorCount.ShouldBe(1);

                    logger.Errors.First().Message.ShouldStartWith("MSB4252:");
                });
        }

        [Fact]
        public void CacheEnforcementShouldAcceptPreviouslyBuiltReferences()
        {
            AssertBuild(new []{"BuildReference"},
                (result, logger) =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Success);

                    logger.Errors.ShouldBeEmpty();
                },
                buildReference: true);
        }

        private void AssertBuild(string[] targets, Action<BuildResult, MockLogger> assert, bool buildReference = false)
        {
            using (var env = TestEnvironment.Create())
            using (var buildManager = new BuildManager())
            {
                var projectFile = env.CreateFile().Path;
                var referenceFile = env.CreateFile().Path;

                File.WriteAllText(projectFile, string.Format(_project, referenceFile));
                File.WriteAllText(referenceFile, _reference);

                var logger = new MockLogger(_testOutput);

                var buildParameters = new BuildParameters
                {
                    IsolateProjects = true,
                    Loggers = new ILogger[] {logger},
                    EnableNodeReuse = false,
                    DisableInProcNode = true
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

                    if (buildReference)
                    {
                        buildManager.BuildRequest(
                            new BuildRequestData(
                                referenceFile,
                                new Dictionary<string, string>(),
                                MSBuildConstants.CurrentToolsVersion,
                                new[] {"ReferenceTarget"},
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
        }
    }
}
