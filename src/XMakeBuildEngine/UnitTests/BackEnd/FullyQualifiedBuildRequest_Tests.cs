// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;

using NUnit.Framework;

namespace Microsoft.Build.UnitTests.BackEnd
{
    [TestFixture]
    public class FullyQualifiedBuildRequest_Tests
    {
        [SetUp]
        public void SetUp()
        {
        }

        [TearDown]
        public void TearDown()
        {
        }

        [Test]
        public void TestConstructorGood()
        {
            BuildRequestData data1 = new BuildRequestData("foo", new Dictionary<string, string>(), "tools", new string[0], null);
            FullyQualifiedBuildRequest request = new FullyQualifiedBuildRequest(new BuildRequestConfiguration(data1, "2.0"), new string[1] { "foo" }, true);

            request = new FullyQualifiedBuildRequest(new BuildRequestConfiguration(data1, "2.0"), new string[0] { }, true);

            BuildRequestData data3 = new BuildRequestData("foo", new Dictionary<string, string>(), "tools", new string[0], null);
            request = new FullyQualifiedBuildRequest(new BuildRequestConfiguration(data1, "2.0"), new string[0] { }, false);
        }

        [ExpectedException(typeof(ArgumentNullException))]
        [Test]
        public void TestConstructorBad1()
        {
            FullyQualifiedBuildRequest request = new FullyQualifiedBuildRequest(null, new string[1] { "foo" }, true);
        }

        [ExpectedException(typeof(ArgumentNullException))]
        [Test]
        public void TestConstructorBad2()
        {
            FullyQualifiedBuildRequest request = new FullyQualifiedBuildRequest(new BuildRequestConfiguration(new BuildRequestData("foo", new Dictionary<string, string>(), "tools", new string[0], null), "2.0"), null, true);
        }

        [Test]
        public void TestProperties()
        {
            BuildRequestData data = new BuildRequestData("foo", new Dictionary<string, string>(), "tools", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(data, "2.0");
            FullyQualifiedBuildRequest request = new FullyQualifiedBuildRequest(config, new string[1] { "foo" }, true);
            Assert.AreEqual(request.Config, config);
            Assert.AreEqual(request.Targets.Length, 1);
            Assert.AreEqual(request.Targets[0], "foo");
            Assert.AreEqual(request.ResultsNeeded, true);
        }
    }
}