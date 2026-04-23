// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Tasks.AssemblyDependency;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

// These tests demonstrate concrete behavioral breakages identified in the council
// review of PR #13319 ("Enlighten RAR task"). Each [Fact] targets one finding and
// is intended to FAIL on the current head of the PR branch (627af67).
//
// Findings covered (see files/council_review.md for full context):
//   * NEW-3  - _appConfigValueIsEmptyString silently dropped over the OOP wire,
//              causing MP/OOP behavioural divergence with Wave18_6 OFF.
//   * NEW-4  - StateFile / AppConfigFile relative paths are no longer absolutized
//              at the OOP wire boundary; they resolve against node-process CWD.
//   * NEW-8  - RawFilenameResolver throws ArgumentException on an empty ItemSpec
//              (sibling of N1; only `!= null` guard, no IsNullOrEmpty).
//   * NEW-10 - MultiThreadedTaskEnvironmentDriver accepts unvalidated wire
//              ProjectDirectory: null throws unhelpfully, relative paths are
//              accepted silently and pollute the thread-static.
//   * C5/N3  - AppConfigFile setter leaves stale absolute path when re-assigned
//              to "" with Wave18_6 OFF, while flipping the empty-string flag.

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// Targeted regression tests demonstrating breakages introduced by PR #13319.
    /// Each test documents the council finding it exercises and should FAIL on the
    /// PR head; if a fix is applied, the test should pass without modification.
    /// </summary>
    public sealed class PRBreakage_Tests
    {
        // ---------------------------------------------------------------
        // NEW-3 — _appConfigValueIsEmptyString lost across the OOP wire.
        // Setting AppConfigFile = "" with Wave18_6 OFF flips a private bool on
        // the client RAR; this bool is NOT a public property so it is not
        // serialized by RarTaskParameters.Get. On the OOP node, the legacy
        // error path (RAR.cs:2506+) therefore never fires. Same input → MP
        // raises an error, OOP succeeds silently. Behavioural divergence.
        // ---------------------------------------------------------------
        [Fact]
        public void NEW3_AppConfigEmptyStringFlag_NotPropagatedAcrossWire()
        {
            try
            {
                using TestEnvironment env = TestEnvironment.Create();
                ChangeWaves.ResetStateForTests();
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_6.ToString());
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

                ResolveAssemblyReference clientRar = new()
                {
                    BuildEngine = new MockEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                    AppConfigFile = string.Empty,
                };

                // Sanity: the private flag IS set on the client.
                bool clientFlag = GetPrivateBool(clientRar, "_appConfigValueIsEmptyString");
                clientFlag.ShouldBeTrue("client RAR must record the empty-string AppConfigFile under Wave18_6 OFF");

                // Round-trip through the OOP request envelope (in-memory; no actual pipe).
                RarNodeExecuteRequest request = new(clientRar);
                ResolveAssemblyReference nodeRar = new();
                request.SetTaskInputs(nodeRar, CreateBuildEngine());

                bool nodeFlag = GetPrivateBool(nodeRar, "_appConfigValueIsEmptyString");

                // BREAKAGE: the flag is dropped over the wire. With Wave18_6 OFF the legacy
                // error path on the node will never fire; OOP silently succeeds where MP
                // would have produced an error.
                nodeFlag.ShouldBeTrue(
                    "NEW-3 regression: _appConfigValueIsEmptyString must propagate to OOP node " +
                    "(it is not a public property and so is dropped by RarTaskParameters.Get).");
            }
            finally
            {
                ChangeWaves.ResetStateForTests();
            }
        }

        // ---------------------------------------------------------------
        // NEW-4 — Relative StateFile is no longer absolutized at the wire
        // boundary. Pre-PR, RarNodeExecuteRequest explicitly Path.GetFullPath'd
        // these paths against client CWD. That code is gone. With Wave18_6 OFF
        // the setter does not absolutize either, so the node receives a bare
        // relative string and resolves it against its own process CWD.
        // ---------------------------------------------------------------
        [Fact]
        public void NEW4_RelativeStateFile_NotAbsolutizedAcrossWire_WaveOff()
        {
            try
            {
                using TestEnvironment env = TestEnvironment.Create();
                ChangeWaves.ResetStateForTests();
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_6.ToString());
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

                const string RelativeStateFile = "obj" + "/" + "rar.cache";

                ResolveAssemblyReference clientRar = new()
                {
                    BuildEngine = new MockEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                    StateFile = RelativeStateFile,
                };

                RarNodeExecuteRequest request = new(clientRar);
                ResolveAssemblyReference nodeRar = new();
                request.SetTaskInputs(nodeRar, CreateBuildEngine());

                // BREAKAGE: the relative path is preserved verbatim. On a real OOP node
                // (different CWD than the client), the cache will be read/written from
                // the wrong directory. The test asserts the property on the node side is
                // not rooted to make the regression observable.
                Path.IsPathRooted(nodeRar.StateFile).ShouldBeTrue(
                    $"NEW-4 regression: StateFile arrived on node as '{nodeRar.StateFile}', " +
                    "still relative. Pre-PR the wire boundary absolutized it against client CWD.");
            }
            finally
            {
                ChangeWaves.ResetStateForTests();
            }
        }

        [Fact]
        public void NEW4_RelativeAppConfigFile_NotAbsolutizedAcrossWire_WaveOff()
        {
            try
            {
                using TestEnvironment env = TestEnvironment.Create();
                ChangeWaves.ResetStateForTests();
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_6.ToString());
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

                const string RelativeAppConfig = "app.config";

                ResolveAssemblyReference clientRar = new()
                {
                    BuildEngine = new MockEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                    AppConfigFile = RelativeAppConfig,
                };

                RarNodeExecuteRequest request = new(clientRar);
                ResolveAssemblyReference nodeRar = new();
                request.SetTaskInputs(nodeRar, CreateBuildEngine());

                Path.IsPathRooted(nodeRar.AppConfigFile).ShouldBeTrue(
                    $"NEW-4 regression: AppConfigFile arrived on node as '{nodeRar.AppConfigFile}', " +
                    "still relative. Will resolve against node-process CWD, not project directory.");
            }
            finally
            {
                ChangeWaves.ResetStateForTests();
            }
        }

        // ---------------------------------------------------------------
        // NEW-8 — RawFilenameResolver only guards `rawFileNameCandidate != null`,
        // not IsNullOrEmpty. An empty ItemSpec reaches taskEnvironment.GetAbsolutePath("")
        // which constructs `new AbsolutePath("", basePath)`; AbsolutePath's ctor calls
        // `ArgumentException.ThrowIfNullOrEmpty(path)` and throws.
        //
        // Pre-PR this codepath silently no-op'd (returned false). Now an empty primary
        // reference ItemSpec aborts the resolver chain.
        // ---------------------------------------------------------------
        [Fact]
        public void NEW8_RawFilenameResolver_EmptyItemSpec_ThrowsArgumentException()
        {
            TaskEnvironment env = TaskEnvironmentHelper.CreateForTest();
            RawFilenameResolver resolver = new(
                searchPathElement: "{RawFileName}",
                getAssemblyName: _ => null,
                fileExists: _ => false,
                getRuntimeVersion: _ => "v4.0.30319",
                targetedRuntimeVesion: new Version(4, 0),
                taskEnvironment: env);

            // BREAKAGE: empty rawFileNameCandidate now throws instead of skipping.
            Should.NotThrow(() => resolver.Resolve(
                assemblyName: new AssemblyNameExtension("System"),
                sdkName: null,
                rawFileNameCandidate: string.Empty,
                isPrimaryProjectReference: true,
                isImmutableFrameworkReference: false,
                wantSpecificVersion: false,
                executableExtensions: new[] { ".dll" },
                hintPath: null,
                assemblyFolderKey: null,
                assembliesConsideredAndRejected: new List<ResolutionSearchLocation>(),
                foundPath: out _,
                userRequestedSpecificFile: out _),
                "NEW-8 regression: RawFilenameResolver should treat an empty rawFileNameCandidate as 'not provided', " +
                "matching its `!= null` guard intent. Currently it throws ArgumentException via AbsolutePath validation.");
        }

        // ---------------------------------------------------------------
        // NEW-10 — MultiThreadedTaskEnvironmentDriver(ProjectDirectory) does NOT
        // validate the input. The wire field RarNodeExecuteRequest._projectDirectory
        // is a plain string; OutOfProcRarNodeEndpoint blindly forwards it to the
        // ctor (line 103). Two failure modes:
        //   * null  → unhelpful NRE / ArgumentNullException deep in Path.GetFullPath
        //             instead of a precondition error.
        //   * relative → silently accepted (`ignoreRootedCheck:true`), then the
        //             ctor writes that bogus value to the FileUtilities thread-static
        //             which leaks into Expander / %(FullPath) downstream.
        // ---------------------------------------------------------------
        [Fact]
        public void NEW10_MultiThreadedDriver_NullProjectDirectory_NoPreconditionCheck()
        {
            // We accept that this throws SOMETHING; the regression is that it isn't a
            // clean precondition exception with the parameter name. If/when a guard is
            // added (ArgumentNullException with paramName == "currentDirectoryFullPath"),
            // this test should be updated to Should.Throw<ArgumentNullException>().
            Exception? thrown = null;
            try
            {
                using var driver = new MultiThreadedTaskEnvironmentDriver(currentDirectoryFullPath: null!);
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            // BREAKAGE: an ArgumentNullException with paramName is the expected contract
            // for an input that crosses a process boundary unvalidated.
            thrown.ShouldBeOfType<ArgumentNullException>(
                "NEW-10 regression: MultiThreadedTaskEnvironmentDriver must reject null " +
                "ProjectDirectory with a typed precondition exception, not let it leak to GetCanonicalForm.");
            ((ArgumentNullException)thrown!).ParamName.ShouldBe("currentDirectoryFullPath");
        }

        [Fact]
        public void NEW10_MultiThreadedDriver_RelativeProjectDirectory_AcceptedSilently()
        {
            const string RelativeDir = "not-rooted";

            // BREAKAGE: the ctor accepts a relative path and (worse) writes it into the
            // FileUtilities.CurrentThreadWorkingDirectory thread-static, where it
            // pollutes Expander / %(FullPath) for the entire thread.
            Should.Throw<ArgumentException>(
                () =>
                {
                    using var driver = new MultiThreadedTaskEnvironmentDriver(RelativeDir);
                    // The ctor sets CurrentThreadWorkingDirectory unconditionally.
                    // Demonstrate the leak so the assertion fires even if no exception is thrown.
                    if (string.Equals(FileUtilities.CurrentThreadWorkingDirectory, RelativeDir, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Thread-static was polluted with relative path '{RelativeDir}'.");
                    }
                },
                "NEW-10 regression: MultiThreadedTaskEnvironmentDriver must reject relative ProjectDirectory " +
                "before mutating the FileUtilities.CurrentThreadWorkingDirectory thread-static.");

            // Defensive cleanup in case the ctor ran far enough to leak.
            FileUtilities.CurrentThreadWorkingDirectory = null;
        }

        // ---------------------------------------------------------------
        // C5 / N3 — AppConfigFile setter has stale-state on re-assignment.
        // Sequence with Wave18_6 OFF:
        //   AppConfigFile = "/abs/foo.config"  →  _appConfigFile = abs path
        //   AppConfigFile = ""                 →  _appConfigValueIsEmptyString = true,
        //                                        but _appConfigFile is NOT cleared.
        // The instance now claims simultaneously "I have an absolute config" AND
        // "the user explicitly cleared the config", which contradicts the legacy
        // error contract.
        // ---------------------------------------------------------------
        [Fact]
        public void C5_AppConfigFileSetter_ReassignToEmpty_LeavesStaleAbsolutePath_WaveOff()
        {
            try
            {
                using TestEnvironment env = TestEnvironment.Create();
                ChangeWaves.ResetStateForTests();
                env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_6.ToString());
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

                string absolutePath = Path.Combine(Path.GetTempPath(), "stale.config");

                ResolveAssemblyReference rar = new()
                {
                    BuildEngine = new MockEngine(),
                    TaskEnvironment = TaskEnvironmentHelper.CreateForTest(),
                    AppConfigFile = absolutePath,
                };

                // Re-assign to empty (wave OFF). Per the legacy contract this should
                // either clear the prior path or remain a no-op; instead, the setter
                // only flips the private flag.
                rar.AppConfigFile = string.Empty;

                bool emptyFlag = GetPrivateBool(rar, "_appConfigValueIsEmptyString");
                object? appConfigField = GetPrivateField(rar, "_appConfigFile");
                string? appConfigOriginal = appConfigField?.GetType()
                    .GetProperty("OriginalValue")?.GetValue(appConfigField) as string;

                emptyFlag.ShouldBeTrue("flag must reflect the explicit empty assignment");

                // BREAKAGE: stale absolute path persists alongside the empty-flag.
                appConfigOriginal.ShouldBeNull(
                    "C5/N3 regression: AppConfigFile setter must clear the underlying absolute " +
                    $"path when re-assigned to empty; instead it retained '{appConfigOriginal}'.");
            }
            finally
            {
                ChangeWaves.ResetStateForTests();
            }
        }

        // ---------------- helpers ----------------

        private static bool GetPrivateBool(object instance, string fieldName)
        {
            object? value = GetPrivateField(instance, fieldName);
            return value is true;
        }

        private static object? GetPrivateField(object instance, string fieldName)
        {
            System.Reflection.FieldInfo? field = instance.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.ShouldNotBeNull($"private field '{fieldName}' not found on {instance.GetType().Name}");
            return field.GetValue(instance);
        }

        private static RarNodeBuildEngine CreateBuildEngine()
        {
            // Mirrors RarNodeExecuteRequest_Tests: NodePipeServer is needed for ctor
            // wiring but the tests never connect to a client, so dispose immediately.
            OutOfProcRarNodeEndpoint.SharedConfig config =
                OutOfProcRarNodeEndpoint.CreateConfig(maxNumberOfServerInstances: 1);
            using NodePipeServer pipeServer = new(config.PipeName, config.Handshake, config.MaxNumberOfServerInstances);
            return new RarNodeBuildEngine(pipeServer);
        }
    }
}
