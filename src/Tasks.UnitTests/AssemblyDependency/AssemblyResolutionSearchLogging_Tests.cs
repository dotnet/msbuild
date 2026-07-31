// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;

using Shouldly;

using Xunit;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests;

public sealed class AssemblyResolutionSearchLogging_Tests
{
    [Fact]
    public void AggregatesSearchAttemptsPerReference()
    {
        using TestEnvironment env = TestEnvironment.Create();
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
        ChangeWaves.ResetStateForTests();

        MockEngine engine = LogSearchAttempts();

        AssemblyResolutionSearchTraceEventArgs searchEvent = engine.MessageEvents
            .OfType<AssemblyResolutionSearchTraceEventArgs>()
            .ShouldHaveSingleItem();
        searchEvent.RequestedAssemblyName.ShouldBe("Requested, Version=1.0.0.0");
        searchEvent.SearchAttempts.Count.ShouldBe(3);
        searchEvent.SearchAttempts[0].Result.ShouldBe(AssemblyResolutionSearchResult.FileNotFound);
        searchEvent.SearchAttempts[1].Result.ShouldBe(AssemblyResolutionSearchResult.FusionNamesDidNotMatch);
        searchEvent.SearchAttempts[2].IsAssemblyFoldersExSearch.ShouldBeTrue();
        searchEvent.MessageFormats.ShouldNotBeNull().SearchPath.ShouldBe(
            "        " + AssemblyResources.PrimaryResources.GetString(
                "ResolveAssemblyReference.SearchPath",
                CultureInfo.InvariantCulture));
        searchEvent.Message.ShouldNotBeNull().ShouldContain("first.dll");
        searchEvent.Message.ShouldNotBeNull().ShouldContain("Candidate, Version=2.0.0.0");
        searchEvent.Message.ShouldNotBeNull().ShouldNotContain("assembly-folder-candidate.dll");
        engine.Messages.ShouldBe(1);

        ChangeWaves.ResetStateForTests();
    }

    [Fact]
    public void ChangeWaveOptOutPreservesIndividualMessages()
    {
        using TestEnvironment env = TestEnvironment.Create();
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_11.ToString());
        ChangeWaves.ResetStateForTests();

        MockEngine engine = LogSearchAttempts();

        engine.MessageEvents.ShouldNotContain(message => message is AssemblyResolutionSearchTraceEventArgs);
        engine.Messages.ShouldBe(6);
        engine.Log.ShouldContain("first.dll");
        engine.Log.ShouldContain("Candidate, Version=2.0.0.0");
        engine.Log.ShouldNotContain("assembly-folder-candidate.dll");

        ChangeWaves.ResetStateForTests();
    }

    [Fact]
    public void AggregatedMessagePreservesLegacyText()
    {
        using TestEnvironment env = TestEnvironment.Create();

        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
        ChangeWaves.ResetStateForTests();
        string aggregatedLog = LogSearchAttempts().Log;

        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_11.ToString());
        ChangeWaves.ResetStateForTests();
        string legacyLog = LogSearchAttempts().Log;

        aggregatedLog.ShouldBe(legacyLog);
        ChangeWaves.ResetStateForTests();
    }

    [Fact]
    public void DoesNotLogEventWhenNoCandidatesWereRejected()
    {
        using TestEnvironment env = TestEnvironment.Create();
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
        ChangeWaves.ResetStateForTests();

        var engine = new MockEngine();
        var rar = new ResolveAssemblyReference
        {
            BuildEngine = engine,
        };
        Reference reference = CreateReference();

        rar.LogAssembliesConsideredAndRejected(reference, "Requested, Version=1.0.0.0", MessageImportance.Low);

        engine.Messages.ShouldBe(0);
        ChangeWaves.ResetStateForTests();
    }

    private static MockEngine LogSearchAttempts()
    {
        var engine = new MockEngine();
        var rar = new ResolveAssemblyReference
        {
            BuildEngine = engine,
        };
        Reference reference = CreateReference();
        reference.AddAssembliesConsideredAndRejected(
        [
            new ResolutionSearchLocation
            {
                FileNameAttempted = "first.dll",
                SearchPath = "first-path",
                Reason = NoMatchReason.FileNotFound,
            },
            new ResolutionSearchLocation
            {
                FileNameAttempted = "second.dll",
                SearchPath = "second-path",
                ParentAssembly = "parent.dll",
                AssemblyName = new AssemblyNameExtension("Candidate, Version=2.0.0.0"),
                Reason = NoMatchReason.FusionNamesDidNotMatch,
            },
            new ResolutionSearchLocation
            {
                FileNameAttempted = "assembly-folder-candidate.dll",
                SearchPath = AssemblyResolutionConstants.assemblyFoldersExSentinel + "test",
                Reason = NoMatchReason.FileNotFound,
            },
        ]);

        rar.LogAssembliesConsideredAndRejected(
            reference,
            "Requested, Version=1.0.0.0",
            MessageImportance.Low);
        return engine;
    }

    private static Reference CreateReference()
        => new(
            (string _, GetAssemblyRuntimeVersion _, FileExists _, out string imageRuntimeVersion, out bool isManagedWinmd) =>
            {
                imageRuntimeVersion = string.Empty;
                isManagedWinmd = false;
                return false;
            },
            _ => false,
            _ => string.Empty);
}
