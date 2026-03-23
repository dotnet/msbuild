// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.CommandLine;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.CommandLine.UnitTests
{
    public sealed class OutOfProcTaskHostNode_Tests
    {
        private readonly ITestOutputHelper _output;

        public OutOfProcTaskHostNode_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ShouldTreatWarningAsError_SpecificCode_ReturnsTrue()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            ChangeWaves.ResetStateForTests();
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", string.Empty);
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            OutOfProcTaskHostNode node = new();
            SetWarningsAsErrors(node, new HashSet<string> { "MY0001" });
            SetWarningsAsMessages(node, null);
            SetWarningsNotAsErrors(node, null);

            node.ShouldTreatWarningAsError("MY0001").ShouldBeTrue();
        }

        [Fact]
        public void ShouldTreatWarningAsError_SpecificCode_OtherCode_ReturnsFalse()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            ChangeWaves.ResetStateForTests();
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", string.Empty);
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            OutOfProcTaskHostNode node = new();
            SetWarningsAsErrors(node, new HashSet<string> { "MY0001" });
            SetWarningsAsMessages(node, null);
            SetWarningsNotAsErrors(node, null);

            node.ShouldTreatWarningAsError("OTHER0001").ShouldBeFalse();
        }

        [Fact]
        public void ShouldTreatWarningAsError_TreatAll_ReturnsTrue()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            ChangeWaves.ResetStateForTests();
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", string.Empty);
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            OutOfProcTaskHostNode node = new();
            SetWarningsAsErrors(node, new HashSet<string>());
            SetWarningsAsMessages(node, null);
            SetWarningsNotAsErrors(node, null);

            node.ShouldTreatWarningAsError("ANY0001").ShouldBeTrue();
        }

        [Fact]
        public void ShouldTreatWarningAsError_TreatAll_OverriddenByNotAsErrors_ReturnsFalse()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            ChangeWaves.ResetStateForTests();
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", string.Empty);
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            OutOfProcTaskHostNode node = new();
            SetWarningsAsErrors(node, new HashSet<string>());
            SetWarningsAsMessages(node, null);
            SetWarningsNotAsErrors(node, new HashSet<string> { "MY0001" });

            node.ShouldTreatWarningAsError("MY0001").ShouldBeFalse();
        }

        [Fact]
        public void ShouldTreatWarningAsError_Null_ReturnsFalse()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            ChangeWaves.ResetStateForTests();
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", string.Empty);
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            OutOfProcTaskHostNode node = new();
            SetWarningsAsErrors(node, null);
            SetWarningsAsMessages(node, null);
            SetWarningsNotAsErrors(node, null);

            node.ShouldTreatWarningAsError("MY0001").ShouldBeFalse();
        }

        [Fact]
        public void ShouldTreatWarningAsError_WarningAsMessage_ReturnsFalse()
        {
            using TestEnvironment env = TestEnvironment.Create(_output);
            ChangeWaves.ResetStateForTests();
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", string.Empty);
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            OutOfProcTaskHostNode node = new();
            SetWarningsAsErrors(node, new HashSet<string> { "MY0001" });
            SetWarningsAsMessages(node, new HashSet<string> { "MY0001" });
            SetWarningsNotAsErrors(node, null);

            // WarningsAsMessages overrides WarningsAsErrors
            node.ShouldTreatWarningAsError("MY0001").ShouldBeFalse();
        }

        [Fact]
        public void ShouldTreatWarningAsError_SpecificCode_NullWarningsAsMessages_ReturnsTrue()
        {
            // This is the exact scenario from the bug report:
            // WarningsAsErrors = {"MY0001"}, WarningsAsMessages = null
            // The old buggy code would NRE on WarningsAsMessages.Contains().
            using TestEnvironment env = TestEnvironment.Create(_output);
            ChangeWaves.ResetStateForTests();
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", string.Empty);
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            OutOfProcTaskHostNode node = new();
            SetWarningsAsErrors(node, new HashSet<string> { "MY0001" });
            SetWarningsAsMessages(node, null);
            SetWarningsNotAsErrors(node, null);

            node.ShouldTreatWarningAsError("MY0001").ShouldBeTrue();
        }

        [Fact]
        public void ShouldTreatWarningAsError_SpecificCode_ChangeWaveDisabled_PreservesOldBehavior()
        {
            // When ChangeWave 18.6 is disabled, the old buggy behavior is preserved.
            using TestEnvironment env = TestEnvironment.Create(_output);
            ChangeWaves.ResetStateForTests();
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_6.ToString());
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            OutOfProcTaskHostNode node = new();
            SetWarningsAsErrors(node, new HashSet<string> { "MY0001" });
            SetWarningsAsMessages(node, new HashSet<string>());
            SetWarningsNotAsErrors(node, null);

            // With old behavior, WarningsAsMessages.Contains("MY0001") is checked instead of WarningsAsErrors,
            // so this returns false (bug).
            node.ShouldTreatWarningAsError("MY0001").ShouldBeFalse();
        }

        private static void SetWarningsAsErrors(OutOfProcTaskHostNode node, ICollection<string> value)
        {
            typeof(OutOfProcTaskHostNode)
                .GetProperty("WarningsAsErrors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(node, value);
        }

        private static void SetWarningsAsMessages(OutOfProcTaskHostNode node, ICollection<string> value)
        {
            typeof(OutOfProcTaskHostNode)
                .GetProperty("WarningsAsMessages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(node, value);
        }

        private static void SetWarningsNotAsErrors(OutOfProcTaskHostNode node, ICollection<string> value)
        {
            typeof(OutOfProcTaskHostNode)
                .GetProperty("WarningsNotAsErrors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(node, value);
        }
    }
}
