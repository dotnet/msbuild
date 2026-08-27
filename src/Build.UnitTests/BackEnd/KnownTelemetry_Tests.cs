// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable
using System;
using System.Globalization;
using Microsoft.Build.Framework.Telemetry;
using Shouldly;
using Xunit;
using static Microsoft.Build.BackEnd.Logging.BuildErrorTelemetryTracker;
using static Microsoft.Build.Framework.Telemetry.BuildInsights;
using static Microsoft.Build.Framework.Telemetry.TelemetryDataUtils;

namespace Microsoft.Build.UnitTests.Telemetry;

public class KnownTelemetry_Tests
{
    [Fact]
    public void BuildTelemetryCanBeSetToNull()
    {
        KnownTelemetry.PartialBuildTelemetry = new BuildTelemetry();
        KnownTelemetry.PartialBuildTelemetry = null;

        KnownTelemetry.PartialBuildTelemetry.ShouldBeNull();
    }

    [Fact]
    public void BuildTelemetryCanBeSet()
    {
        BuildTelemetry buildTelemetry = new BuildTelemetry();
        KnownTelemetry.PartialBuildTelemetry = buildTelemetry;

        KnownTelemetry.PartialBuildTelemetry.ShouldBeSameAs(buildTelemetry);
    }

    [Fact]
    public void BuildTelemetryConstructedHasNoProperties()
    {
        BuildTelemetry buildTelemetry = new BuildTelemetry();

        buildTelemetry.BuildEngineDisplayVersion.ShouldBeNull();
        buildTelemetry.EventName.ShouldBe("build");
        buildTelemetry.FinishedAt.ShouldBeNull();
        buildTelemetry.BuildEngineFrameworkName.ShouldBeNull();
        buildTelemetry.BuildEngineHost.ShouldBeNull();
        buildTelemetry.InitialMSBuildServerState.ShouldBeNull();
        buildTelemetry.InnerStartAt.ShouldBeNull();
        buildTelemetry.ProjectPath.ShouldBeNull();
        buildTelemetry.ServerFallbackReason.ShouldBeNull();
        buildTelemetry.StartAt.ShouldBeNull();
        buildTelemetry.BuildSuccess.ShouldBeNull();
        buildTelemetry.BuildTarget.ShouldBeNull();
        buildTelemetry.BuildEngineVersion.ShouldBeNull();
        buildTelemetry.BuildCheckEnabled.ShouldBeNull();
        buildTelemetry.MultiThreadedModeEnabled.ShouldBeNull();
        buildTelemetry.SACEnabled.ShouldBeNull();

        buildTelemetry.GetProperties().ShouldBeEmpty();
    }

    [Fact]
    public void BuildTelemetryCreateProperProperties()
    {
        BuildTelemetry buildTelemetry = new BuildTelemetry();

        DateTime startAt = new DateTime(2023, 01, 02, 10, 11, 22);
        DateTime innerStartAt = new DateTime(2023, 01, 02, 10, 20, 30);
        DateTime finishedAt = new DateTime(2023, 12, 13, 14, 15, 16);

        buildTelemetry.BuildEngineDisplayVersion = "Some Display Version";
        buildTelemetry.FinishedAt = finishedAt;
        buildTelemetry.BuildEngineFrameworkName = "new .NET";
        buildTelemetry.BuildEngineHost = "Host description";
        buildTelemetry.InitialMSBuildServerState = "hot";
        buildTelemetry.InnerStartAt = innerStartAt;
        buildTelemetry.ProjectPath = "C:/dev/theProject";
        buildTelemetry.ServerFallbackReason = "busy";
        buildTelemetry.StartAt = startAt;
        buildTelemetry.BuildSuccess = true;
        buildTelemetry.BuildTarget = "Clean";
        buildTelemetry.BuildEngineVersion = new Version(1, 2, 3, 4);
        buildTelemetry.BuildCheckEnabled = true;
        buildTelemetry.MultiThreadedModeEnabled = false;
        buildTelemetry.SACEnabled = true;

        var properties = buildTelemetry.GetProperties();

        properties.Count.ShouldBe(14);

        properties["BuildEngineDisplayVersion"].ShouldBe("Some Display Version");
        properties["BuildEngineFrameworkName"].ShouldBe("new .NET");
        properties["BuildEngineHost"].ShouldBe("Host description");
        properties["InitialMSBuildServerState"].ShouldBe("hot");
        properties["ProjectPath"].ShouldBe("theProject");
        properties["ServerFallbackReason"].ShouldBe("busy");
        properties["BuildSuccess"].ShouldBe("True");
        properties["BuildTarget"].ShouldBe("Clean");
        properties["BuildEngineVersion"].ShouldBe("1.2.3.4");
        properties["BuildCheckEnabled"].ShouldBe("True");
        properties["MultiThreadedModeEnabled"].ShouldBe("False");
        properties["SACEnabled"].ShouldBe("True");

        // verify computed
        properties["BuildDurationInMilliseconds"] = (finishedAt - startAt).TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
        properties["InnerBuildDurationInMilliseconds"] = (finishedAt - innerStartAt).TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
    }

    [Fact]
    public void BuildTelemetryHandleNullsInRecordedTimes()
    {
        BuildTelemetry buildTelemetry = new BuildTelemetry();

        buildTelemetry.StartAt = DateTime.MinValue;
        buildTelemetry.FinishedAt = null;
        buildTelemetry.GetProperties().ShouldBeEmpty();

        buildTelemetry.StartAt = null;
        buildTelemetry.FinishedAt = DateTime.MaxValue;
        buildTelemetry.GetProperties().ShouldBeEmpty();

        buildTelemetry.InnerStartAt = DateTime.MinValue;
        buildTelemetry.FinishedAt = null;
        buildTelemetry.GetProperties().ShouldBeEmpty();

        buildTelemetry.InnerStartAt = null;
        buildTelemetry.FinishedAt = DateTime.MaxValue;
        buildTelemetry.GetProperties().ShouldBeEmpty();
    }

    [Fact]
    public void BuildTelemetryIncludesFailureCategoryProperties()
    {
        BuildTelemetry buildTelemetry = new BuildTelemetry();

        buildTelemetry.BuildSuccess = false;
        buildTelemetry.FailureCategory = nameof(ErrorCategory.Compiler);
        buildTelemetry.ErrorCounts = new ErrorCountsInfo(
            Compiler: 5,
            MsBuildGeneral: 2,
            MsBuildEvaluation: null,
            MsBuildExecution: null,
            MsBuildGraph: null,
            Task: 1,
            SdkResolvers: null,
            NetSdk: null,
            NuGet: 3,
            BuildCheck: null,
            NativeToolchain: null,
            CodeAnalysis: null,
            Razor: null,
            Wpf: null,
            AspNet: null,
            Other: 1);

        var properties = buildTelemetry.GetProperties();

        properties["BuildSuccess"].ShouldBe("False");
        properties["FailureCategory"].ShouldBe(nameof(ErrorCategory.Compiler));
        properties.ContainsKey("ErrorCounts").ShouldBeTrue();

        var activityProperties = buildTelemetry.GetActivityProperties();
        activityProperties["FailureCategory"].ShouldBe(nameof(ErrorCategory.Compiler));
        var errorCounts = activityProperties["ErrorCounts"] as ErrorCountsInfo;
        errorCounts.ShouldNotBeNull();
        errorCounts.Compiler.ShouldBe(5);
        errorCounts.MsBuildGeneral.ShouldBe(2);
        errorCounts.Task.ShouldBe(1);
        errorCounts.NuGet.ShouldBe(3);
        errorCounts.Other.ShouldBe(1);
        errorCounts.SdkResolvers.ShouldBeNull();
        errorCounts.NetSdk.ShouldBeNull();
        errorCounts.BuildCheck.ShouldBeNull();
    }

    [Fact]
    public void BuildTelemetryActivityPropertiesIncludesFailureData()
    {
        BuildTelemetry buildTelemetry = new BuildTelemetry();

        buildTelemetry.BuildSuccess = false;
        buildTelemetry.FailureCategory = nameof(ErrorCategory.Tasks);
        buildTelemetry.ErrorCounts = new ErrorCountsInfo(
            Compiler: null,
            MsBuildGeneral: null,
            MsBuildEvaluation: null,
            MsBuildExecution: null,
            MsBuildGraph: null,
            Task: 10,
            SdkResolvers: null,
            NetSdk: null,
            NuGet: null,
            BuildCheck: null,
            NativeToolchain: null,
            CodeAnalysis: null,
            Razor: null,
            Wpf: null,
            AspNet: null,
            Other: null);

        var activityProperties = buildTelemetry.GetActivityProperties();

        activityProperties["BuildSuccess"].ShouldBe(false);
        activityProperties["FailureCategory"].ShouldBe(nameof(ErrorCategory.Tasks));
        var errorCounts = activityProperties["ErrorCounts"] as ErrorCountsInfo;
        errorCounts.ShouldNotBeNull();
        errorCounts.Task.ShouldBe(10);
    }

    [Fact]
    public void BuildTelemetryProjectPathEmitsOnlyFileName()
    {
        BuildTelemetry buildTelemetry = new BuildTelemetry();
        buildTelemetry.ProjectPath = "C:/Users/useralias/repos/MyProject/MyProject.csproj";
        buildTelemetry.StartAt = DateTime.UtcNow;
        buildTelemetry.FinishedAt = DateTime.UtcNow;

        var properties = buildTelemetry.GetProperties();

        // Should only contain the file name, not the directory path
        properties["ProjectPath"].ShouldBe("MyProject.csproj");
        properties["ProjectPath"].ShouldNotContain("useralias");
        properties["ProjectPath"].ShouldNotContain("Users");
    }

    [Fact]
    public void BuildTelemetryBuildTargetHashesCustomTargets()
    {
        BuildTelemetry buildTelemetry = new BuildTelemetry();
        buildTelemetry.BuildTarget = "MySecretCustomTarget";
        buildTelemetry.StartAt = DateTime.UtcNow;
        buildTelemetry.FinishedAt = DateTime.UtcNow;

        var properties = buildTelemetry.GetProperties();

        // Custom target name should be hashed
        properties["BuildTarget"].ShouldNotBe("MySecretCustomTarget");
        properties["BuildTarget"].ShouldBe(GetHashed("MySecretCustomTarget"));
    }

    [Fact]
    public void BuildTelemetryBuildTargetPreservesKnownTargets()
    {
        string[] knownTargets = { "Build", "Clean", "Rebuild", "Restore", "Pack", "Publish", "Test" };

        foreach (string target in knownTargets)
        {
            BuildTelemetry buildTelemetry = new BuildTelemetry();
            buildTelemetry.BuildTarget = target;
            buildTelemetry.StartAt = DateTime.UtcNow;
            buildTelemetry.FinishedAt = DateTime.UtcNow;

            var properties = buildTelemetry.GetProperties();
            properties["BuildTarget"].ShouldBe(target, $"Known target '{target}' should not be hashed");
        }
    }

    [Fact]
    public void BuildTelemetryActivityPropertiesHashCustomTarget()
    {
        BuildTelemetry buildTelemetry = new BuildTelemetry();
        buildTelemetry.BuildTarget = "InternalCustomTarget";

        var activityProperties = buildTelemetry.GetActivityProperties();

        activityProperties["BuildTarget"].ShouldBe(GetHashed("InternalCustomTarget"));
    }

    [Fact]
    public void BuildTelemetryBuildTargetHandlesCommaSeparatedTargets()
    {
        BuildTelemetry buildTelemetry = new BuildTelemetry();
        buildTelemetry.BuildTarget = "Build,Clean";
        buildTelemetry.StartAt = DateTime.UtcNow;
        buildTelemetry.FinishedAt = DateTime.UtcNow;

        var properties = buildTelemetry.GetProperties();

        // Both known targets should be preserved individually, not hashed as a whole string
        properties["BuildTarget"].ShouldBe("Build,Clean");
    }

    [Fact]
    public void BuildTelemetryBuildTargetHashesMixedTargets()
    {
        BuildTelemetry buildTelemetry = new BuildTelemetry();
        buildTelemetry.BuildTarget = "Build,MyCustomTarget,Restore";
        buildTelemetry.StartAt = DateTime.UtcNow;
        buildTelemetry.FinishedAt = DateTime.UtcNow;

        var properties = buildTelemetry.GetProperties();

        // Known targets preserved, custom target hashed
        properties["BuildTarget"].ShouldBe($"Build,{GetHashed("MyCustomTarget")},Restore");
    }
}
