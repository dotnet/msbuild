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

        buildTelemetry.DisplayVersion.ShouldBeNull();
        buildTelemetry.EventName.ShouldBe("build");
        buildTelemetry.FinishedAt.ShouldBeNull();
        buildTelemetry.FrameworkName.ShouldBeNull();
        buildTelemetry.Host.ShouldBeNull();
        buildTelemetry.InitialServerState.ShouldBeNull();
        buildTelemetry.InnerStartAt.ShouldBeNull();
        buildTelemetry.Project.ShouldBeNull();
        buildTelemetry.ServerFallbackReason.ShouldBeNull();
        buildTelemetry.StartAt.ShouldBeNull();
        buildTelemetry.Success.ShouldBeNull();
        buildTelemetry.Target.ShouldBeNull();
        buildTelemetry.Version.ShouldBeNull();

        buildTelemetry.UpdateEventProperties();
        buildTelemetry.Properties.ShouldBeEmpty();
    }

    [Fact]
    public void BuildTelemetryCreateProperProperties()
    {
        BuildTelemetry buildTelemetry = new BuildTelemetry();

        DateTime startAt = new DateTime(2023, 01, 02, 10, 11, 22);
        DateTime innerStartAt = new DateTime(2023, 01, 02, 10, 20, 30);
        DateTime finishedAt = new DateTime(2023, 12, 13, 14, 15, 16);

        buildTelemetry.DisplayVersion = "Some Display Version";
        buildTelemetry.FinishedAt = finishedAt;
        buildTelemetry.FrameworkName = "new .NET";
        buildTelemetry.Host = "Host description";
        buildTelemetry.InitialServerState = "hot";
        buildTelemetry.InnerStartAt = innerStartAt;
        buildTelemetry.Project = @"C:\\dev\\theProject";
        buildTelemetry.ServerFallbackReason = "busy";
        buildTelemetry.StartAt = startAt;
        buildTelemetry.Success = true;
        buildTelemetry.Target = "clean";
        buildTelemetry.Version = new Version(1, 2, 3, 4);

        buildTelemetry.UpdateEventProperties();
        buildTelemetry.Properties.Count.ShouldBe(11);

        buildTelemetry.Properties["BuildEngineDisplayVersion"].ShouldBe("Some Display Version");
        buildTelemetry.Properties["BuildEngineFrameworkName"].ShouldBe("new .NET");
        buildTelemetry.Properties["BuildEngineHost"].ShouldBe("Host description");
        buildTelemetry.Properties["InitialMSBuildServerState"].ShouldBe("hot");
        buildTelemetry.Properties["ProjectPath"].ShouldBe(@"C:\\dev\\theProject");
        buildTelemetry.Properties["ServerFallbackReason"].ShouldBe("busy");
        buildTelemetry.Properties["BuildSuccess"].ShouldBe("True");
        buildTelemetry.Properties["BuildTarget"].ShouldBe("clean");
        buildTelemetry.Properties["BuildEngineVersion"].ShouldBe("1.2.3.4");

        // verify computed
        buildTelemetry.Properties["BuildDurationInMilliseconds"] = (finishedAt - startAt).TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
        buildTelemetry.Properties["InnerBuildDurationInMilliseconds"] = (finishedAt - innerStartAt).TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
    }

    [Fact]
    public void BuildTelemetryHandleNullsInRecordedTimes()
    {
        BuildTelemetry buildTelemetry = new BuildTelemetry();

        buildTelemetry.StartAt = DateTime.MinValue;
        buildTelemetry.FinishedAt = null;
        buildTelemetry.UpdateEventProperties();
        buildTelemetry.Properties.ShouldBeEmpty();

        buildTelemetry.StartAt = null;
        buildTelemetry.FinishedAt = DateTime.MaxValue;
        buildTelemetry.UpdateEventProperties();
        buildTelemetry.Properties.ShouldBeEmpty();

        buildTelemetry.InnerStartAt = DateTime.MinValue;
        buildTelemetry.FinishedAt = null;
        buildTelemetry.UpdateEventProperties();
        buildTelemetry.Properties.ShouldBeEmpty();

        buildTelemetry.InnerStartAt = null;
        buildTelemetry.FinishedAt = DateTime.MaxValue;
        buildTelemetry.UpdateEventProperties();
        buildTelemetry.Properties.ShouldBeEmpty();
    }
}
