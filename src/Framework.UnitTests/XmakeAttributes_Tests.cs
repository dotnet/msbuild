// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class XmakeAttributesTest
{
    [Fact]
    public void TestIsSpecialTaskAttribute()
    {
        XMakeAttributes.IsSpecialTaskAttribute("NotAnAttribute").ShouldBeFalse();
        XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.xmlns).ShouldBeTrue();
        XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.continueOnError).ShouldBeTrue();
        XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.condition).ShouldBeTrue();
        XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.msbuildArchitecture).ShouldBeTrue();
        XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.msbuildRuntime).ShouldBeTrue();
    }

    [Fact]
    public void TestIsBadlyCasedSpecialTaskAttribute()
    {
        XMakeAttributes.IsBadlyCasedSpecialTaskAttribute("NotAnAttribute").ShouldBeFalse();
        XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(XMakeAttributes.include).ShouldBeFalse();
        XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(XMakeAttributes.continueOnError).ShouldBeFalse();
        XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(XMakeAttributes.condition).ShouldBeFalse();
        XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(XMakeAttributes.msbuildArchitecture).ShouldBeFalse();
        XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(XMakeAttributes.msbuildRuntime).ShouldBeFalse();
        XMakeAttributes.IsBadlyCasedSpecialTaskAttribute("continueOnError").ShouldBeTrue();
        XMakeAttributes.IsBadlyCasedSpecialTaskAttribute("condition").ShouldBeTrue();
        XMakeAttributes.IsBadlyCasedSpecialTaskAttribute("MsbuildRuntime").ShouldBeTrue();
        XMakeAttributes.IsBadlyCasedSpecialTaskAttribute("msbuildarchitecture").ShouldBeTrue();
    }

    [Fact]
    public void TestIsNonBatchingTargetAttribute()
    {
        XMakeAttributes.IsNonBatchingTargetAttribute("NotAnAttribute").ShouldBeFalse();
        XMakeAttributes.IsNonBatchingTargetAttribute(XMakeAttributes.dependsOnTargets).ShouldBeTrue();
        XMakeAttributes.IsNonBatchingTargetAttribute(XMakeAttributes.name).ShouldBeTrue();
        XMakeAttributes.IsNonBatchingTargetAttribute(XMakeAttributes.condition).ShouldBeTrue();
    }

    [Fact]
    public void TestRuntimeValuesMatch()
    {
        XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.any, XMakeAttributes.MSBuildRuntimeValues.currentRuntime).ShouldBeTrue();
        XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.any, XMakeAttributes.MSBuildRuntimeValues.net).ShouldBeTrue();
        XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.any, XMakeAttributes.MSBuildRuntimeValues.clr4).ShouldBeTrue();
        XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.clr2, XMakeAttributes.MSBuildRuntimeValues.any).ShouldBeTrue();
#if NET5_0_OR_GREATER
        XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.currentRuntime, XMakeAttributes.MSBuildRuntimeValues.net).ShouldBeTrue();
#else
        XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.currentRuntime, XMakeAttributes.MSBuildRuntimeValues.clr4).ShouldBeTrue();
#endif

        // Never true
        XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.currentRuntime, XMakeAttributes.MSBuildRuntimeValues.clr2).ShouldBeFalse();

        XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.clr4, XMakeAttributes.MSBuildRuntimeValues.clr2).ShouldBeFalse();
        XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.clr4, XMakeAttributes.MSBuildRuntimeValues.net).ShouldBeFalse();
        XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.clr2, XMakeAttributes.MSBuildRuntimeValues.net).ShouldBeFalse();
    }

    [Theory]
    [InlineData(XMakeAttributes.MSBuildRuntimeValues.any, XMakeAttributes.MSBuildRuntimeValues.clr4, true, XMakeAttributes.MSBuildRuntimeValues.clr4)]
    [InlineData(XMakeAttributes.MSBuildRuntimeValues.clr4, XMakeAttributes.MSBuildRuntimeValues.any, true, XMakeAttributes.MSBuildRuntimeValues.clr4)]
    [InlineData(XMakeAttributes.MSBuildRuntimeValues.clr2, XMakeAttributes.MSBuildRuntimeValues.any, true, XMakeAttributes.MSBuildRuntimeValues.clr2)]
    [InlineData(XMakeAttributes.MSBuildRuntimeValues.currentRuntime, XMakeAttributes.MSBuildRuntimeValues.clr2, false, null)]
    [InlineData(XMakeAttributes.MSBuildRuntimeValues.clr4, XMakeAttributes.MSBuildRuntimeValues.clr2, false, null)]
    public void TestMergeRuntimeValues(string left, string right, bool success, string? expected)
    {
        XMakeAttributes.TryMergeRuntimeValues(left, right, out string mergedRuntime)
            .ShouldBe(success);

        mergedRuntime.ShouldBe(expected);
    }

    [Fact]
    public void TestMergeRuntimeValuesAnyAcceptsCurrent()
    {
        XMakeAttributes.TryMergeRuntimeValues(
            XMakeAttributes.MSBuildRuntimeValues.any,
            XMakeAttributes.MSBuildRuntimeValues.currentRuntime,
            out string mergedRuntime)
            .ShouldBeTrue();

        mergedRuntime.ShouldBe(XMakeAttributes.GetCurrentMSBuildRuntime());
    }

    [WindowsFullFrameworkOnlyFact(additionalMessage: "Tests whether 'current' merges with 'clr4' which is true only on Framework.")]
    public void TestMergeRuntimeValuesCurrentToClr4()
    {
        XMakeAttributes.TryMergeRuntimeValues(
            XMakeAttributes.MSBuildRuntimeValues.currentRuntime,
            XMakeAttributes.MSBuildRuntimeValues.clr4,
            out string mergedRuntime).ShouldBeTrue();
        mergedRuntime.ShouldBe(XMakeAttributes.MSBuildRuntimeValues.clr4);

        XMakeAttributes.TryMergeRuntimeValues(
            XMakeAttributes.MSBuildRuntimeValues.currentRuntime,
            XMakeAttributes.MSBuildRuntimeValues.net,
            out mergedRuntime).ShouldBeFalse();
        mergedRuntime.ShouldBeNull();
    }

    [DotNetOnlyFact(additionalMessage: "Tests whether 'current' merges with 'net' which is true only on core.")]
    public void TestMergeRuntimeValuesCurrentToCore()
    {
        XMakeAttributes.TryMergeRuntimeValues(
            XMakeAttributes.MSBuildRuntimeValues.currentRuntime,
            XMakeAttributes.MSBuildRuntimeValues.net,
            out string mergedRuntime).ShouldBeTrue();
        mergedRuntime.ShouldBe(XMakeAttributes.MSBuildRuntimeValues.net);

        XMakeAttributes.TryMergeRuntimeValues(
            XMakeAttributes.MSBuildRuntimeValues.currentRuntime,
            XMakeAttributes.MSBuildRuntimeValues.clr4,
            out mergedRuntime).ShouldBeFalse();
        mergedRuntime.ShouldBeNull();
    }

    [Fact]
    public void TestArchitectureValuesMatch()
    {
        string currentArchitecture = XMakeAttributes.GetCurrentMSBuildArchitecture();
        string notCurrentArchitecture = Environment.Is64BitProcess ? XMakeAttributes.MSBuildArchitectureValues.x86 : XMakeAttributes.MSBuildArchitectureValues.x64;

        XMakeAttributes.ArchitectureValuesMatch(XMakeAttributes.MSBuildArchitectureValues.any, XMakeAttributes.MSBuildArchitectureValues.currentArchitecture).ShouldBeTrue();
        XMakeAttributes.ArchitectureValuesMatch(XMakeAttributes.MSBuildArchitectureValues.any, XMakeAttributes.MSBuildArchitectureValues.x64).ShouldBeTrue();
        XMakeAttributes.ArchitectureValuesMatch(XMakeAttributes.MSBuildArchitectureValues.x86, XMakeAttributes.MSBuildArchitectureValues.any).ShouldBeTrue();
        XMakeAttributes.ArchitectureValuesMatch(XMakeAttributes.MSBuildArchitectureValues.currentArchitecture, currentArchitecture).ShouldBeTrue();

        XMakeAttributes.ArchitectureValuesMatch(XMakeAttributes.MSBuildArchitectureValues.currentArchitecture, notCurrentArchitecture).ShouldBeFalse();
        XMakeAttributes.ArchitectureValuesMatch(XMakeAttributes.MSBuildArchitectureValues.x64, XMakeAttributes.MSBuildArchitectureValues.x86).ShouldBeFalse();
    }

    [Fact]
    public void TestMergeArchitectureValues()
    {
        string currentArchitecture = XMakeAttributes.GetCurrentMSBuildArchitecture();
        string notCurrentArchitecture = Environment.Is64BitProcess ? XMakeAttributes.MSBuildArchitectureValues.x86 : XMakeAttributes.MSBuildArchitectureValues.x64;

        XMakeAttributes.TryMergeArchitectureValues(XMakeAttributes.MSBuildArchitectureValues.any, XMakeAttributes.MSBuildArchitectureValues.currentArchitecture, out string mergedArchitecture).ShouldBeTrue();
        mergedArchitecture.ShouldBe(currentArchitecture);

        XMakeAttributes.TryMergeArchitectureValues(XMakeAttributes.MSBuildArchitectureValues.any, XMakeAttributes.MSBuildArchitectureValues.x64, out mergedArchitecture).ShouldBeTrue();
        mergedArchitecture.ShouldBe(XMakeAttributes.MSBuildArchitectureValues.x64);

        XMakeAttributes.TryMergeArchitectureValues(XMakeAttributes.MSBuildArchitectureValues.x86, XMakeAttributes.MSBuildArchitectureValues.any, out mergedArchitecture).ShouldBeTrue();
        mergedArchitecture.ShouldBe(XMakeAttributes.MSBuildArchitectureValues.x86);

        XMakeAttributes.TryMergeArchitectureValues(XMakeAttributes.MSBuildArchitectureValues.currentArchitecture, currentArchitecture, out mergedArchitecture).ShouldBeTrue();
        mergedArchitecture.ShouldBe(currentArchitecture);

        XMakeAttributes.TryMergeArchitectureValues(XMakeAttributes.MSBuildArchitectureValues.currentArchitecture, notCurrentArchitecture, out _).ShouldBeFalse();
        XMakeAttributes.TryMergeArchitectureValues(XMakeAttributes.MSBuildArchitectureValues.x64, XMakeAttributes.MSBuildArchitectureValues.x86, out _).ShouldBeFalse();
    }

    /// <summary>
    /// When Runtime="NET" is specified and architecture is unspecified (null or "*"/"any") under
    /// .NET Framework MSBuild, the explicit architecture should remain "*" (any) rather than being
    /// pinned to the current process architecture. This avoids attempting to launch e.g. an x86
    /// .NET task host from an x86 .NET Framework MSBuild process, which would fail because the
    /// .NET SDK ships only x64/arm64 binaries (MSB4216). Bitness compatibility with the spawned
    /// dotnet task host is handled by the existing IsAllowedBitnessMismatch tolerance on the
    /// task host server side.
    ///
    /// Under .NET MSBuild the existing behavior is preserved: Runtime="NET" runs in-process so the
    /// architecture resolves to the current process architecture, like any other runtime.
    /// </summary>
    [Fact]
    public void GetExplicitMSBuildArchitecture_NetRuntimeUnspecifiedArchitecture()
    {
        string currentArchitecture = XMakeAttributes.GetCurrentMSBuildArchitecture();
#if NETFRAMEWORK
        XMakeAttributes.GetExplicitMSBuildArchitecture(XMakeAttributes.MSBuildArchitectureValues.any, XMakeAttributes.MSBuildRuntimeValues.net).ShouldBe(XMakeAttributes.MSBuildArchitectureValues.any);
        XMakeAttributes.GetExplicitMSBuildArchitecture(null, XMakeAttributes.MSBuildRuntimeValues.net).ShouldBe(XMakeAttributes.MSBuildArchitectureValues.any);
#else
        XMakeAttributes.GetExplicitMSBuildArchitecture(XMakeAttributes.MSBuildArchitectureValues.any, XMakeAttributes.MSBuildRuntimeValues.net).ShouldBe(currentArchitecture);
        XMakeAttributes.GetExplicitMSBuildArchitecture(null, XMakeAttributes.MSBuildRuntimeValues.net).ShouldBe(currentArchitecture);
#endif

        // Architecture="CurrentArchitecture" is an explicit user request to pin to the current process
        // architecture; it is honored on every TFM, including .NET Framework with Runtime="NET".
        XMakeAttributes.GetExplicitMSBuildArchitecture(XMakeAttributes.MSBuildArchitectureValues.currentArchitecture, XMakeAttributes.MSBuildRuntimeValues.net).ShouldBe(currentArchitecture);
    }

    /// <summary>
    /// When Runtime="NET" is specified and an explicit architecture is provided, the explicit architecture
    /// should be honored (e.g. to allow opting into a specific NET task host architecture).
    /// </summary>
    [Fact]
    public void GetExplicitMSBuildArchitecture_NetRuntimeExplicitArchitecture_ReturnsExplicitArchitecture()
    {
        XMakeAttributes.GetExplicitMSBuildArchitecture(XMakeAttributes.MSBuildArchitectureValues.x64, XMakeAttributes.MSBuildRuntimeValues.net).ShouldBe(XMakeAttributes.MSBuildArchitectureValues.x64);
        XMakeAttributes.GetExplicitMSBuildArchitecture(XMakeAttributes.MSBuildArchitectureValues.x86, XMakeAttributes.MSBuildRuntimeValues.net).ShouldBe(XMakeAttributes.MSBuildArchitectureValues.x86);
        XMakeAttributes.GetExplicitMSBuildArchitecture(XMakeAttributes.MSBuildArchitectureValues.arm64, XMakeAttributes.MSBuildRuntimeValues.net).ShouldBe(XMakeAttributes.MSBuildArchitectureValues.arm64);
    }

    /// <summary>
    /// For non-NET runtimes the explicit architecture resolution should match the current process architecture
    /// when the architecture is unspecified, preserving the existing behavior.
    /// </summary>
    [Fact]
    public void GetExplicitMSBuildArchitecture_NonNetRuntimeUnspecifiedArchitecture_ReturnsCurrentProcessArchitecture()
    {
        string currentArchitecture = XMakeAttributes.GetCurrentMSBuildArchitecture();

        XMakeAttributes.GetExplicitMSBuildArchitecture(XMakeAttributes.MSBuildArchitectureValues.any, XMakeAttributes.MSBuildRuntimeValues.clr4).ShouldBe(currentArchitecture);
        XMakeAttributes.GetExplicitMSBuildArchitecture(XMakeAttributes.MSBuildArchitectureValues.any, XMakeAttributes.MSBuildRuntimeValues.clr2).ShouldBe(currentArchitecture);
        XMakeAttributes.GetExplicitMSBuildArchitecture(XMakeAttributes.MSBuildArchitectureValues.any, runtime: null).ShouldBe(currentArchitecture);
    }
}
