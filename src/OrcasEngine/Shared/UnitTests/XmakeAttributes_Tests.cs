// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using NUnit.Framework;

using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class XmakeAttributesTest
    {
        [Test]
        public void TestAttributeMethods()
        {
            Assert.IsFalse(XMakeAttributes.IsSpecialTaskAttribute("NotAnAttribute"));
            Assert.IsTrue(XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.xmlns));
            Assert.IsTrue(XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.continueOnError));
            Assert.IsTrue(XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.condition));
            Assert.IsTrue(XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.msbuildArchitecture));
            Assert.IsTrue(XMakeAttributes.IsSpecialTaskAttribute(XMakeAttributes.msbuildRuntime));

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

            Assert.IsFalse(XMakeAttributes.IsNonBatchingTargetAttribute("NotAnAttribute"));
            Assert.IsTrue(XMakeAttributes.IsNonBatchingTargetAttribute(XMakeAttributes.dependsOnTargets));
            Assert.IsTrue(XMakeAttributes.IsNonBatchingTargetAttribute(XMakeAttributes.name));
            Assert.IsTrue(XMakeAttributes.IsNonBatchingTargetAttribute(XMakeAttributes.condition));
        }
    }
}
