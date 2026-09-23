// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;

using Shouldly;

using Xunit;

using FrameworkSR = Microsoft.Build.Framework.Resources.SR;

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
    public async Task AggregatedMessageSupportsInvariantAndLocalizedText()
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

        CultureInfo frenchCulture = CultureInfo.GetCultureInfo("fr-FR");
        string invariantMessage = await Task.Run(() =>
        {
            CultureInfo.CurrentCulture = frenchCulture;
            CultureInfo.CurrentUICulture = frenchCulture;
            return searchEvent.Message.ShouldNotBeNull();
        });

        string[] expectedInvariantMessages =
        [
            FormatResource(CultureInfo.InvariantCulture, "AssemblyResolutionSearchTrace_SearchPath", "path"),
            FormatResource(CultureInfo.InvariantCulture, "AssemblyResolutionSearchTrace_ConsideredAndRejectedBecauseNoFile", "missing.dll"),
            FormatResource(CultureInfo.InvariantCulture, "AssemblyResolutionSearchTrace_ConsideredAndRejectedBecauseTargetDidntHaveFusionName", "not-an-assembly.dll"),
            FormatResource(CultureInfo.InvariantCulture, "AssemblyResolutionSearchTrace_ConsideredAndRejectedBecauseNotInGac", "not-in-gac.dll"),
            FormatResource(CultureInfo.InvariantCulture, "AssemblyResolutionSearchTrace_ConsideredAndRejectedBecauseNotAFileNameOnDisk", "not-a-file.dll"),
            FormatResource(CultureInfo.InvariantCulture, "AssemblyResolutionSearchTrace_TargetedProcessorArchitectureDoesNotMatch", "wrong-architecture.dll", "AMD64", targetProcessorArchitecture),
            FormatResource(CultureInfo.InvariantCulture, "AssemblyResolutionSearchTrace_SearchPathAddedByParentAssembly", "parent-path", "parent.dll"),
            FormatResource(CultureInfo.InvariantCulture, "AssemblyResolutionSearchTrace_ConsideredAndRejectedBecauseFusionNamesDidntMatch", "wrong-name.dll", "Candidate, Version=2.0.0.0", requestedAssemblyName),
            FormatResource(CultureInfo.InvariantCulture, "AssemblyResolutionSearchTrace_SearchPath", AssemblyResolutionConstants.assemblyFoldersExSentinel + "test"),
            FormatResource(CultureInfo.InvariantCulture, "AssemblyResolutionSearchTrace_SearchedAssemblyFoldersEx"),
        ];

        string[] expectedLocalizedMessages =
        [
            FormatResource(frenchCulture, "AssemblyResolutionSearchTrace_SearchPath", "path"),
            FormatResource(frenchCulture, "AssemblyResolutionSearchTrace_ConsideredAndRejectedBecauseNoFile", "missing.dll"),
            FormatResource(frenchCulture, "AssemblyResolutionSearchTrace_ConsideredAndRejectedBecauseTargetDidntHaveFusionName", "not-an-assembly.dll"),
            FormatResource(frenchCulture, "AssemblyResolutionSearchTrace_ConsideredAndRejectedBecauseNotInGac", "not-in-gac.dll"),
            FormatResource(frenchCulture, "AssemblyResolutionSearchTrace_ConsideredAndRejectedBecauseNotAFileNameOnDisk", "not-a-file.dll"),
            FormatResource(frenchCulture, "AssemblyResolutionSearchTrace_TargetedProcessorArchitectureDoesNotMatch", "wrong-architecture.dll", "AMD64", targetProcessorArchitecture),
            FormatResource(frenchCulture, "AssemblyResolutionSearchTrace_SearchPathAddedByParentAssembly", "parent-path", "parent.dll"),
            FormatResource(frenchCulture, "AssemblyResolutionSearchTrace_ConsideredAndRejectedBecauseFusionNamesDidntMatch", "wrong-name.dll", "Candidate, Version=2.0.0.0", requestedAssemblyName),
            FormatResource(frenchCulture, "AssemblyResolutionSearchTrace_SearchPath", AssemblyResolutionConstants.assemblyFoldersExSentinel + "test"),
            FormatResource(frenchCulture, "AssemblyResolutionSearchTrace_SearchedAssemblyFoldersEx"),
        ];

        invariantMessage.ShouldBe(string.Join(System.Environment.NewLine, expectedInvariantMessages));
        string localizedMessage = searchEvent.FormatMessage(frenchCulture);
        localizedMessage.ShouldBe(string.Join(System.Environment.NewLine, expectedLocalizedMessages));
        searchEvent.FormatMessage(frenchCulture).ShouldBeSameAs(localizedMessage);
        searchEvent.Message.ShouldBe(invariantMessage);
        Should.Throw<ArgumentNullException>(() => searchEvent.FormatMessage(null!));
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

    private static string FormatResource(CultureInfo culture, string resourceName, params object[] arguments)
        => string.Format(
            culture,
            "        " + FrameworkSR.ResourceManager.GetString(resourceName, culture).ShouldNotBeNull(),
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
