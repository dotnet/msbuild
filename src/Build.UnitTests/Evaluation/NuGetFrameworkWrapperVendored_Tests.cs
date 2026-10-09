// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using NuGet.Frameworks;
using Shouldly;
using Xunit;
using VendoredFrameworks = Microsoft.Build.NuGetFrameworks;

#nullable disable

namespace Microsoft.Build.UnitTests.Evaluation
{
    /// <summary>
    /// On .NET Framework, MSBuild ships a vendored copy of NuGet.Frameworks compiled into
    /// Microsoft.Build (see src\Build\Utilities\NuGetFrameworks) and, by default (Change Wave 18.9),
    /// calls it instead of reflecting into the SDK's NuGet.Frameworks.dll. These tests pin that
    /// vendored copy's behavior against the live NuGet.Frameworks package the repo references and keeps
    /// current via darc: any divergence here means the vendored snapshot has drifted and should be
    /// re-cloned. Names below qualified with <c>VendoredFrameworks</c> are the vendored types; the
    /// unqualified NuGet.Frameworks names are the reference package (the oracle).
    /// </summary>
    public class NuGetFrameworkWrapperVendored_Tests
    {
        /// <summary>
        /// A broad spread of target framework monikers, including legacy, netstandard, modern net,
        /// platform-flavored, portable/PCL, Xamarin/UAP, and invalid inputs.
        /// </summary>
        public static IEnumerable<object[]> TargetFrameworks()
        {
            string[] tfms =
            [
                "net11", "net20", "net35", "net40", "net403", "net45", "net451", "net452",
                "net46", "net461", "net462", "net47", "net471", "net472", "net48", "net481",
                "netcoreapp1.0", "netcoreapp1.1", "netcoreapp2.0", "netcoreapp2.1", "netcoreapp3.0", "netcoreapp3.1",
                "netstandard1.0", "netstandard1.1", "netstandard1.2", "netstandard1.3", "netstandard1.4",
                "netstandard1.5", "netstandard1.6", "netstandard2.0", "netstandard2.1",
                "net5.0", "net6.0", "net7.0", "net8.0", "net9.0", "net10.0",
                "net5.0-windows", "net6.0-windows10.0.19041.0", "net7.0-ios", "net8.0-android",
                "net8.0-maccatalyst", "net8.0-tizen", "net6.0-macos",
                "monoandroid", "monotouch", "xamarinios", "xamarinmac", "xamarinwatchos",
                "uap10.0", "uap10.0.16299", "tizen40",
                "portable-net45+win8+wp8", "portable-net451+win81",
                "win", "win8", "wp8", "wpa81", "sl5", "netmf", "native",
                "net472-client", "NET6.0", "Net8.0-Windows",
                "any", "unsupported", "foo", "",
            ];

            foreach (string tfm in tfms)
            {
                yield return [tfm];
            }
        }

        /// <summary>
        /// Pairs exercising both compatible and incompatible directions across framework families.
        /// </summary>
        public static IEnumerable<object[]> CompatibilityPairs()
        {
            (string target, string candidate)[] pairs =
            [
                ("net45", "net40"), ("net40", "net45"), ("net472", "net48"), ("net48", "net472"),
                ("net472", "netstandard2.0"), ("netstandard2.0", "net472"),
                ("net6.0", "netstandard2.0"), ("netstandard2.0", "net6.0"),
                ("net6.0", "netstandard2.1"), ("net6.0", "net5.0"), ("net5.0", "net6.0"),
                ("net5.0", "netcoreapp3.1"), ("netcoreapp3.1", "net5.0"),
                ("netstandard2.0", "netstandard1.6"), ("netstandard1.6", "netstandard2.0"),
                ("net6.0-windows", "net6.0"), ("net6.0", "net6.0-windows"),
                ("net6.0-windows10.0.19041.0", "net6.0-windows"),
                ("net8.0", "net6.0"), ("net6.0", "net8.0"),
                ("net8.0-android", "netstandard2.0"), ("uap10.0", "netstandard2.0"),
                ("monoandroid", "netstandard1.6"), ("xamarinios", "netstandard2.0"),
                ("net45", "netstandard1.1"), ("netstandard1.1", "net45"),
                ("net472", "foo"), ("foo", "net472"),
            ];

            foreach ((string target, string candidate) in pairs)
            {
                yield return [target, candidate];
            }
        }

        /// <summary>
        /// Inputs for FilterTargetFrameworks; the oracle is computed by running MSBuild's own filtering
        /// logic over the reference package via <see cref="ReferenceTfmAdapter"/>.
        /// </summary>
        public static IEnumerable<object[]> FilterCases()
        {
            (string incoming, string filter)[] cases =
            [
                ("netstandard2.0;net6.0;net472", "net6.0"),
                ("netstandard2.0;net6.0-windows", "net6.0;netstandard2.0;net472"),
                ("netstandard2.0;net472", "net6.0;netstandard2.0;net472"),
                ("net5.0;net6.0;net7.0;net8.0", "net6.0;net8.0"),
                ("net472;net48", "net48"),
                ("net6.0-windows;net6.0-android;net6.0", "net6.0"),
                ("netstandard1.6;netstandard2.0;netstandard2.1", "netstandard2.0"),
                ("net6.0;netcoreapp3.1", "net5.0;net6.0"),
                ("", "net6.0"),
                ("net6.0", ""),
            ];

            foreach ((string incoming, string filter) in cases)
            {
                yield return [incoming, filter];
            }
        }

        [Theory]
        [MemberData(nameof(TargetFrameworks))]
        public void VendoredParse_MatchesReferencePackage(string tfm)
        {
            VendoredFrameworks.NuGetFramework vendored = VendoredFrameworks.NuGetFramework.Parse(tfm);
            NuGetFramework reference = NuGetFramework.Parse(tfm);

            vendored.Framework.ShouldBe(reference.Framework, $"Framework mismatch for '{tfm}'");
            vendored.Version.ShouldBe(reference.Version, $"Version mismatch for '{tfm}'");
            vendored.Platform.ShouldBe(reference.Platform, $"Platform mismatch for '{tfm}'");
            vendored.PlatformVersion.ShouldBe(reference.PlatformVersion, $"PlatformVersion mismatch for '{tfm}'");
            vendored.AllFrameworkVersions.ShouldBe(reference.AllFrameworkVersions, $"AllFrameworkVersions mismatch for '{tfm}'");
        }

        [Theory]
        [MemberData(nameof(TargetFrameworks))]
        public void VendoredWrapper_FrameworkAndPlatform_MatchReferencePackage(string tfm)
        {
            INuGetFrameworkWrapper vendored = new NuGetFrameworkWrapperVendored();
            NuGetFramework reference = NuGetFramework.Parse(tfm);

            vendored.GetTargetFrameworkIdentifier(tfm).ShouldBe(reference.Framework, $"identifier mismatch for '{tfm}'");
            vendored.GetTargetPlatformIdentifier(tfm).ShouldBe(reference.Platform, $"platform mismatch for '{tfm}'");

            foreach (int minVersionPartCount in new[] { 2, 3 })
            {
                vendored.GetTargetFrameworkVersion(tfm, minVersionPartCount)
                    .ShouldBe(NuGetFrameworkWrapper.GetNonZeroVersionParts(reference.Version, minVersionPartCount), $"version mismatch for '{tfm}' ({minVersionPartCount})");
                vendored.GetTargetPlatformVersion(tfm, minVersionPartCount)
                    .ShouldBe(NuGetFrameworkWrapper.GetNonZeroVersionParts(reference.PlatformVersion, minVersionPartCount), $"platform version mismatch for '{tfm}' ({minVersionPartCount})");
            }
        }

        [Theory]
        [MemberData(nameof(CompatibilityPairs))]
        public void VendoredWrapper_IsCompatible_MatchesReferencePackage(string target, string candidate)
        {
            INuGetFrameworkWrapper vendored = new NuGetFrameworkWrapperVendored();
            bool reference = DefaultCompatibilityProvider.Instance.IsCompatible(NuGetFramework.Parse(target), NuGetFramework.Parse(candidate));

            vendored.IsCompatible(target, candidate).ShouldBe(reference, $"IsCompatible mismatch for target '{target}', candidate '{candidate}'");
        }

        [Theory]
        [MemberData(nameof(FilterCases))]
        public void VendoredWrapper_FilterTargetFrameworks_MatchesReferencePackage(string incoming, string filter)
        {
            INuGetFrameworkWrapper vendored = new NuGetFrameworkWrapperVendored();
            string reference = NuGetFrameworkWrapper.FilterTargetFrameworks<NuGetFramework, ReferenceTfmAdapter>(incoming, filter, default);

            vendored.FilterTargetFrameworks(incoming, filter).ShouldBe(reference, $"FilterTargetFrameworks mismatch for incoming '{incoming}', filter '{filter}'");
        }

        /// <summary>
        /// With Change Wave 18.9 opted out, the wrapper falls back to the reflection/AppDomain path.
        /// It must still agree with the reference package, preserving the historical behavior.
        /// </summary>
        [Theory]
        [InlineData("net472")]
        [InlineData("net6.0")]
        [InlineData("netstandard2.0")]
        [InlineData("net8.0-windows10.0.19041.0")]
        public void ChangeWaveOptOut_ReflectionPath_MatchesReferencePackage(string tfm)
        {
            using TestEnvironment env = TestEnvironment.Create();
            env.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_9.ToString());
            ChangeWaves.ResetStateForTests();
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            // TestEnvironment.Dispose clears the environment variable and resets ChangeWaves and
            // BuildEnvironmentHelper, so no manual cleanup is needed here.
            NuGetFrameworkWrapper wrapper = NuGetFrameworkWrapper.CreateInstance();
            NuGetFramework reference = NuGetFramework.Parse(tfm);

            wrapper.GetTargetFrameworkIdentifier(tfm).ShouldBe(reference.Framework);
            wrapper.GetTargetFrameworkVersion(tfm, 2).ShouldBe(NuGetFrameworkWrapper.GetNonZeroVersionParts(reference.Version, 2));
            wrapper.GetTargetPlatformIdentifier(tfm).ShouldBe(reference.Platform);
            wrapper.GetTargetPlatformVersion(tfm, 2).ShouldBe(NuGetFrameworkWrapper.GetNonZeroVersionParts(reference.PlatformVersion, 2));
        }

        /// <summary>
        /// Runs MSBuild's generic <c>FilterTargetFrameworks</c> filtering over the reference package so the
        /// only difference between expected and actual output is which NuGet.Frameworks implementation parses.
        /// </summary>
        private readonly struct ReferenceTfmAdapter : NuGetFrameworkWrapper.ITfmAdapter<NuGetFramework>
        {
            public NuGetFramework Parse(string tfm) => NuGetFramework.Parse(tfm);
            public string GetFramework(NuGetFramework parsed) => parsed.Framework;
            public bool GetAllFrameworkVersions(NuGetFramework parsed) => parsed.AllFrameworkVersions;
            public Version GetVersion(NuGetFramework parsed) => parsed.Version;
        }
    }
}

#endif
