// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Graph;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.BuildCheck.UnitTests;

public class BuildCheckLifecycleTests(ITestOutputHelper output)
{
    private const string CallbackFailure = "something went wrong when executing registered action";

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void MixedGraphConstructionAndBuildReportCallbackFailureOnceBeforeBuildFinished(
        bool graphFirst, bool promoteWarnings)
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        var folder = env.CreateFolder(createFolder: true);
        string assemblyPath = Path.Combine(AppContext.BaseDirectory, "ErrorCustomCheck.dll");
        File.Exists(assemblyPath).ShouldBeTrue();
        string graphProject = env.CreateFile(folder, "Graph.proj", $"""
            <Project DefaultTargets="Build">
              <PropertyGroup>
                <_Registered>$([MSBuild]::RegisterBuildCheck('{assemblyPath}'))</_Registered>
              </PropertyGroup>
              <Target Name="Build" />
            </Project>
            """).Path;
        string ordinaryProject = env.CreateFile(folder, "Ordinary.proj", """
            <Project DefaultTargets="Build">
              <Target Name="Build" />
            </Project>
            """).Path;
        env.CreateFile(folder, ".editorconfig", """
            root = true

            [Graph.proj]
            build_check.X01237.severity = warning
            """);
        using var collection = new ProjectCollection();
        using var manager = new BuildManager();
        var logger = new MockLogger(output);
        BuildParameters parameters = CreateParameters(collection, logger, checkingEnabled: true);
        if (promoteWarnings)
        {
            parameters.WarningsAsErrors = new HashSet<string>();
        }

        var graphRequest = new GraphBuildRequestData(
            [new ProjectGraphEntryPoint(graphProject, new Dictionary<string, string>())], ["Build"], null,
            BuildRequestDataFlags.None, new GraphBuildOptions { Build = false });
        var ordinaryRequest = new BuildRequestData(
            ordinaryProject, new Dictionary<string, string?>(), null, ["Build"], null);

        logger.AllBuildEvents.ShouldBeEmpty();
        logger.Warnings.ShouldBeEmpty();
        logger.Errors.ShouldBeEmpty();
        manager.BeginBuild(parameters);
        try
        {
            if (graphFirst)
            {
                manager.BuildRequest(graphRequest);
                manager.BuildRequest(ordinaryRequest);
            }
            else
            {
                manager.BuildRequest(ordinaryRequest);
                manager.BuildRequest(graphRequest);
            }
        }
        finally
        {
            manager.EndBuild();
        }

        logger.EvaluationFinishedEvents.ShouldContain(e => e.ProjectFile == graphProject);
        logger.ProjectStartedEvents.ShouldHaveSingleItem().ProjectFile.ShouldBe(ordinaryProject);
        logger.TargetFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeTrue();

        BuildEventArgs diagnostic;
        if (promoteWarnings)
        {
            logger.Warnings.ShouldBeEmpty();
            BuildErrorEventArgs error = logger.Errors.ShouldHaveSingleItem();
            error.Message.ShouldNotBeNull().ShouldContain(CallbackFailure);
            error.File.ShouldBe(graphProject);
            diagnostic = error;
        }
        else
        {
            logger.Errors.ShouldBeEmpty();
            BuildWarningEventArgs warning = logger.Warnings.ShouldHaveSingleItem();
            warning.Message.ShouldNotBeNull().ShouldContain(CallbackFailure);
            warning.File.ShouldBe(graphProject);
            diagnostic = warning;
        }

        BuildFinishedEventArgs finished = logger.BuildFinishedEvents.ShouldHaveSingleItem();
        logger.AllBuildEvents.IndexOf(diagnostic).ShouldBeLessThan(logger.AllBuildEvents.IndexOf(finished));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReusedBuildManagerHonorsCheckingForEachBuild(bool includeDisabledBuild)
    {
        using TestEnvironment env = TestEnvironment.Create(output);
        string errorCheckPath = Path.Combine(AppContext.BaseDirectory, "ErrorCustomCheck.dll");
        string ordinaryCheckPath = Path.Combine(AppContext.BaseDirectory, "CustomCheck.dll");
        File.Exists(errorCheckPath).ShouldBeTrue();
        File.Exists(ordinaryCheckPath).ShouldBeTrue();
        using var manager = new BuildManager();
        bool[] checkingSequence = includeDisabledBuild ? [true, false, true] : [true, true];

        foreach (bool checkingEnabled in checkingSequence)
        {
            var folder = env.CreateFolder(createFolder: true);
            string project = env.CreateFile(folder, "Build.proj", $"""
                <Project DefaultTargets="Build">
                  <PropertyGroup>
                    <_ErrorCheck>$([MSBuild]::RegisterBuildCheck('{errorCheckPath}'))</_ErrorCheck>
                    <_OrdinaryCheck>$([MSBuild]::RegisterBuildCheck('{ordinaryCheckPath}'))</_OrdinaryCheck>
                  </PropertyGroup>
                  <Target Name="Build" />
                </Project>
                """).Path;
            env.CreateFile(folder, ".editorconfig", """
                root = true

                [Build.proj]
                build_check.X01237.severity = warning
                build_check.X01235.severity = warning
                """);
            using var collection = new ProjectCollection();
            var logger = new MockLogger(output);
            BuildParameters parameters = CreateParameters(collection, logger, checkingEnabled);
            var request = new BuildRequestData(
                project, new Dictionary<string, string?>(), null, ["Build"], null);

            logger.AllBuildEvents.ShouldBeEmpty();
            logger.Warnings.ShouldBeEmpty();
            logger.Errors.ShouldBeEmpty();
            BuildResult result = manager.Build(parameters, request);

            result.OverallResult.ShouldBe(BuildResultCode.Success);
            logger.Errors.ShouldBeEmpty();
            logger.ProjectStartedEvents.ShouldHaveSingleItem().ProjectFile.ShouldBe(project);
            logger.TargetFinishedEvents.ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
            BuildFinishedEventArgs finished = logger.BuildFinishedEvents.ShouldHaveSingleItem();
            finished.Succeeded.ShouldBeTrue();
            if (checkingEnabled)
            {
                logger.Warnings.Count.ShouldBe(2);
                BuildWarningEventArgs infrastructureWarning = logger.Warnings
                    .Where(w => w.Message?.Contains(CallbackFailure) == true).ShouldHaveSingleItem();
                infrastructureWarning.File.ShouldBe(project);
                BuildWarningEventArgs ruleWarning = logger.Warnings
                    .Where(w => w.Code == "X01235").ShouldHaveSingleItem();
                ruleWarning.Message.ShouldBe("Message format: Argument for the message format");
                ruleWarning.ProjectFile.ShouldBe(project);
                logger.AllBuildEvents.IndexOf(infrastructureWarning).ShouldBeLessThan(
                    logger.AllBuildEvents.IndexOf(finished));
                logger.AllBuildEvents.IndexOf(ruleWarning).ShouldBeLessThan(
                    logger.AllBuildEvents.IndexOf(finished));
            }
            else
            {
                logger.Warnings.ShouldBeEmpty();
                logger.FullLog.ShouldNotContain(CallbackFailure);
            }
        }
    }

    private static BuildParameters CreateParameters(
        ProjectCollection collection, MockLogger logger, bool checkingEnabled) => new(collection)
        {
            IsBuildCheckEnabled = checkingEnabled,
            Loggers = [logger],
            MaxNodeCount = 1,
            DisableInProcNode = false,
            EnableNodeReuse = false,
            ShutdownInProcNodeOnBuildFinish = true,
            UseSynchronousLogging = false,
        };
}
