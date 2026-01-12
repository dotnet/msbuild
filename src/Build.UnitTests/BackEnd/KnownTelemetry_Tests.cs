// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable
using System;
using System.Globalization;
using Microsoft.Build.Framework.Telemetry;
using Shouldly;
using Xunit;

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
        buildTelemetry.ProjectPath = @"C:\\dev\\theProject";
        buildTelemetry.ServerFallbackReason = "busy";
        buildTelemetry.StartAt = startAt;
        buildTelemetry.BuildSuccess = true;
        buildTelemetry.BuildTarget = "clean";
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
        properties["ProjectPath"].ShouldBe(@"C:\\dev\\theProject");
        properties["ServerFallbackReason"].ShouldBe("busy");
        properties["BuildSuccess"].ShouldBe("True");
        properties["BuildTarget"].ShouldBe("clean");
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
        buildTelemetry.FailureCategory = "Compiler";
        buildTelemetry.CompilerErrorCount = 5;
        buildTelemetry.MSBuildEngineErrorCount = 2;
        buildTelemetry.TaskErrorCount = 1;
        // Don't set SDKErrorCount and BuildCheckErrorCount (leave them null)
        buildTelemetry.NuGetErrorCount = 3;
        buildTelemetry.OtherErrorCount = 1;
        buildTelemetry.FirstErrorCode = "CS0103";

        var properties = buildTelemetry.GetProperties();

        properties["BuildSuccess"].ShouldBe("False");
        properties["FailureCategory"].ShouldBe("Compiler");
        properties["CompilerErrorCount"].ShouldBe("5");
        properties["MSBuildEngineErrorCount"].ShouldBe("2");
        properties["TaskErrorCount"].ShouldBe("1");
        properties["NuGetErrorCount"].ShouldBe("3");
        properties["OtherErrorCount"].ShouldBe("1");
        properties["FirstErrorCode"].ShouldBe("CS0103");

        // Should not include null counts
        properties.ContainsKey("SDKErrorCount").ShouldBeFalse();
        properties.ContainsKey("BuildCheckErrorCount").ShouldBeFalse();
    }

    [Fact]
    public void BuildTelemetryActivityPropertiesIncludesFailureData()
    {
        BuildTelemetry buildTelemetry = new BuildTelemetry();

        buildTelemetry.BuildSuccess = false;
        buildTelemetry.FailureCategory = "Tasks";
        buildTelemetry.TaskErrorCount = 10;
        buildTelemetry.FirstErrorCode = "MSB3075";

        var activityProperties = buildTelemetry.GetActivityProperties();

        activityProperties["BuildSuccess"].ShouldBe(false);
        activityProperties["FailureCategory"].ShouldBe("Tasks");
        activityProperties["TaskErrorCount"].ShouldBe(10);
        activityProperties["FirstErrorCode"].ShouldBe("MSB3075");
    }
}
