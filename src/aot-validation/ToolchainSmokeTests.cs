// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Build.AotValidation;

/// <summary>
/// Smoke test that validates the MSTest + Microsoft.Testing.Platform + Native AOT toolchain
/// itself, independent of the MSBuild object model. If this passes in an AOT-published run,
/// the test host works under AOT and any failures in the object-model tests are real findings.
/// </summary>
[TestClass]
public sealed class ToolchainSmokeTests
{
    [TestMethod]
    public void TestHostRunsUnderAot()
    {
        Assert.AreEqual(4, 2 + 2);
    }
}
