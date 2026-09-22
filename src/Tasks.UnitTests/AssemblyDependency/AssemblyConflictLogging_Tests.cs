// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Linq;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;

using Shouldly;

using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// Tests RAR's structured assembly-conflict events behind <see cref="ChangeWaves.Wave18_12"/>.
    /// The events replace large preformatted dependency-list strings.
    /// The events include the MSB3277 warning and the low-importance dependency details.
    /// </summary>
    public sealed class AssemblyConflictLogging_Tests : ResolveAssemblyReferenceTestFixture
    {
        public AssemblyConflictLogging_Tests(ITestOutputHelper output)
            : base(output)
        {
        }

        /// <summary>
        /// Reproduces the D1 primary-reference conflict with the D2 dependency of B.
        /// A also depends on D1, so the victor is both primary and a dependency.
        /// The legacy <c>ConflictGeneratesMessageReferencingAssemblyName</c> test uses this scenario.
        /// This conflict produces the aggregated MSB3277 warning.
        /// </summary>
        private ResolveAssemblyReference CreateWarningConflictTask(MockEngine engine) => new()
        {
            BuildEngine = engine,
            Assemblies = new ITaskItem[]
            {
                new TaskItem("A"),
                new TaskItem("B"),
                new TaskItem("D, Version=1.0.0.0, Culture=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa"),
            },
            SearchPaths = new string[]
            {
                s_myLibrariesRootPath, s_myLibraries_V2Path, s_myLibraries_V1Path,
            },
            TargetFrameworkDirectories = new string[] { s_myVersion20Path },
        };

        /// <summary>
        /// Reproduces the unresolved primary-reference conflict from the legacy regression test.
        /// This conflict produces a standalone dependency-details message without an MSB3277 warning.
        /// </summary>
        private ResolveAssemblyReference CreateMessageConflictTask(MockEngine engine, bool useEscapedPrimaryItem = false) => new()
        {
            BuildEngine = engine,
            Assemblies = new ITaskItem[]
            {
                new TaskItem(useEscapedPrimaryItem
                    ? "A%2C Version=20.0.0.0%2C Culture=Neutral%2C PublicKeyToken=null"
                    : "A, Version=20.0.0.0, Culture=Neutral, PublicKeyToken=null"),
                new TaskItem("B, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null"),
                new TaskItem("D, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null"),
            },
            SearchPaths = new string[]
            {
                s_regress444809RootPath, s_regress444809_V2RootPath,
            },
            TargetFrameworkDirectories = new string[] { s_myVersion20Path },
        };

        private MockEngine RunWithWaveState(bool waveEnabled, Func<MockEngine, ResolveAssemblyReference> createTask)
        {
            MockEngine engine = new(_output);
            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", waveEnabled ? null : ChangeWaves.Wave18_12.ToString());
            ChangeWaves.ResetStateForTests();

            Execute(createTask(engine)).ShouldBeTrue();

            ChangeWaves.ResetStateForTests();
            return engine;
        }

        [Fact]
        public void ConflictWarningIsStructuredAndInvariant()
        {
            MockEngine structuredEngine = RunWithWaveState(waveEnabled: true, CreateWarningConflictTask);
            MockEngine legacyEngine = RunWithWaveState(waveEnabled: false, CreateWarningConflictTask);

            structuredEngine.Warnings.ShouldBe(1);
            legacyEngine.Warnings.ShouldBe(1);

            AssemblyConflictWarningEventArgs structuredWarning = structuredEngine.WarningEvents[0].ShouldBeOfType<AssemblyConflictWarningEventArgs>();
            string expectedBody = AssemblyConflictMessageFormatter.FormatWarningBody(
                structuredWarning.LossReason,
                structuredWarning.Victor,
                structuredWarning.Victim);
            structuredWarning.Message.ShouldBe(
                AssemblyConflictMessageFormatter.FormatWarningMessage(structuredWarning.SimpleAssemblyName, expectedBody));
            structuredWarning.Code.ShouldBe(legacyEngine.WarningEvents[0].Code);
            structuredWarning.Code.ShouldBe("MSB3277");
            structuredWarning.HelpKeyword.ShouldBe("MSBuild.ResolveAssemblyReference.FoundConflicts");
            structuredWarning.HelpKeyword.ShouldBe(legacyEngine.WarningEvents[0].HelpKeyword);
            structuredWarning.SimpleAssemblyName.ShouldBe("D");
            structuredWarning.Victor.FusionName.ShouldContain("D, Version=1.0.0.0");
            structuredWarning.Victim.FusionName.ShouldContain("D, Version=2.0.0.0");
            structuredWarning.Victor.PrimarySourceItemSpecs.ShouldNotBeEmpty();
            structuredWarning.Victor.Dependees.ShouldNotBeEmpty();
            structuredWarning.Victim.Dependees.ShouldNotBeEmpty();

            // Verify that the legacy path does not log the structured event type.
            legacyEngine.WarningEvents[0].ShouldNotBeOfType<AssemblyConflictWarningEventArgs>();
        }

        [Fact]
        public void ConflictWarningPromotedToErrorMatchesWarningText()
        {
            MockEngine warningEngine = RunWithWaveState(waveEnabled: true, CreateWarningConflictTask);
            MockEngine errorEngine = new(_output);
            errorEngine.WarningsAsErrors.Add("MSB3277");
            ResolveAssemblyReference task = CreateWarningConflictTask(errorEngine);

            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
            ChangeWaves.ResetStateForTests();

            Execute(task, RARSimulationMode.BuildProject).ShouldBeFalse();

            ChangeWaves.ResetStateForTests();
            errorEngine.Warnings.ShouldBe(0);
            errorEngine.Errors.ShouldBe(1);
            BuildErrorEventArgs error = errorEngine.ErrorEvents.ShouldHaveSingleItem();
            error.Code.ShouldBe("MSB3277");
            AssemblyConflictWarningEventArgs warning = warningEngine.WarningEvents[0].ShouldBeOfType<AssemblyConflictWarningEventArgs>();
            CultureInfo culture = CultureInfo.CurrentUICulture;
            string expectedBody = AssemblyConflictMessageFormatter.FormatWarningBody(
                warning.LossReason,
                warning.Victor,
                warning.Victim,
                culture);
            error.Message.ShouldBe(AssemblyConflictMessageFormatter.FormatWarningMessage(warning.SimpleAssemblyName, expectedBody, culture));
        }

        [Fact]
        public void StructuredConflictLocalizesDisplayStringsButKeepsPayloadsInvariant()
        {
            CultureInfo originalUICulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");

                MockEngine messageEngine = new(_output);
                ResolveAssemblyReference messageTask = CreateMessageConflictTask(messageEngine);
                messageTask.OutputUnresolvedAssemblyConflicts = true;

                using TestEnvironment env = TestEnvironment.Create(_output);
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
                ChangeWaves.ResetStateForTests();

                Execute(messageTask).ShouldBeTrue();

                messageEngine.MessageEvents.Any(message =>
                    message is not AssemblyConflictDependencyDetailsMessageEventArgs
                    && message.Message is not null
                    && message.Message.StartsWith("Conflit existant entre", StringComparison.Ordinal)).ShouldBeTrue();
                AssemblyConflictDependencyDetailsMessageEventArgs details = messageEngine.MessageEvents
                    .OfType<AssemblyConflictDependencyDetailsMessageEventArgs>()
                    .ShouldHaveSingleItem();
                details.Message.ShouldStartWith("    References which depend on");
                messageTask.UnresolvedAssemblyConflicts.ShouldHaveSingleItem()
                    .GetMetadata("logMessage").ShouldStartWith("There was a conflict between");

                MockEngine errorEngine = new(_output);
                errorEngine.WarningsAsErrors.Add("MSB3277");
                Execute(CreateWarningConflictTask(errorEngine), RARSimulationMode.BuildProject).ShouldBeFalse();
                errorEngine.ErrorEvents.ShouldHaveSingleItem().Message
                    .ShouldStartWith("détection de conflits non résolus");
            }
            finally
            {
                CultureInfo.CurrentUICulture = originalUICulture;
                ChangeWaves.ResetStateForTests();
            }
        }

        [Fact]
        public void ConflictDependencyDetailsMessageIsStructuredAndInvariant()
        {
            MockEngine structuredEngine = RunWithWaveState(waveEnabled: true, engine => CreateMessageConflictTask(engine));
            MockEngine legacyEngine = RunWithWaveState(waveEnabled: false, engine => CreateMessageConflictTask(engine));

            // This conflict does not produce MSB3277 because RAR resolves the conflict.
            // Both runs can contain an unrelated MSB3245 warning for the unresolved primary reference to A.
            structuredEngine.WarningEvents.ShouldNotContain(w => w.Code == "MSB3277");
            legacyEngine.WarningEvents.ShouldNotContain(w => w.Code == "MSB3277");

            AssemblyConflictDependencyDetailsMessageEventArgs detailsEvent = structuredEngine.MessageEvents
                .OfType<AssemblyConflictDependencyDetailsMessageEventArgs>()
                .ShouldHaveSingleItem();
            detailsEvent.Importance.ShouldBe(MessageImportance.Low);
            detailsEvent.Victor.IsPrimary.ShouldBeTrue();
            detailsEvent.Victor.IsResolved.ShouldBeFalse();
            detailsEvent.Victor.UnresolvedPrimaryItemSpec.ShouldNotBeNullOrEmpty();
            detailsEvent.Victor.PrimarySourceItemSpecs.ShouldBeEmpty();
            detailsEvent.Victor.Dependees.ShouldNotBeEmpty();
            detailsEvent.Victim.Dependees.ShouldNotBeEmpty();

            detailsEvent.Message.ShouldBe(
                AssemblyConflictMessageFormatter.FormatDependencyDetails(detailsEvent.Victor, detailsEvent.Victim));

            legacyEngine.MessageEvents.ShouldNotContain(m => m is AssemblyConflictDependencyDetailsMessageEventArgs);
        }

        [Fact]
        public void OutputUnresolvedAssemblyConflictsMatchesLegacyMetadata()
        {
            MockEngine structuredEngine = new(_output);
            ResolveAssemblyReference structuredTask = CreateWarningConflictTask(structuredEngine);
            structuredTask.OutputUnresolvedAssemblyConflicts = true;

            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
                ChangeWaves.ResetStateForTests();
                Execute(structuredTask).ShouldBeTrue();
                ChangeWaves.ResetStateForTests();
            }

            MockEngine legacyEngine = new(_output);
            ResolveAssemblyReference legacyTask = CreateWarningConflictTask(legacyEngine);
            legacyTask.OutputUnresolvedAssemblyConflicts = true;

            using (TestEnvironment env = TestEnvironment.Create(_output))
            {
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_12.ToString());
                ChangeWaves.ResetStateForTests();
                Execute(legacyTask).ShouldBeTrue();
                ChangeWaves.ResetStateForTests();
            }

            structuredTask.UnresolvedAssemblyConflicts.Length.ShouldBe(1);
            legacyTask.UnresolvedAssemblyConflicts.Length.ShouldBe(1);

            ITaskItem structuredConflict = structuredTask.UnresolvedAssemblyConflicts[0];
            ITaskItem legacyConflict = legacyTask.UnresolvedAssemblyConflicts[0];
            AssemblyConflictWarningEventArgs structuredWarning = structuredEngine.WarningEvents[0].ShouldBeOfType<AssemblyConflictWarningEventArgs>();

            structuredConflict.ItemSpec.ShouldBe(legacyConflict.ItemSpec);
            structuredConflict.GetMetadata("logMessage").ShouldBe(
                AssemblyConflictMessageFormatter.FormatWarningBody(
                    structuredWarning.LossReason,
                    structuredWarning.Victor,
                    structuredWarning.Victim));
            structuredConflict.GetMetadata("logMessageDetails").ShouldBeEmpty();
            structuredConflict.GetMetadata("victorVersionNumber").ShouldBe(legacyConflict.GetMetadata("victorVersionNumber"));
            structuredConflict.GetMetadata("victimVersionNumber").ShouldBe(legacyConflict.GetMetadata("victimVersionNumber"));
        }

        [Fact]
        public void ConflictDependencyDetailsMessageAvailableWhenOutputDisabled()
        {
            MockEngine engine = new(_output);
            ResolveAssemblyReference task = CreateMessageConflictTask(engine);

            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
            ChangeWaves.ResetStateForTests();
            Execute(task).ShouldBeTrue();
            ChangeWaves.ResetStateForTests();

            AssemblyConflictDependencyDetailsMessageEventArgs detailsEvent = engine.MessageEvents
                .OfType<AssemblyConflictDependencyDetailsMessageEventArgs>()
                .ShouldHaveSingleItem();

            detailsEvent.Message.ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public void ConflictDependencyDetailsMessageRespectsImportanceFilter()
        {
            MockEngine engine = new(_output)
            {
                MinimumMessageImportance = MessageImportance.Normal,
            };
            ResolveAssemblyReference task = CreateMessageConflictTask(engine);

            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
            ChangeWaves.ResetStateForTests();
            Execute(task).ShouldBeTrue();
            ChangeWaves.ResetStateForTests();

            engine.MessageEvents.ShouldNotContain(message => message is AssemblyConflictDependencyDetailsMessageEventArgs);
        }

        [Fact]
        public void FilteredConflictDependencyDetailsStillPopulateOutputMetadata()
        {
            MockEngine engine = new(_output)
            {
                MinimumMessageImportance = MessageImportance.Normal,
            };
            ResolveAssemblyReference task = CreateMessageConflictTask(engine);
            task.OutputUnresolvedAssemblyConflicts = true;

            using TestEnvironment env = TestEnvironment.Create(_output);
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
            ChangeWaves.ResetStateForTests();
            Execute(task).ShouldBeTrue();
            ChangeWaves.ResetStateForTests();

            engine.MessageEvents.ShouldNotContain(message => message is AssemblyConflictDependencyDetailsMessageEventArgs);
            task.UnresolvedAssemblyConflicts.ShouldHaveSingleItem()
                .GetMetadata("logMessageDetails").ShouldNotBeNullOrEmpty();
        }

        [Fact]
        public void EscapedUnresolvedPrimaryItemTextMatchesLegacy()
        {
            MockEngine structuredEngine = RunWithWaveState(
                waveEnabled: true,
                engine => CreateMessageConflictTask(engine, useEscapedPrimaryItem: true));
            MockEngine legacyEngine = RunWithWaveState(
                waveEnabled: false,
                engine => CreateMessageConflictTask(engine, useEscapedPrimaryItem: true));

            AssemblyConflictDependencyDetailsMessageEventArgs structuredDetails = structuredEngine.MessageEvents
                .OfType<AssemblyConflictDependencyDetailsMessageEventArgs>()
                .ShouldHaveSingleItem();
            BuildMessageEventArgs legacyDetails = legacyEngine.MessageEvents
                .Where(message => message.Importance == MessageImportance.Low
                    && message.Message != null
                    && message.Message.Contains("A%2C Version=20.0.0.0"))
                .ShouldHaveSingleItem();

            structuredDetails.Message.ShouldBe(
                AssemblyConflictMessageFormatter.FormatDependencyDetails(structuredDetails.Victor, structuredDetails.Victim));
            legacyDetails.Message.ShouldContain("A%2C Version=20.0.0.0");
            structuredDetails.Message.ShouldContain("A%2C Version=20.0.0.0");
        }
    }
}
