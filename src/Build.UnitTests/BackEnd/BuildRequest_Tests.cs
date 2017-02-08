// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Xunit;



namespace Microsoft.Build.UnitTests.BackEnd
{
    public class BuildRequest_Tests
    {
        private int _nodeRequestId;

        public BuildRequest_Tests()
        {
            _nodeRequestId = 1;
        }

        [Fact]
        public void TestConstructorBad()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(0, null);
            }
           );
        }
        [Fact]
        public void TestConstructorGood()
        {
            BuildRequest request = CreateNewBuildRequest(0, new string[0] { });
        }

        [Fact]
        public void TestConfigurationId()
        {
            BuildRequest request = CreateNewBuildRequest(0, new string[0] { });
            Assert.Equal(0, request.ConfigurationId);

            BuildRequest request2 = CreateNewBuildRequest(1, new string[0] { });
            Assert.Equal(1, request2.ConfigurationId);

            BuildRequest request3 = CreateNewBuildRequest(-1, new string[0] { });
            Assert.Equal(-1, request3.ConfigurationId);
        }

        [Fact]
        public void TestConfigurationResolved()
        {
            BuildRequest request = CreateNewBuildRequest(0, new string[0] { });
            Assert.False(request.IsConfigurationResolved);

            BuildRequest request2 = CreateNewBuildRequest(1, new string[0] { });
            Assert.True(request2.IsConfigurationResolved);

            BuildRequest request3 = CreateNewBuildRequest(-1, new string[0] { });
            Assert.False(request3.IsConfigurationResolved);
        }

        [Fact]
        public void TestTargets()
        {
            BuildRequest request = CreateNewBuildRequest(0, new string[0] { });
            Assert.NotNull(request.Targets);
            Assert.Equal(0, request.Targets.Count);

            BuildRequest request2 = CreateNewBuildRequest(1, new string[1] { "a" });
            Assert.NotNull(request2.Targets);
            Assert.Equal(1, request2.Targets.Count);
            Assert.Equal("a", request2.Targets[0]);
        }

        [Fact]
        public void TestPacketType()
        {
            BuildRequest request = CreateNewBuildRequest(0, new string[0] { });
            Assert.Equal(NodePacketType.BuildRequest, request.Type);
        }

        [Fact]
        public void TestResolveConfigurationGood()
        {
            BuildRequest request = CreateNewBuildRequest(0, new string[0] { });
            request.ResolveConfiguration(1);
            Assert.True(request.IsConfigurationResolved);
            Assert.Equal(1, request.ConfigurationId);
        }

        [Fact]
        public void TestResolveConfigurationBad()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, new string[0] { });
                request.ResolveConfiguration(2);
            }
           );
        }

        [Fact]
        public void TestResolveConfigurationBad2()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(0, new string[0] { });
                request.ResolveConfiguration(-1);
            }
           );
        }
        [Fact]
        public void TestTranslation()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[] { "alpha", "omega" });

            Assert.Equal(NodePacketType.BuildRequest, request.Type);

            ((INodePacketTranslatable)request).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = BuildRequest.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            BuildRequest deserializedRequest = packet as BuildRequest;

            Assert.Equal(request.BuildEventContext, deserializedRequest.BuildEventContext);
            Assert.Equal(request.ConfigurationId, deserializedRequest.ConfigurationId);
            Assert.Equal(request.GlobalRequestId, deserializedRequest.GlobalRequestId);
            Assert.Equal(request.IsConfigurationResolved, deserializedRequest.IsConfigurationResolved);
            Assert.Equal(request.NodeRequestId, deserializedRequest.NodeRequestId);
            Assert.Equal(request.ParentBuildEventContext, deserializedRequest.ParentBuildEventContext);
            Assert.Equal(request.Targets.Count, deserializedRequest.Targets.Count);
            for (int i = 0; i < request.Targets.Count; i++)
            {
                Assert.Equal(request.Targets[i], deserializedRequest.Targets[i]);
            }
        }

        private BuildRequest CreateNewBuildRequest(int configurationId, string[] targets)
        {
            return new BuildRequest(1 /* submissionId */, _nodeRequestId++, configurationId, targets, null, BuildEventContext.Invalid, null);
        }
    }
}