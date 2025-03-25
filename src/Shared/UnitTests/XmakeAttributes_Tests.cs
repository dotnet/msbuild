// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;

using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class XmakeAttributesTest
    {
        [Fact]
        public void TestIsSpecialTaskAttribute()
        {
            Assert.False(XMakeAttributes.IsSpecialTaskAttribute("NotAnAttribute"));
            Assert.True(XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.xmlns));
            Assert.True(XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.continueOnError));
            Assert.True(XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.condition));
            Assert.True(XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.msbuildArchitecture));
            Assert.True(XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.msbuildRuntime));
        }

        [Fact]
        public void TestIsBadlyCasedSpecialTaskAttribute()
        {
            Assert.False(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute("NotAnAttribute"));
            Assert.False(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(XMakeAttributes.include));
            Assert.False(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(XMakeAttributes.continueOnError));
            Assert.False(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(XMakeAttributes.condition));
            Assert.False(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(XMakeAttributes.msbuildArchitecture));
            Assert.False(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(XMakeAttributes.msbuildRuntime));
            Assert.True(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute("continueOnError"));
            Assert.True(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute("condition"));
            Assert.True(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute("MsbuildRuntime"));
            Assert.True(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute("msbuildarchitecture"));
        }

        [Fact]
        public void TestIsNonBatchingTargetAttribute()
        {
            Assert.False(XMakeAttributes.IsNonBatchingTargetAttribute("NotAnAttribute"));
            Assert.True(XMakeAttributes.IsNonBatchingTargetAttribute(XMakeAttributes.dependsOnTargets));
            Assert.True(XMakeAttributes.IsNonBatchingTargetAttribute(XMakeAttributes.name));
            Assert.True(XMakeAttributes.IsNonBatchingTargetAttribute(XMakeAttributes.condition));
        }

        [Fact]
        public void TestRuntimeValuesMatch()
        {
            Assert.True(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.any, XMakeAttributes.MSBuildRuntimeValues.currentRuntime));
            Assert.True(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.any, XMakeAttributes.MSBuildRuntimeValues.net));
            Assert.True(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.any, XMakeAttributes.MSBuildRuntimeValues.clr4));
            Assert.True(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.clr2, XMakeAttributes.MSBuildRuntimeValues.any));
#if NET5_0_OR_GREATER
            Assert.True(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.currentRuntime, XMakeAttributes.MSBuildRuntimeValues.net));
#else
            Assert.True(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.currentRuntime, XMakeAttributes.MSBuildRuntimeValues.clr4));
#endif

            // Never true
            Assert.False(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.currentRuntime, XMakeAttributes.MSBuildRuntimeValues.clr2));

            Assert.False(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.clr4, XMakeAttributes.MSBuildRuntimeValues.clr2));
            Assert.False(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.clr4, XMakeAttributes.MSBuildRuntimeValues.net));
            Assert.False(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.clr2, XMakeAttributes.MSBuildRuntimeValues.net));
        }

        [Theory]
        [InlineData(XMakeAttributes.MSBuildRuntimeValues.any, XMakeAttributes.MSBuildRuntimeValues.clr4, true, XMakeAttributes.MSBuildRuntimeValues.clr4)]
        [InlineData(XMakeAttributes.MSBuildRuntimeValues.clr4, XMakeAttributes.MSBuildRuntimeValues.any, true, XMakeAttributes.MSBuildRuntimeValues.clr4)]
        [InlineData(XMakeAttributes.MSBuildRuntimeValues.clr2, XMakeAttributes.MSBuildRuntimeValues.any, true, XMakeAttributes.MSBuildRuntimeValues.clr2)]
        [InlineData(XMakeAttributes.MSBuildRuntimeValues.currentRuntime, XMakeAttributes.MSBuildRuntimeValues.clr2, false, null)]
        [InlineData(XMakeAttributes.MSBuildRuntimeValues.clr4, XMakeAttributes.MSBuildRuntimeValues.clr2, false, null)]
        public void TestMergeRuntimeValues(string left, string right, bool success, string expected)
        {
            XMakeAttributes.TryMergeRuntimeValues(left, right, out string mergedRuntime)
                .ShouldBe(success);

            mergedRuntime.ShouldBe(expected);
        }

        [Fact]
        public void TestMergeRuntimeValuesAnyAcceptsCurrent()
        {
            XMakeAttributes.TryMergeRuntimeValues(XMakeAttributes.MSBuildRuntimeValues.any,
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

            Assert.True(XMakeAttributes.ArchitectureValuesMatch(XMakeAttributes.MSBuildArchitectureValues.any, XMakeAttributes.MSBuildArchitectureValues.currentArchitecture));
            Assert.True(XMakeAttributes.ArchitectureValuesMatch(XMakeAttributes.MSBuildArchitectureValues.any, XMakeAttributes.MSBuildArchitectureValues.x64));
            Assert.True(XMakeAttributes.ArchitectureValuesMatch(XMakeAttributes.MSBuildArchitectureValues.x86, XMakeAttributes.MSBuildArchitectureValues.any));
            Assert.True(XMakeAttributes.ArchitectureValuesMatch(XMakeAttributes.MSBuildArchitectureValues.currentArchitecture, currentArchitecture));

            Assert.False(XMakeAttributes.ArchitectureValuesMatch(XMakeAttributes.MSBuildArchitectureValues.currentArchitecture, notCurrentArchitecture));
            Assert.False(XMakeAttributes.ArchitectureValuesMatch(XMakeAttributes.MSBuildArchitectureValues.x64, XMakeAttributes.MSBuildArchitectureValues.x86));
        }

        [Fact]
        public void TestMergeArchitectureValues()
        {
            string currentArchitecture = XMakeAttributes.GetCurrentMSBuildArchitecture();
            string notCurrentArchitecture = Environment.Is64BitProcess ? XMakeAttributes.MSBuildArchitectureValues.x86 : XMakeAttributes.MSBuildArchitectureValues.x64;

            string mergedArchitecture;
            Assert.True(XMakeAttributes.TryMergeArchitectureValues(XMakeAttributes.MSBuildArchitectureValues.any, XMakeAttributes.MSBuildArchitectureValues.currentArchitecture, out mergedArchitecture));
            Assert.Equal(currentArchitecture, mergedArchitecture);

            Assert.True(XMakeAttributes.TryMergeArchitectureValues(XMakeAttributes.MSBuildArchitectureValues.any, XMakeAttributes.MSBuildArchitectureValues.x64, out mergedArchitecture));
            Assert.Equal(XMakeAttributes.MSBuildArchitectureValues.x64, mergedArchitecture);

            Assert.True(XMakeAttributes.TryMergeArchitectureValues(XMakeAttributes.MSBuildArchitectureValues.x86, XMakeAttributes.MSBuildArchitectureValues.any, out mergedArchitecture));
            Assert.Equal(XMakeAttributes.MSBuildArchitectureValues.x86, mergedArchitecture);

            Assert.True(XMakeAttributes.TryMergeArchitectureValues(XMakeAttributes.MSBuildArchitectureValues.currentArchitecture, currentArchitecture, out mergedArchitecture));
            Assert.Equal(currentArchitecture, mergedArchitecture);

            Assert.False(XMakeAttributes.TryMergeArchitectureValues(XMakeAttributes.MSBuildArchitectureValues.currentArchitecture, notCurrentArchitecture, out mergedArchitecture));
            Assert.False(XMakeAttributes.TryMergeArchitectureValues(XMakeAttributes.MSBuildArchitectureValues.x64, XMakeAttributes.MSBuildArchitectureValues.x86, out mergedArchitecture));
        }
    }
}
