// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Logging;
using Microsoft.Build.UnitTests.BackEnd;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    public class NodeConfiguration_Tests
    {
#if FEATURE_APPDOMAIN
        /// <summary>
        /// Test serialization / deserialization of the AppDomainSetup instance.
        /// </summary>
        [Theory]
        [InlineData(new byte[] { 1, 2, 3 })]
        [InlineData(null)]
        public void TestTranslationWithAppDomainSetup(byte[]? configBytes)
        {
            AppDomainSetup setup = new AppDomainSetup();

            NodeConfiguration config = new NodeConfiguration(
                nodeId: 1,
                buildParameters: new BuildParameters(),
                forwardingLoggers: Array.Empty<LoggerDescription>(),
                appDomainSetup: setup,
                loggingNodeConfiguration: new LoggingNodeConfiguration());

            setup.SetConfigurationBytes(configBytes);

            ((ITranslatable)config).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = NodeConfiguration.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            packet.ShouldBeOfType<NodeConfiguration>();
            NodeConfiguration deserializedConfig = (NodeConfiguration)packet;

            deserializedConfig.AppDomainSetup.ShouldNotBeNull();

            if (configBytes is null)
            {
                deserializedConfig.AppDomainSetup.GetConfigurationBytes().ShouldBeNull();
            }
            else
            {
                deserializedConfig.AppDomainSetup.GetConfigurationBytes().SequenceEqual(configBytes).ShouldBeTrue();
            }
        }

        /// <summary>
        /// A configuration without an AppDomainSetup, as BuildManager creates on a runtime without
        /// AppDomains, is written without configuration bytes, and on .NET Framework the node that
        /// reads it gets an AppDomainSetup without configuration bytes.
        /// </summary>
        [Fact]
        public void TestTranslationWithoutAppDomainSetup()
        {
            NodeConfiguration config = new NodeConfiguration(
                nodeId: 1,
                buildParameters: new BuildParameters(),
                forwardingLoggers: Array.Empty<LoggerDescription>(),
                appDomainSetup: null,
                loggingNodeConfiguration: new LoggingNodeConfiguration());

            ((ITranslatable)config).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = NodeConfiguration.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            NodeConfiguration deserializedConfig = packet.ShouldBeOfType<NodeConfiguration>();
            deserializedConfig.AppDomainSetup.ShouldNotBeNull();
            deserializedConfig.AppDomainSetup.GetConfigurationBytes().ShouldBeNull();
        }
#endif
    }
}
