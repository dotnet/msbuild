// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Shouldly;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    [TestClass]
    public class BuildRequest_Tests
    {
        private int _nodeRequestId;

        public BuildRequest_Tests()
        {
            _nodeRequestId = 1;
        }

        [MSBuildTestMethod]
        public void TestConstructorBad()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
            {
                CreateNewBuildRequest(0, null);
            });
        }
        [MSBuildTestMethod]
        public void TestConstructorGood()
        {
            CreateNewBuildRequest(0, Array.Empty<string>());
        }

        [MSBuildTestMethod]
        public void TestConfigurationId()
        {
            BuildRequest request = CreateNewBuildRequest(0, Array.Empty<string>());
            Assert.AreEqual(0, request.ConfigurationId);

            BuildRequest request2 = CreateNewBuildRequest(1, Array.Empty<string>());
            Assert.AreEqual(1, request2.ConfigurationId);

            BuildRequest request3 = CreateNewBuildRequest(-1, Array.Empty<string>());
            Assert.AreEqual(-1, request3.ConfigurationId);
        }

        [MSBuildTestMethod]
        public void TestConfigurationResolved()
        {
            BuildRequest request = CreateNewBuildRequest(0, Array.Empty<string>());
            Assert.IsFalse(request.IsConfigurationResolved);

            BuildRequest request2 = CreateNewBuildRequest(1, Array.Empty<string>());
            Assert.IsTrue(request2.IsConfigurationResolved);

            BuildRequest request3 = CreateNewBuildRequest(-1, Array.Empty<string>());
            Assert.IsFalse(request3.IsConfigurationResolved);
        }

        [MSBuildTestMethod]
        public void TestTargets()
        {
            BuildRequest request = CreateNewBuildRequest(0, Array.Empty<string>());
            Assert.IsNotNull(request.Targets);
            Assert.IsEmpty(request.Targets);

            BuildRequest request2 = CreateNewBuildRequest(1, new string[1] { "a" });
            Assert.IsNotNull(request2.Targets);
            Assert.ContainsSingle(request2.Targets);
            Assert.AreEqual("a", request2.Targets[0]);
        }

        [MSBuildTestMethod]
        public void TestPacketType()
        {
            BuildRequest request = CreateNewBuildRequest(0, Array.Empty<string>());
            Assert.AreEqual(NodePacketType.BuildRequest, request.Type);
        }

        [MSBuildTestMethod]
        public void TestResolveConfigurationGood()
        {
            BuildRequest request = CreateNewBuildRequest(0, Array.Empty<string>());
            request.ResolveConfiguration(1);
            Assert.IsTrue(request.IsConfigurationResolved);
            Assert.AreEqual(1, request.ConfigurationId);
        }

        [MSBuildTestMethod]
        public void TestResolveConfigurationBad()
        {
            Assert.ThrowsExactly<InternalErrorException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
                request.ResolveConfiguration(2);
            });
        }

        [MSBuildTestMethod]
        public void TestResolveConfigurationBad2()
        {
            Assert.ThrowsExactly<InternalErrorException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(0, Array.Empty<string>());
                request.ResolveConfiguration(-1);
            });
        }
        [MSBuildTestMethod]
        public void TestTranslation()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[] { "alpha", "omega" });

            Assert.AreEqual(NodePacketType.BuildRequest, request.Type);

            ((ITranslatable)request).Translate(TranslationHelpers.GetWriteTranslator());
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

        [WindowsOnlyFact]
        [SupportedOSPlatform("windows5.0")]
        public void TestRunningObjectTableErrorLogging()
        {
            var rot = new RunningObjectTable();
            var nonExistentMoniker = "NonExistent_" + Guid.NewGuid();

            var exception = Should.Throw<COMException>(() => rot.GetObject(nonExistentMoniker));

            exception.Message.ShouldContain($"Failed to get object '{nonExistentMoniker}' from Running Object Table");
            exception.Message.ShouldContain("HRESULT:");
            exception.HResult.ShouldNotBe(0);
        }

        [WindowsOnlyFact]
        [SupportedOSPlatform("windows5.0")]
        public void TestRunningObjectTableErrorDoesNotMaskOriginalError()
        {
            var rot = new RunningObjectTable();
            var testMoniker = "ErrorTest_" + Guid.NewGuid();

            var exception = Should.Throw<COMException>(() => rot.GetObject(testMoniker));

            exception.ShouldBeOfType<COMException>();
            exception.HResult.ShouldNotBe(0);

            exception.Message.ShouldContain(testMoniker);
        }

        [WindowsOnlyFact]
        [SupportedOSPlatform("windows5.0")]
        public void TestRunningObjectTableSuccessDoesNotThrow()
        {
            var stateInHostObject = 42;
            var hostServices = new HostServices();
            var rot = new MockRunningObjectTable();
            hostServices.SetTestRunningObjectTable(rot);

            var moniker = nameof(TestRunningObjectTableSuccessDoesNotThrow) + Guid.NewGuid();
            var remoteHost = new MockRemoteHostObject(stateInHostObject);

            using (var result = rot.Register(moniker, remoteHost))
            {
                // This should succeed without throwing - validates error logging doesn't affect success path
                var retrievedObject = Should.NotThrow(() => rot.GetObject(moniker));

                retrievedObject.ShouldNotBeNull();
                retrievedObject.ShouldBeOfType<MockRemoteHostObject>();
                ((MockRemoteHostObject)retrievedObject).GetState().ShouldBe(stateInHostObject);
            }
        }

        [WindowsOnlyFact]
        [SupportedOSPlatform("windows5.0")]
        public void TestRunningObjectTableErrorMessageIsMultiLine()
        {
            var rot = new RunningObjectTable();
            var testMoniker = "MultiLineTest_" + Guid.NewGuid();

            var exception = Should.Throw<COMException>(() => rot.GetObject(testMoniker));

            // The error message should have at least 2 lines:
            // 1. "Failed to get object..." 
            // 2. "HRESULT: ..."
            var lines = exception.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            lines.Length.ShouldBeGreaterThanOrEqualTo(2);

            // First line should mention the failure
            lines[0].ShouldContain("Failed to get object");
            lines[0].ShouldContain(testMoniker);

            // Second line should have HRESULT
            lines[1].ShouldContain("HRESULT:");
        }

        [WindowsOnlyFact]
        [SupportedOSPlatform("windows5.0")]
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

        [MSBuildTestMethod]
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
