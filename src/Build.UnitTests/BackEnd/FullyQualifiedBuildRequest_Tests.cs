// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    [TestClass]
    public class FullyQualifiedBuildRequest_Tests
    {
        [MSBuildTestMethod]
        public void TestConstructorGood()
        {
            BuildRequestData data1 = new BuildRequestData("foo", new Dictionary<string, string>(), "tools", Array.Empty<string>(), null);
            FullyQualifiedBuildRequest request = new FullyQualifiedBuildRequest(new BuildRequestConfiguration(data1, "2.0"), new string[1] { "foo" }, true);

            request = new FullyQualifiedBuildRequest(new BuildRequestConfiguration(data1, "2.0"), Array.Empty<string>(), true);

            BuildRequestData data3 = new BuildRequestData("foo", new Dictionary<string, string>(), "tools", Array.Empty<string>(), null);
            request = new FullyQualifiedBuildRequest(new BuildRequestConfiguration(data1, "2.0"), Array.Empty<string>(), false);
        }

        [MSBuildTestMethod]
        public void TestConstructorBad1()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                FullyQualifiedBuildRequest request = new FullyQualifiedBuildRequest(null, new string[1] { "foo" }, true);
            });
        }

        [MSBuildTestMethod]
        public void TestConstructorBad2()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                FullyQualifiedBuildRequest request = new FullyQualifiedBuildRequest(new BuildRequestConfiguration(new BuildRequestData("foo", new Dictionary<string, string>(), "tools", Array.Empty<string>(), null), "2.0"), null, true);
            });
        }
        [MSBuildTestMethod]
        public void TestProperties()
        {
            BuildRequestData data = new BuildRequestData("foo", new Dictionary<string, string>(), "tools", Array.Empty<string>(), null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(data, "2.0");
            FullyQualifiedBuildRequest request = new FullyQualifiedBuildRequest(config, new string[1] { "foo" }, true);
            Assert.AreEqual(request.Config, config);
            Assert.ContainsSingle(request.Targets);
            Assert.AreEqual("foo", request.Targets[0]);
            Assert.IsTrue(request.ResultsNeeded);
        }
    }
}
