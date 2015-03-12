// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests.BackEnd
{
    [TestClass]
    public class BuildRequest_Tests
    {
        private int _nodeRequestId;

        [TestInitialize]
        public void SetUp()
        {
            _nodeRequestId = 1;
        }

        [TestCleanup]
        public void TearDown()
        {
        }

        [ExpectedException(typeof(ArgumentNullException))]
        [TestMethod]
        public void TestConstructorBad()
        {
            BuildRequest request = CreateNewBuildRequest(0, null);
        }

        [TestMethod]
        public void TestConstructorGood()
        {
            BuildRequest request = CreateNewBuildRequest(0, new string[0] { });
        }

        [TestMethod]
        public void TestConfigurationId()
        {
            BuildRequest request = CreateNewBuildRequest(0, new string[0] { });
            Assert.AreEqual(0, request.ConfigurationId);

            BuildRequest request2 = CreateNewBuildRequest(1, new string[0] { });
            Assert.AreEqual(1, request2.ConfigurationId);

            BuildRequest request3 = CreateNewBuildRequest(-1, new string[0] { });
            Assert.AreEqual(-1, request3.ConfigurationId);
        }

        [TestMethod]
        public void TestConfigurationResolved()
        {
            BuildRequest request = CreateNewBuildRequest(0, new string[0] { });
            Assert.IsFalse(request.IsConfigurationResolved);

            BuildRequest request2 = CreateNewBuildRequest(1, new string[0] { });
            Assert.IsTrue(request2.IsConfigurationResolved);

            BuildRequest request3 = CreateNewBuildRequest(-1, new string[0] { });
            Assert.IsFalse(request3.IsConfigurationResolved);
        }

        [TestMethod]
        public void TestTargets()
        {
            BuildRequest request = CreateNewBuildRequest(0, new string[0] { });
            Assert.IsNotNull(request.Targets);
            Assert.AreEqual(0, request.Targets.Count);

            BuildRequest request2 = CreateNewBuildRequest(1, new string[1] { "a" });
            Assert.IsNotNull(request2.Targets);
            Assert.AreEqual(1, request2.Targets.Count);
            Assert.AreEqual("a", request2.Targets[0]);
        }

        [TestMethod]
        public void TestPacketType()
        {
            BuildRequest request = CreateNewBuildRequest(0, new string[0] { });
            Assert.AreEqual(NodePacketType.BuildRequest, request.Type);
        }

        [TestMethod]
        public void TestResolveConfigurationGood()
        {
            BuildRequest request = CreateNewBuildRequest(0, new string[0] { });
            request.ResolveConfiguration(1);
            Assert.IsTrue(request.IsConfigurationResolved);
            Assert.AreEqual(1, request.ConfigurationId);
        }

        [ExpectedException(typeof(InternalErrorException))]
        [TestMethod]
        public void TestResolveConfigurationBad()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0] { });
            request.ResolveConfiguration(2);
        }

        [ExpectedException(typeof(InternalErrorException))]
        [TestMethod]
        public void TestResolveConfigurationBad2()
        {
            BuildRequest request = CreateNewBuildRequest(0, new string[0] { });
            request.ResolveConfiguration(-1);
        }

        [TestMethod]
        public void TestTranslation()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[] { "alpha", "omega" });

            Assert.AreEqual(NodePacketType.BuildRequest, request.Type);

            ((INodePacketTranslatable)request).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = BuildRequest.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            BuildRequest deserializedRequest = packet as BuildRequest;

            Assert.AreEqual(request.BuildEventContext, deserializedRequest.BuildEventContext);
            Assert.AreEqual(request.ConfigurationId, deserializedRequest.ConfigurationId);
            Assert.AreEqual(request.GlobalRequestId, deserializedRequest.GlobalRequestId);
            Assert.AreEqual(request.IsConfigurationResolved, deserializedRequest.IsConfigurationResolved);
            Assert.AreEqual(request.NodeRequestId, deserializedRequest.NodeRequestId);
            Assert.AreEqual(request.ParentBuildEventContext, deserializedRequest.ParentBuildEventContext);
            Assert.AreEqual(request.Targets.Count, deserializedRequest.Targets.Count);
            for (int i = 0; i < request.Targets.Count; i++)
            {
                Assert.AreEqual(request.Targets[i], deserializedRequest.Targets[i]);
            }
        }

        private BuildRequest CreateNewBuildRequest(int configurationId, string[] targets)
        {
            return new BuildRequest(1 /* submissionId */, _nodeRequestId++, configurationId, targets, null, BuildEventContext.Invalid, null);
        }
    }
}