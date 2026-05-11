// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.Tasks.UnitTests
{
    public class RawFilenameResolver_Tests : IDisposable
    {
        private readonly TestEnvironment _env;

        public RawFilenameResolver_Tests(ITestOutputHelper testOutput)
        {
            _env = TestEnvironment.Create(testOutput);
        }

        public void Dispose()
        {
            ChangeWaves.ResetStateForTests();
            _env.Dispose();
        }

        /// <summary>
        /// Under Wave18_8 (default), an empty rawFileNameCandidate is silently treated as a no-op:
        /// the resolver returns false, sets no output, and does not add a "considered and rejected"
        /// entry. This avoids the ArgumentException that AbsolutePath validation would throw for
        /// the empty string.
        /// </summary>
        [Fact]
        public void EmptyRawFileName_SilentlySkipped()
        {
            List<ResolutionSearchLocation> considered = new();

            bool result = Resolve(rawFileNameCandidate: string.Empty, considered);

            result.ShouldBeFalse();
            considered.ShouldBeEmpty();
        }

        /// <summary>
        /// Under Wave18_8 disabled (legacy), an empty rawFileNameCandidate is added to the
        /// considered-and-rejected diagnostic list (matching pre-Wave18_8 behavior) and the
        /// resolver returns false.
        /// </summary>
        [Fact]
        public void EmptyRawFileName_WaveDisabled_AddsConsideredAndRejected()
        {
            ChangeWaves.ResetStateForTests();
            _env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_8.ToString());
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            List<ResolutionSearchLocation> considered = new();

            bool result = Resolve(rawFileNameCandidate: string.Empty, considered);

            result.ShouldBeFalse();
            considered.Count.ShouldBe(1);
            considered[0].FileNameAttempted.ShouldBe(string.Empty);
            considered[0].Reason.ShouldBe(NoMatchReason.NotAFileNameOnDisk);
        }

        /// <summary>
        /// A null rawFileNameCandidate is silently skipped under both wave-on and wave-off
        /// (this matches both pre- and post-Wave18_8 behavior; null is the "not provided" sentinel).
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void NullRawFileName_SilentlySkipped(bool waveEnabled)
        {
            if (!waveEnabled)
            {
                ChangeWaves.ResetStateForTests();
                _env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_8.ToString());
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();
            }

            List<ResolutionSearchLocation> considered = new();

            bool result = Resolve(rawFileNameCandidate: null, considered);

            result.ShouldBeFalse();
            considered.ShouldBeEmpty();
        }

        private bool Resolve(string rawFileNameCandidate, List<ResolutionSearchLocation> considered)
        {
            var resolver = new RawFilenameResolver(
                searchPathElement: "{RawFileName}",
                getAssemblyName: (path) => throw new NotImplementedException(),
                fileExists: p => FileUtilities.FileExistsNoThrow(p),
                getRuntimeVersion: (path) => throw new NotImplementedException(),
                targetedRuntimeVesion: Version.Parse("4.0.30319"),
                taskEnvironment: TaskEnvironmentHelper.CreateForTest());

            return resolver.Resolve(
                assemblyName: new AssemblyNameExtension("System"),
                sdkName: string.Empty,
                rawFileNameCandidate: rawFileNameCandidate,
                isPrimaryProjectReference: true,
                isImmutableFrameworkReference: false,
                wantSpecificVersion: false,
                executableExtensions: new[] { ".dll", ".exe" },
                hintPath: string.Empty,
                assemblyFolderKey: string.Empty,
                assembliesConsideredAndRejected: considered,
                foundPath: out _,
                userRequestedSpecificFile: out _);
        }
    }
}
