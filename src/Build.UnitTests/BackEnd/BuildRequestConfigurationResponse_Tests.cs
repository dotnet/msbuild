// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for the BuildRequestConfigurationResponse class.
    /// </summary>
    public class BuildRequestConfigurationResponse_Tests
    {
        /// <summary>
        /// Validate the constructor takes any combination of arguments.  It is not the purpose of this class to enforce
        /// rules on configuration IDs.
        /// </summary>
        [Fact]
        public void TestConstructor()
        {
            BuildRequestConfigurationResponse response = new BuildRequestConfigurationResponse(0, 0, 0);
            BuildRequestConfigurationResponse response2 = new BuildRequestConfigurationResponse(0, 1, 0);
            BuildRequestConfigurationResponse response3 = new BuildRequestConfigurationResponse(0, -1, 0);
            BuildRequestConfigurationResponse response4 = new BuildRequestConfigurationResponse(1, 0, 0);
            BuildRequestConfigurationResponse response5 = new BuildRequestConfigurationResponse(1, 1, 0);
            BuildRequestConfigurationResponse response6 = new BuildRequestConfigurationResponse(1, -1, 0);
            BuildRequestConfigurationResponse response7 = new BuildRequestConfigurationResponse(-1, 0, 0);
            BuildRequestConfigurationResponse response8 = new BuildRequestConfigurationResponse(-1, 1, 0);
            BuildRequestConfigurationResponse response9 = new BuildRequestConfigurationResponse(-1, -1, 0);
            BuildRequestConfigurationResponse response10 = new BuildRequestConfigurationResponse(0, 0, 1);
            BuildRequestConfigurationResponse response11 = new BuildRequestConfigurationResponse(0, 1, 0);
            BuildRequestConfigurationResponse response12 = new BuildRequestConfigurationResponse(0, -1, -1);
            BuildRequestConfigurationResponse response13 = new BuildRequestConfigurationResponse(1, 0, 1);
            BuildRequestConfigurationResponse response14 = new BuildRequestConfigurationResponse(1, 1, 0);
            BuildRequestConfigurationResponse response15 = new BuildRequestConfigurationResponse(1, -1, -1);
            BuildRequestConfigurationResponse response16 = new BuildRequestConfigurationResponse(-1, 0, 1);
            BuildRequestConfigurationResponse response17 = new BuildRequestConfigurationResponse(-1, 1, 0);
            BuildRequestConfigurationResponse response18 = new BuildRequestConfigurationResponse(-1, -1, -1);
        }

        /// <summary>
        /// Test the NodeConfigurationId property
        /// </summary>
        [Fact]
        public void TestNodeConfigurationId()
        {
            BuildRequestConfigurationResponse response = new BuildRequestConfigurationResponse(1, 0, 0);
            Assert.Equal(1, response.NodeConfigurationId);
        }

        /// <summary>
        /// Test the GlobalConfigurationId property
        /// </summary>
        [Fact]
        public void TestGlobalConfigurationId()
        {
            BuildRequestConfigurationResponse response = new BuildRequestConfigurationResponse(0, 1, 0);
            Assert.Equal(1, response.GlobalConfigurationId);
        }

        /// <summary>
        /// Test the ResultsNodeId property
        /// </summary>
        [Fact]
        public void TestResultsNodeId()
        {
            BuildRequestConfigurationResponse response = new BuildRequestConfigurationResponse(0, 1, 2);
            Assert.Equal(2, response.ResultsNodeId);
        }

        /// <summary>
        /// Test the Serialize method
        /// </summary>
        [Fact]
        public void TestTranslation()
        {
            BuildRequestConfigurationResponse response = new BuildRequestConfigurationResponse(1, 2, 3);
            Assert.Equal(NodePacketType.BuildRequestConfigurationResponse, response.Type);

            ((ITranslatable)response).Translate(TranslationHelpers.GetWriteTranslator());

            INodePacket deserializedPacket = BuildRequestConfigurationResponse.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());
            BuildRequestConfigurationResponse deserializedResponse = deserializedPacket as BuildRequestConfigurationResponse;
            Assert.Equal(response.NodeConfigurationId, deserializedResponse.NodeConfigurationId);
            Assert.Equal(response.GlobalConfigurationId, deserializedResponse.GlobalConfigurationId);
            Assert.Equal(response.ResultsNodeId, deserializedResponse.ResultsNodeId);
        }
    }
}
