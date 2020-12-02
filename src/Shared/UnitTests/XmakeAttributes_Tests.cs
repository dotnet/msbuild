// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using Xunit;

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
            Assert.True(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.any, XMakeAttributes.MSBuildRuntimeValues.clr4));
            Assert.True(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.clr2, XMakeAttributes.MSBuildRuntimeValues.any));
            Assert.True(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.currentRuntime, XMakeAttributes.MSBuildRuntimeValues.clr4));

            Assert.False(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.currentRuntime, XMakeAttributes.MSBuildRuntimeValues.clr2));
            Assert.False(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.clr4, XMakeAttributes.MSBuildRuntimeValues.clr2));
        }

        [Fact]
        public void TestMergeRuntimeValues()
        {
            string mergedRuntime;
            Assert.True(XMakeAttributes.TryMergeRuntimeValues(XMakeAttributes.MSBuildRuntimeValues.any, XMakeAttributes.MSBuildRuntimeValues.currentRuntime, out mergedRuntime));
            Assert.Equal(XMakeAttributes.MSBuildRuntimeValues.clr4, mergedRuntime);

            Assert.True(XMakeAttributes.TryMergeRuntimeValues(XMakeAttributes.MSBuildRuntimeValues.any, XMakeAttributes.MSBuildRuntimeValues.clr4, out mergedRuntime));
            Assert.Equal(XMakeAttributes.MSBuildRuntimeValues.clr4, mergedRuntime);

            Assert.True(XMakeAttributes.TryMergeRuntimeValues(XMakeAttributes.MSBuildRuntimeValues.clr2, XMakeAttributes.MSBuildRuntimeValues.any, out mergedRuntime));
            Assert.Equal(XMakeAttributes.MSBuildRuntimeValues.clr2, mergedRuntime);

            Assert.True(XMakeAttributes.TryMergeRuntimeValues(XMakeAttributes.MSBuildRuntimeValues.currentRuntime, XMakeAttributes.MSBuildRuntimeValues.clr4, out mergedRuntime));
            Assert.Equal(XMakeAttributes.MSBuildRuntimeValues.clr4, mergedRuntime);

            Assert.False(XMakeAttributes.TryMergeRuntimeValues(XMakeAttributes.MSBuildRuntimeValues.currentRuntime, XMakeAttributes.MSBuildRuntimeValues.clr2, out mergedRuntime));
            Assert.Null(mergedRuntime);

            Assert.False(XMakeAttributes.TryMergeRuntimeValues(XMakeAttributes.MSBuildRuntimeValues.clr4, XMakeAttributes.MSBuildRuntimeValues.clr2, out mergedRuntime));
            Assert.Null(mergedRuntime);
        }

        [Fact]
        public void TestArchitectureValuesMatch()
        {
            string currentArchitecture = EnvironmentUtilities.Is64BitProcess ? XMakeAttributes.MSBuildArchitectureValues.x64 : XMakeAttributes.MSBuildArchitectureValues.x86;
            string notCurrentArchitecture = EnvironmentUtilities.Is64BitProcess ? XMakeAttributes.MSBuildArchitectureValues.x86 : XMakeAttributes.MSBuildArchitectureValues.x64;

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
            string currentArchitecture = EnvironmentUtilities.Is64BitProcess ? XMakeAttributes.MSBuildArchitectureValues.x64 : XMakeAttributes.MSBuildArchitectureValues.x86;
            string notCurrentArchitecture = EnvironmentUtilities.Is64BitProcess ? XMakeAttributes.MSBuildArchitectureValues.x86 : XMakeAttributes.MSBuildArchitectureValues.x64;

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
