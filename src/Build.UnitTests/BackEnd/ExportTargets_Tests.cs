// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    public class ExportTargets_Tests
    {
        [Theory]
        [InlineData(true, new[] {"Exported", "Build"}, new[] {"Build", "DependedUponByExported", "BeforeExported", "Exported", "AfterExported"})]
        [InlineData(false, new[] {"Build"}, new[] {"Build"})]
        public void ExportTargetsShouldGetExecutedOnlyDuringIsolatedProjectBuilds(
            bool isIsolatedProjectBuild,
            string[] expectedBuildResultTargets,
            string[] expectedExecutedTargets)
        {
            BuildProject(
                @"<Project DefaultTargets='Build'>
                    <ItemGroup>
                        <ExportTargets Include='Exported' />
                    </ItemGroup>

                    <Target Name='Exported' DependsOnTargets='DependedUponByExported'>
                    </Target>

                    <Target Name='AfterExported' AfterTargets='Exported'>
                    </Target>

                    <Target Name='BeforeExported' BeforeTargets='Exported'>
                    </Target>

                    <Target Name='DependedUponByExported'>
                    </Target>

                    <Target Name='Build'>
                    </Target>
                </Project>",
                new BuildParameters
                {
                    // ensures export targets work out of proc
                    DisableInProcNode = true,
                    IsolateProjects = isIsolatedProjectBuild
                },
                result =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Success);
                    result.ResultsByTarget.Keys
                        .OrderBy(_ => _)
                        .ShouldBe(expectedBuildResultTargets.OrderBy(_ => _));
                },
                logger =>
                {
                    logger.TargetFinishedEvents
                        .Select(e => e.TargetName)
                        .ShouldBe(expectedExecutedTargets);
                });
        }

        [Fact]
        public void ExportTargetsCanContainDefaultTargets()
        {
            var expectedTargets = new []{"Build"};

            BuildProject(
                @"<Project DefaultTargets='Build'>
                    <ItemGroup>
                        <ExportTargets Include='Build' />
                    </ItemGroup>

                    <Target Name='Build'>
                    </Target>
                </Project>",
                new BuildParameters
                {
                    // ensures export targets work out of proc
                    DisableInProcNode = true
                },
                result =>
                {
                    result.OverallResult.ShouldBe(BuildResultCode.Success);
                    result.ResultsByTarget.Keys.ShouldBe(expectedTargets);
                },
                logger =>
                {
                    logger.TargetFinishedEvents
                        .Select(e => e.TargetName)
                        .ShouldBe(expectedTargets);
                });
        }

        private static void BuildProject(
            string projectContents,
            BuildParameters buildParameters,
            Action<BuildResult> buildResultAssert,
            Action<MockLogger> loggerAssert)
        {
            using (var env = TestEnvironment.Create())
            using (var buildManager = new BuildManager())
            {
                var testProject = env.CreateTestProjectWithFiles(
                    projectContents);

                var projectCollection = env.CreateProjectCollection()
                    .Collection;

                var logger = new MockLogger();
                projectCollection.RegisterLogger(logger);

                var projectInstance = new ProjectInstance(testProject.ProjectFile, null, MSBuildConstants.CurrentToolsVersion, projectCollection);

                buildParameters.Loggers = buildParameters.Loggers == null
                    ? new[] {logger}
                    : buildParameters.Loggers.Concat(new[] {logger});

                var buildResult = buildManager.Build(
                    buildParameters,
                    new BuildRequestData(
                        projectInstance,
                        new string[] {},
                        null
                        ));

                buildResultAssert(buildResult);

                loggerAssert(logger);
            }
        }
    }
}
