// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Versioning;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;

#nullable disable

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
                CreateNewBuildRequest(0, null);
            });
        }
        [Fact]
        public void TestConstructorGood()
        {
            CreateNewBuildRequest(0, Array.Empty<string>());
        }

        [Fact]
        public void TestConfigurationId()
        {
            BuildRequest request = CreateNewBuildRequest(0, Array.Empty<string>());
            Assert.Equal(0, request.ConfigurationId);

            BuildRequest request2 = CreateNewBuildRequest(1, Array.Empty<string>());
            Assert.Equal(1, request2.ConfigurationId);

            BuildRequest request3 = CreateNewBuildRequest(-1, Array.Empty<string>());
            Assert.Equal(-1, request3.ConfigurationId);
        }

        [Fact]
        public void TestConfigurationResolved()
        {
            BuildRequest request = CreateNewBuildRequest(0, Array.Empty<string>());
            Assert.False(request.IsConfigurationResolved);

            BuildRequest request2 = CreateNewBuildRequest(1, Array.Empty<string>());
            Assert.True(request2.IsConfigurationResolved);

            BuildRequest request3 = CreateNewBuildRequest(-1, Array.Empty<string>());
            Assert.False(request3.IsConfigurationResolved);
        }

        [Fact]
        public void TestTargets()
        {
            BuildRequest request = CreateNewBuildRequest(0, Array.Empty<string>());
            Assert.NotNull(request.Targets);
            Assert.Empty(request.Targets);

            BuildRequest request2 = CreateNewBuildRequest(1, new string[1] { "a" });
            Assert.NotNull(request2.Targets);
            Assert.Single(request2.Targets);
            Assert.Equal("a", request2.Targets[0]);
        }

        [Fact]
        public void TestPacketType()
        {
            BuildRequest request = CreateNewBuildRequest(0, Array.Empty<string>());
            Assert.Equal(NodePacketType.BuildRequest, request.Type);
        }

        [Fact]
        public void TestResolveConfigurationGood()
        {
            BuildRequest request = CreateNewBuildRequest(0, Array.Empty<string>());
            request.ResolveConfiguration(1);
            Assert.True(request.IsConfigurationResolved);
            Assert.Equal(1, request.ConfigurationId);
        }

        [Fact]
        public void TestResolveConfigurationBad()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
                request.ResolveConfiguration(2);
            });
        }

        [Fact]
        public void TestResolveConfigurationBad2()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(0, Array.Empty<string>());
                request.ResolveConfiguration(-1);
            });
        }
        [Fact]
        public void TestTranslation()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[] { "alpha", "omega" });

            Assert.Equal(NodePacketType.BuildRequest, request.Type);

            ((ITranslatable)request).Translate(TranslationHelpers.GetWriteTranslator());
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

        [WindowsOnlyFact]
        [SupportedOSPlatform("windows")]
        public void TestTranslationRemoteHostObjects()
        {
            var stateInHostObject = 3;

            var hostServices = new HostServices();
            var rot = new MockRunningObjectTable();
            hostServices.SetTestRunningObjectTable(rot);
            var moniker = nameof(TestTranslationRemoteHostObjects) + Guid.NewGuid();
            var remoteHost = new MockRemoteHostObject(stateInHostObject);
            using (var result = rot.Register(moniker, remoteHost))
            {
                hostServices.RegisterHostObject(
                    "WithOutOfProc.targets",
                    "DisplayMessages",
                    "ATask",
                    moniker);

                BuildRequest request = new BuildRequest(
                    submissionId: 1,
                    _nodeRequestId++,
                    1,
                    new string[] { "alpha", "omega" },
                    hostServices: hostServices,
                    BuildEventContext.Invalid,
                    parentRequest: null);

                ((ITranslatable)request).Translate(TranslationHelpers.GetWriteTranslator());
                INodePacket packet = BuildRequest.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

                BuildRequest deserializedRequest = packet as BuildRequest;
                deserializedRequest.HostServices.SetTestRunningObjectTable(rot);
                var hostObject = deserializedRequest.HostServices.GetHostObject(
                    "WithOutOfProc.targets",
                    "DisplayMessages",
                    "ATask") as ITestRemoteHostObject;

                hostObject.GetState().ShouldBe(stateInHostObject);
            }
        }

        [Fact]
        public void TestTranslationHostObjectsWhenEmpty()
        {
            var hostServices = new HostServices();
            BuildRequest request = new BuildRequest(
                submissionId: 1,
                _nodeRequestId++,
                1,
                new string[] { "alpha", "omega" },
                hostServices: hostServices,
                BuildEventContext.Invalid,
                parentRequest: null);

            ((ITranslatable)request).Translate(TranslationHelpers.GetWriteTranslator());
            BuildRequest.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());
        }

        private BuildRequest CreateNewBuildRequest(int configurationId, string[] targets)
        {
            return new BuildRequest(1 /* submissionId */, _nodeRequestId++, configurationId, targets, null, BuildEventContext.Invalid, null);
        }
    }
}
