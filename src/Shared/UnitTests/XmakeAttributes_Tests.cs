// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class XmakeAttributesTest
    {
        [TestMethod]
        public void TestIsSpecialTaskAttribute()
        {
            Assert.IsFalse(XMakeAttributes.IsSpecialTaskAttribute("NotAnAttribute"));
            Assert.IsTrue(XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.xmlns));
            Assert.IsTrue(XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.continueOnError));
            Assert.IsTrue(XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.condition));
            Assert.IsTrue(XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.msbuildArchitecture));
            Assert.IsTrue(XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.msbuildRuntime));
        }

        [TestMethod]
        public void TestIsBadlyCasedSpecialTaskAttribute()
        {
            Assert.IsFalse(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute("NotAnAttribute"));
            Assert.IsFalse(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(XMakeAttributes.include));
            Assert.IsFalse(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(XMakeAttributes.continueOnError));
            Assert.IsFalse(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(XMakeAttributes.condition));
            Assert.IsFalse(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(XMakeAttributes.msbuildArchitecture));
            Assert.IsFalse(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(XMakeAttributes.msbuildRuntime));
            Assert.IsTrue(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute("continueOnError"));
            Assert.IsTrue(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute("condition"));
            Assert.IsTrue(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute("MsbuildRuntime"));
            Assert.IsTrue(XMakeAttributes.IsBadlyCasedSpecialTaskAttribute("msbuildarchitecture"));
        }

        [TestMethod]
        public void TestIsNonBatchingTargetAttribute()
        {
            Assert.IsFalse(XMakeAttributes.IsNonBatchingTargetAttribute("NotAnAttribute"));
            Assert.IsTrue(XMakeAttributes.IsNonBatchingTargetAttribute(XMakeAttributes.dependsOnTargets));
            Assert.IsTrue(XMakeAttributes.IsNonBatchingTargetAttribute(XMakeAttributes.name));
            Assert.IsTrue(XMakeAttributes.IsNonBatchingTargetAttribute(XMakeAttributes.condition));
        }

        [TestMethod]
        public void TestRuntimeValuesMatch()
        {
            Assert.IsTrue(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.any, XMakeAttributes.MSBuildRuntimeValues.currentRuntime));
            Assert.IsTrue(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.any, XMakeAttributes.MSBuildRuntimeValues.clr4));
            Assert.IsTrue(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.clr2, XMakeAttributes.MSBuildRuntimeValues.any));
            Assert.IsTrue(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.currentRuntime, XMakeAttributes.MSBuildRuntimeValues.clr4));

            Assert.IsFalse(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.currentRuntime, XMakeAttributes.MSBuildRuntimeValues.clr2));
            Assert.IsFalse(XMakeAttributes.RuntimeValuesMatch(XMakeAttributes.MSBuildRuntimeValues.clr4, XMakeAttributes.MSBuildRuntimeValues.clr2));
        }

        [TestMethod]
        public void TestMergeRuntimeValues()
        {
            string mergedRuntime = null;

            Assert.IsTrue(XMakeAttributes.TryMergeRuntimeValues(XMakeAttributes.MSBuildRuntimeValues.any, XMakeAttributes.MSBuildRuntimeValues.currentRuntime, out mergedRuntime));
            Assert.AreEqual(XMakeAttributes.MSBuildRuntimeValues.clr4, mergedRuntime);

            Assert.IsTrue(XMakeAttributes.TryMergeRuntimeValues(XMakeAttributes.MSBuildRuntimeValues.any, XMakeAttributes.MSBuildRuntimeValues.clr4, out mergedRuntime));
            Assert.AreEqual(XMakeAttributes.MSBuildRuntimeValues.clr4, mergedRuntime);

            Assert.IsTrue(XMakeAttributes.TryMergeRuntimeValues(XMakeAttributes.MSBuildRuntimeValues.clr2, XMakeAttributes.MSBuildRuntimeValues.any, out mergedRuntime));
            Assert.AreEqual(XMakeAttributes.MSBuildRuntimeValues.clr2, mergedRuntime);

            Assert.IsTrue(XMakeAttributes.TryMergeRuntimeValues(XMakeAttributes.MSBuildRuntimeValues.currentRuntime, XMakeAttributes.MSBuildRuntimeValues.clr4, out mergedRuntime));
            Assert.AreEqual(XMakeAttributes.MSBuildRuntimeValues.clr4, mergedRuntime);

            Assert.IsFalse(XMakeAttributes.TryMergeRuntimeValues(XMakeAttributes.MSBuildRuntimeValues.currentRuntime, XMakeAttributes.MSBuildRuntimeValues.clr2, out mergedRuntime));
            Assert.IsNull(mergedRuntime);

            Assert.IsFalse(XMakeAttributes.TryMergeRuntimeValues(XMakeAttributes.MSBuildRuntimeValues.clr4, XMakeAttributes.MSBuildRuntimeValues.clr2, out mergedRuntime));
            Assert.IsNull(mergedRuntime);
        }

        [TestMethod]
        public void TestArchitectureValuesMatch()
        {
            string currentArchitecture = Environment.Is64BitProcess ? XMakeAttributes.MSBuildArchitectureValues.x64 : XMakeAttributes.MSBuildArchitectureValues.x86;
            string notCurrentArchitecture = Environment.Is64BitProcess ? XMakeAttributes.MSBuildArchitectureValues.x86 : XMakeAttributes.MSBuildArchitectureValues.x64;

            Assert.IsTrue(XMakeAttributes.ArchitectureValuesMatch(XMakeAttributes.MSBuildArchitectureValues.any, XMakeAttributes.MSBuildArchitectureValues.currentArchitecture));
            Assert.IsTrue(XMakeAttributes.ArchitectureValuesMatch(XMakeAttributes.MSBuildArchitectureValues.any, XMakeAttributes.MSBuildArchitectureValues.x64));
            Assert.IsTrue(XMakeAttributes.ArchitectureValuesMatch(XMakeAttributes.MSBuildArchitectureValues.x86, XMakeAttributes.MSBuildArchitectureValues.any));
            Assert.IsTrue(XMakeAttributes.ArchitectureValuesMatch(XMakeAttributes.MSBuildArchitectureValues.currentArchitecture, currentArchitecture));

            Assert.IsFalse(XMakeAttributes.ArchitectureValuesMatch(XMakeAttributes.MSBuildArchitectureValues.currentArchitecture, notCurrentArchitecture));
            Assert.IsFalse(XMakeAttributes.ArchitectureValuesMatch(XMakeAttributes.MSBuildArchitectureValues.x64, XMakeAttributes.MSBuildArchitectureValues.x86));
        }

        [TestMethod]
        public void TestMergeArchitectureValues()
        {
            string mergedArchitecture = null;

            string currentArchitecture = Environment.Is64BitProcess ? XMakeAttributes.MSBuildArchitectureValues.x64 : XMakeAttributes.MSBuildArchitectureValues.x86;
            string notCurrentArchitecture = Environment.Is64BitProcess ? XMakeAttributes.MSBuildArchitectureValues.x86 : XMakeAttributes.MSBuildArchitectureValues.x64;

            Assert.IsTrue(XMakeAttributes.TryMergeArchitectureValues(XMakeAttributes.MSBuildArchitectureValues.any, XMakeAttributes.MSBuildArchitectureValues.currentArchitecture, out mergedArchitecture));
            Assert.AreEqual(currentArchitecture, mergedArchitecture);

            Assert.IsTrue(XMakeAttributes.TryMergeArchitectureValues(XMakeAttributes.MSBuildArchitectureValues.any, XMakeAttributes.MSBuildArchitectureValues.x64, out mergedArchitecture));
            Assert.AreEqual(XMakeAttributes.MSBuildArchitectureValues.x64, mergedArchitecture);

            Assert.IsTrue(XMakeAttributes.TryMergeArchitectureValues(XMakeAttributes.MSBuildArchitectureValues.x86, XMakeAttributes.MSBuildArchitectureValues.any, out mergedArchitecture));
            Assert.AreEqual(XMakeAttributes.MSBuildArchitectureValues.x86, mergedArchitecture);

            Assert.IsTrue(XMakeAttributes.TryMergeArchitectureValues(XMakeAttributes.MSBuildArchitectureValues.currentArchitecture, currentArchitecture, out mergedArchitecture));
            Assert.AreEqual(currentArchitecture, mergedArchitecture);

            Assert.IsFalse(XMakeAttributes.TryMergeArchitectureValues(XMakeAttributes.MSBuildArchitectureValues.currentArchitecture, notCurrentArchitecture, out mergedArchitecture));
            Assert.IsFalse(XMakeAttributes.TryMergeArchitectureValues(XMakeAttributes.MSBuildArchitectureValues.x64, XMakeAttributes.MSBuildArchitectureValues.x86, out mergedArchitecture));
        }
    }
}