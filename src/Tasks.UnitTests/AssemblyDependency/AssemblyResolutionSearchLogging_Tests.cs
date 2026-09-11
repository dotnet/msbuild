// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

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
        searchEvent.SearchAttempts[0].Result.ShouldBe(AssemblyResolutionSearchResult.NotInGac);
        searchEvent.SearchAttempts[0].AssemblyName.ShouldBeNull();
        searchEvent.SearchAttempts[1].Result.ShouldBe(AssemblyResolutionSearchResult.FusionNamesDidNotMatch);
        searchEvent.SearchAttempts[1].AssemblyName.ShouldBe("Candidate, Version=2.0.0.0");
        searchEvent.SearchAttempts[2].IsAssemblyFoldersExSearch.ShouldBeTrue();
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
        env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_12.ToString());
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
    public async Task AggregatedMessageUsesInvariantCultureAndPreservesLegacyInvariantText()
    {
        const string requestedAssemblyName = "Requested, Version=1.0.0.0";
        const string targetProcessorArchitecture = "MSIL";
        var searchEvent = new AssemblyResolutionSearchTraceEventArgs(
            requestedAssemblyName,
            targetProcessorArchitecture,
            [
                new("missing.dll", "path", null, null, AssemblyResolutionSearchResult.FileNotFound, null, false),
                new("not-an-assembly.dll", "path", null, null, AssemblyResolutionSearchResult.TargetHadNoFusionName, null, false),
                new("not-in-gac.dll", "path", null, null, AssemblyResolutionSearchResult.NotInGac, null, false),
                new("not-a-file.dll", "path", null, null, AssemblyResolutionSearchResult.NotAFileNameOnDisk, null, false),
                new("wrong-architecture.dll", "path", null, null, AssemblyResolutionSearchResult.ProcessorArchitectureDoesNotMatch, "AMD64", false),
                new("wrong-name.dll", "parent-path", "parent.dll", "Candidate, Version=2.0.0.0", AssemblyResolutionSearchResult.FusionNamesDidNotMatch, null, false),
                new("assembly-folder-candidate.dll", AssemblyResolutionConstants.assemblyFoldersExSentinel + "test", null, null, AssemblyResolutionSearchResult.FileNotFound, null, true),
            ],
            nameof(ResolveAssemblyReference),
            MessageImportance.Low,
            eventTimestamp: default);

        string actualMessage = await Task.Run(() =>
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            return searchEvent.Message.ShouldNotBeNull();
        });

        string[] expectedMessages =
        [
            FormatInvariantResource("ResolveAssemblyReference.SearchPath", "path"),
            FormatInvariantResource("ResolveAssemblyReference.ConsideredAndRejectedBecauseNoFile", "missing.dll"),
            FormatInvariantResource("ResolveAssemblyReference.ConsideredAndRejectedBecauseTargetDidntHaveFusionName", "not-an-assembly.dll"),
            FormatInvariantResource("ResolveAssemblyReference.ConsideredAndRejectedBecauseNotInGac", "not-in-gac.dll"),
            FormatInvariantResource("ResolveAssemblyReference.ConsideredAndRejectedBecauseNotAFileNameOnDisk", "not-a-file.dll"),
            FormatInvariantResource("ResolveAssemblyReference.TargetedProcessorArchitectureDoesNotMatch", "wrong-architecture.dll", "AMD64", targetProcessorArchitecture),
            FormatInvariantResource("ResolveAssemblyReference.SearchPathAddedByParentAssembly", "parent-path", "parent.dll"),
            FormatInvariantResource("ResolveAssemblyReference.ConsideredAndRejectedBecauseFusionNamesDidntMatch", "wrong-name.dll", "Candidate, Version=2.0.0.0", requestedAssemblyName),
            FormatInvariantResource("ResolveAssemblyReference.SearchPath", AssemblyResolutionConstants.assemblyFoldersExSentinel + "test"),
            FormatInvariantResource("ResolveAssemblyReference.SearchedAssemblyFoldersEx"),
        ];

        actualMessage.ShouldBe(string.Join(System.Environment.NewLine, expectedMessages));
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
                AssemblyName = new AssemblyNameExtension("Ignored, Version=3.0.0.0"),
                Reason = NoMatchReason.NotInGac,
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

    private static string FormatInvariantResource(string resourceName, params object[] arguments)
        => string.Format(
            CultureInfo.InvariantCulture,
            "        " + AssemblyResources.GetString(resourceName, CultureInfo.InvariantCulture),
            arguments);

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
