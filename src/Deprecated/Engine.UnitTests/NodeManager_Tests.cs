// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Reflection;
using System.Collections;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using System.Text.RegularExpressions;
using System.Xml;
using System.Collections.Generic;

namespace Microsoft.Build.UnitTests
{

    [TestFixture]
    public class NodeManager_Tests
    {
        private Engine engine = new Engine(@"c:\");
        [Test]
        public void TestConstructor()
        {
            NodeManager nodeManager = new NodeManager(1, false, engine);
            Assert.IsTrue(nodeManager.TaskExecutionModule.GetExecutionModuleMode() == TaskExecutionModule.TaskExecutionModuleMode.SingleProcMode, "Expected Task Mode to be Single");
            nodeManager.UpdateSettings(true, true, true);
        }

        [Test]
        public void TestConstructor2()
        {
            NodeManager nodeManager = new NodeManager(1, true, engine);
            Assert.IsTrue(nodeManager.TaskExecutionModule.GetExecutionModuleMode() == TaskExecutionModule.TaskExecutionModuleMode.MultiProcFullNodeMode, "Expected Task Mode to be SingleProc");
        }

        [Test]
        public void TestConstructor3()
        {

            NodeManager nodeManager = new NodeManager(4, true, engine);
            Assert.IsTrue(nodeManager.TaskExecutionModule.GetExecutionModuleMode() == TaskExecutionModule.TaskExecutionModuleMode.MultiProcFullNodeMode, "Expected Task Mode to be MultiProc");
        }

        [Test]
        public void TestConstructor4()
        {

            NodeManager nodeManager = new NodeManager(4, false, engine);
            Assert.IsTrue(nodeManager.TaskExecutionModule.GetExecutionModuleMode() == TaskExecutionModule.TaskExecutionModuleMode.MultiProcFullNodeMode, "Expected Task Mode to be MultiProc");
        }



        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void RegisterNullNodeProviders()
        {
             MockNodeProvider nullNodeProvider = null;
             NodeManager nodeManager = new NodeManager(1, false, engine);
             nodeManager.RegisterNodeProvider(nullNodeProvider);
        }

        [Test]
        public void RegisterNodeProviders()
        {
                      
            MockNodeProvider ProviderOneNode = new MockNodeProvider();
            ProviderOneNode.NodeDescriptions.Add(new MockNodeDescription("Provider One Node One"));
           
            MockNodeProvider ProviderThreeNodes = new MockNodeProvider();
            ProviderThreeNodes.NodeDescriptions.Add(new MockNodeDescription("Provider Two Node One"));
            ProviderThreeNodes.NodeDescriptions.Add(new MockNodeDescription("Provider Two Node Two"));
            ProviderThreeNodes.NodeDescriptions.Add(new MockNodeDescription("Provider Two Node Three"));

            MockNodeProvider ProviderNoNodes = new MockNodeProvider();

            // Register a node provider with only one node
            NodeManager nodeManager = new NodeManager(1, false, new Engine(@"c:\"));
            nodeManager.RegisterNodeProvider(ProviderOneNode);
            // One from node added by node provider, one for the default 0 local node (null as there is no description)
            Assert.IsTrue(nodeManager.GetNodeDescriptions().Length == 2, "Expected there to be two node Descriptions");
            Assert.AreEqual(2, nodeManager.MaxNodeCount);
            Assert.IsNull(nodeManager.GetNodeDescriptions()[0],"Expected first element to be null");
            Assert.IsTrue(string.Compare(((MockNodeDescription)nodeManager.GetNodeDescriptions()[1]).NodeDescription, "Provider One Node One", StringComparison.OrdinalIgnoreCase)==0, "Expected node description to be Provider One  Node One");

            // Register a node provider with more than one node
            nodeManager = new NodeManager(1, false, new Engine(@"c:\"));
            nodeManager.RegisterNodeProvider(ProviderThreeNodes);
            // THree from node added by node provider, one for the default 0 local node (null as there is no description)
            Assert.IsTrue(nodeManager.GetNodeDescriptions().Length == 4, "Expected there to be four node Descriptions");
            Assert.AreEqual(4, nodeManager.MaxNodeCount);
            Assert.IsNull(nodeManager.GetNodeDescriptions()[0], "Expected first element to be null");
            Assert.IsTrue(string.Compare(((MockNodeDescription)nodeManager.GetNodeDescriptions()[1]).NodeDescription, "Provider Two Node One", StringComparison.OrdinalIgnoreCase) == 0, "Expected node description to be Provider Two Node One");
            Assert.IsTrue(string.Compare(((MockNodeDescription)nodeManager.GetNodeDescriptions()[2]).NodeDescription, "Provider Two Node Two", StringComparison.OrdinalIgnoreCase) == 0, "Expected node description to be Provider Two Node Two");
            Assert.IsTrue(string.Compare(((MockNodeDescription)nodeManager.GetNodeDescriptions()[3]).NodeDescription, "Provider Two Node Three", StringComparison.OrdinalIgnoreCase) == 0, "Expected node description to be Provider Two Node Three");


            // Register a node provider with more than one node
            nodeManager = new NodeManager(1, false, new Engine(@"c:\"));
            nodeManager.RegisterNodeProvider(ProviderOneNode);
            nodeManager.RegisterNodeProvider(ProviderThreeNodes);
            // THree from node added by node provider, one for the default 0 local node (null as there is no description)
            Assert.IsTrue(nodeManager.GetNodeDescriptions().Length == 5, "Expected there to be four node Descriptions");
            Assert.AreEqual(5, nodeManager.MaxNodeCount);
            Assert.IsNull(nodeManager.GetNodeDescriptions()[0], "Expected first element to be null");
            Assert.IsTrue(string.Compare(((MockNodeDescription)nodeManager.GetNodeDescriptions()[1]).NodeDescription, "Provider One Node One", StringComparison.OrdinalIgnoreCase) == 0, "Expected node description to be Provider One Node One");
            Assert.IsTrue(string.Compare(((MockNodeDescription)nodeManager.GetNodeDescriptions()[2]).NodeDescription, "Provider Two Node One", StringComparison.OrdinalIgnoreCase) == 0, "Expected node description to be Provider Two Node One");
            Assert.IsTrue(string.Compare(((MockNodeDescription)nodeManager.GetNodeDescriptions()[3]).NodeDescription, "Provider Two Node Two", StringComparison.OrdinalIgnoreCase) == 0, "Expected node description to be Provider Two Node Two");
            Assert.IsTrue(string.Compare(((MockNodeDescription)nodeManager.GetNodeDescriptions()[4]).NodeDescription, "Provider Two Node Three", StringComparison.OrdinalIgnoreCase) == 0, "Expected node description to be Provider Two Node Three");


            // Register a node provider with more than one node
            nodeManager = new NodeManager(1, false, new Engine(@"c:\"));
            nodeManager.RegisterNodeProvider(ProviderThreeNodes);
            nodeManager.RegisterNodeProvider(ProviderOneNode);
            nodeManager.UpdateSettings(true, false, true); // just need to test this once
            // THree from node added by node provider, one for the default 0 local node (null as there is no description)
            Assert.IsTrue(nodeManager.GetNodeDescriptions().Length == 5, "Expected there to be four node Descriptions");
            Assert.AreEqual(5, nodeManager.MaxNodeCount);
            Assert.IsNull(nodeManager.GetNodeDescriptions()[0], "Expected first element to be null");
            Assert.IsTrue(string.Compare(((MockNodeDescription)nodeManager.GetNodeDescriptions()[1]).NodeDescription, "Provider Two Node One", StringComparison.OrdinalIgnoreCase) == 0, "Expected node description to be Provider Two Node One");
            Assert.IsTrue(string.Compare(((MockNodeDescription)nodeManager.GetNodeDescriptions()[2]).NodeDescription, "Provider Two Node Two", StringComparison.OrdinalIgnoreCase) == 0, "Expected node description to be Provider Two Node Two");
            Assert.IsTrue(string.Compare(((MockNodeDescription)nodeManager.GetNodeDescriptions()[3]).NodeDescription, "Provider Two Node Three", StringComparison.OrdinalIgnoreCase) == 0, "Expected node description to be Provider Two Node Three");
            Assert.IsTrue(string.Compare(((MockNodeDescription)nodeManager.GetNodeDescriptions()[4]).NodeDescription, "Provider One Node One", StringComparison.OrdinalIgnoreCase) == 0, "Expected node description to be Provider One Node One");
        }

        [Test]
        public void TestEnableOutOfProcLogging()
        {
            // Register a node provider with more than one node
            MockNodeProvider ProviderOneNode = new MockNodeProvider();
            ProviderOneNode.NodeDescriptions.Add(new MockNodeDescription("Provider One Node One"));
            NodeManager nodeManager = new NodeManager(1, false, new Engine(@"c:\"));
            nodeManager.RegisterNodeProvider(ProviderOneNode);
            nodeManager.UpdateSettings(true,false, true); // just need to test this once
        }

        [Test]
        public void TestShutdownNodes()
        {
            MockNodeProvider ProviderThreeNodes = new MockNodeProvider();
            ProviderThreeNodes.NodeDescriptions.Add(new MockNodeDescription("Provider Two Node One"));
            ProviderThreeNodes.NodeDescriptions.Add(new MockNodeDescription("Provider Two Node Two"));
            ProviderThreeNodes.NodeDescriptions.Add(new MockNodeDescription("Provider Two Node Three"));

            NodeManager nodeManager = new NodeManager(1, false, new Engine(@"c:\"));
            nodeManager.RegisterNodeProvider(ProviderThreeNodes);
            nodeManager.ShutdownNodes(Node.NodeShutdownLevel.PoliteShutdown);

            Assert.IsTrue(ProviderThreeNodes.NodeDescriptions.TrueForAll(delegate(INodeDescription o)
                                                                                                     {
                                                                                                         return o == null;
                                                                                                     }
                                                                         ), "Expected all descriptions to be null");

        }

        [Test]
        public void TestPostBuildResultToNode()
        {
            MockNodeProvider ProviderThreeNodes = new MockNodeProvider();
            ProviderThreeNodes.NodeDescriptions.Add(new MockNodeDescription("Provider Two Node One"));
            ProviderThreeNodes.NodeDescriptions.Add(new MockNodeDescription("Provider Two Node Two"));
            ProviderThreeNodes.NodeDescriptions.Add(new MockNodeDescription("Provider Two Node Three"));

            MockNodeProvider ProviderOneNode = new MockNodeProvider();
            ProviderOneNode.NodeDescriptions.Add(new MockNodeDescription("Provider One Node One"));

            NodeManager nodeManager = new NodeManager(1, false, new Engine(@"c:\"));
            nodeManager.RegisterNodeProvider(ProviderThreeNodes);
            nodeManager.RegisterNodeProvider(ProviderOneNode);

            nodeManager.PostBuildResultToNode(1, new BuildResult(null, new Hashtable(StringComparer.OrdinalIgnoreCase), false, 2, 1, 6, false, string.Empty, string.Empty, 0, 0, 0));
            nodeManager.PostBuildResultToNode(2, new BuildResult(null, new Hashtable(StringComparer.OrdinalIgnoreCase), false, 3, 2, 7, false, string.Empty, string.Empty, 0, 0, 0));
            nodeManager.PostBuildResultToNode(3, new BuildResult(null, new Hashtable(StringComparer.OrdinalIgnoreCase), false, 4, 3, 8, false, string.Empty, string.Empty, 0, 0, 0));
            nodeManager.PostBuildResultToNode(4, new BuildResult(null, new Hashtable(StringComparer.OrdinalIgnoreCase), false, 5, 4, 9, false, string.Empty, string.Empty, 0, 0, 0));
            Assert.IsTrue(ProviderThreeNodes.buildResultsSubmittedToProvider.Count == 3, "Expected there to be three build results in the mock provider");
            Assert.IsTrue(ProviderThreeNodes.buildResultsSubmittedToProvider[0].HandleId == 2, "Expected first NodeProxyId to be 2");
            Assert.IsTrue(ProviderThreeNodes.buildResultsSubmittedToProvider[1].HandleId == 3, "Expected second NodeProxyId to be 3");
            Assert.IsTrue(ProviderThreeNodes.buildResultsSubmittedToProvider[2].HandleId == 4, "Expected third NodeProxyId to be 4");
            Assert.IsTrue(ProviderOneNode.buildResultsSubmittedToProvider.Count == 1, "Expected there to be one build results in the mock provider");
            Assert.IsTrue(ProviderOneNode.buildResultsSubmittedToProvider[0].HandleId == 5, "Expected first NodeProxyId to be 5");
        }

        [Test]
        public void TestPostBuildRequestToNode()
        {
            MockNodeProvider ProviderThreeNodes = new MockNodeProvider();
            ProviderThreeNodes.NodeDescriptions.Add(new MockNodeDescription("Provider Two Node One"));
            ProviderThreeNodes.NodeDescriptions.Add(new MockNodeDescription("Provider Two Node Two"));
            ProviderThreeNodes.NodeDescriptions.Add(new MockNodeDescription("Provider Two Node Three"));

            MockNodeProvider ProviderOneNode = new MockNodeProvider();
            ProviderOneNode.NodeDescriptions.Add(new MockNodeDescription("Provider One Node One"));

            NodeManager nodeManager = new NodeManager(1, false, new Engine(@"c:\"));
            nodeManager.RegisterNodeProvider(ProviderThreeNodes);
            nodeManager.RegisterNodeProvider(ProviderOneNode);

            nodeManager.PostBuildRequestToNode(1, new BuildRequest(1, "ProjectFile", null, new BuildPropertyGroup(), null, 1, false, false));
            nodeManager.PostBuildRequestToNode(2, new BuildRequest(2, "ProjectFile", null, new BuildPropertyGroup(), null, 2, false, false));
            nodeManager.PostBuildRequestToNode(3, new BuildRequest(3, "ProjectFile", null, new BuildPropertyGroup(), null, 3, false, false));
            nodeManager.PostBuildRequestToNode(4, new BuildRequest(4, "ProjectFile", null, new BuildPropertyGroup(), null, 4, false, false));
            
            Assert.IsTrue(ProviderThreeNodes.buildRequestsSubmittedToProvider.Count == 3, "Expected there to be three build results in the mock provider");
            Assert.IsTrue(ProviderThreeNodes.buildRequestsSubmittedToProvider[0].HandleId == 1, "Expected first NodeProxyId to be 1");
            Assert.IsTrue(ProviderThreeNodes.buildRequestsSubmittedToProvider[1].HandleId == 2, "Expected second NodeProxyId to be 2");
            Assert.IsTrue(ProviderThreeNodes.buildRequestsSubmittedToProvider[2].HandleId == 3, "Expected third NodeProxyId to be 3");
            Assert.IsTrue(ProviderOneNode.buildRequestsSubmittedToProvider.Count == 1, "Expected there to be one build results in the mock provider");
            Assert.IsTrue(ProviderOneNode.buildRequestsSubmittedToProvider[0].HandleId == 4, "Expected first NodeProxyId to be 4");
        }


        [Test]
        public void TestGetNodeDescriptions()
        {
            MockNodeProvider ProviderThreeNodes = new MockNodeProvider();
            ProviderThreeNodes.NodeDescriptions.Add(new MockNodeDescription("Provider Two Node One"));
            ProviderThreeNodes.NodeDescriptions.Add(new MockNodeDescription("Provider Two Node Two"));
            ProviderThreeNodes.NodeDescriptions.Add(new MockNodeDescription("Provider Two Node Three"));

            MockNodeProvider ProviderOneNode = new MockNodeProvider();
            ProviderOneNode.NodeDescriptions.Add(new MockNodeDescription("Provider One Node One"));

            NodeManager nodeManager = new NodeManager(1, false, new Engine(@"c:\"));
            nodeManager.RegisterNodeProvider(ProviderThreeNodes);
            nodeManager.RegisterNodeProvider(ProviderOneNode);
            // Cant assert the contents yet as there is no definition inside of a INodeDescription interface
            Assert.IsTrue(nodeManager.GetNodeDescriptions().Length == 5, "Expected there to be five descriptions");
        }
    }

    /// <summary>
    /// Dont know what node description is, so I just set it to something for now
    /// </summary>
    internal class MockNodeDescription:INodeDescription
    {
        string nodeDescription;

        public string NodeDescription
        {
            get { return nodeDescription; }
        }
        internal MockNodeDescription(string description)
        {
            nodeDescription = description;
        }
    }

    internal class MockNodeProvider:INodeProvider
    {
        string initConfiguration;
        IEngineCallback initEngineCallback;
        List<INodeDescription> nodeDescriptions;
        BuildPropertyGroup parentGlobalProperties;
        ToolsetDefinitionLocations toolsetSearchLocations;
        string startDirectory;

        internal List<INodeDescription> NodeDescriptions
        {
            get { return nodeDescriptions; }
            set { nodeDescriptions = value; }
        }
        internal List<BuildRequest> buildRequestsSubmittedToProvider;
        internal List<BuildResult> buildResultsSubmittedToProvider;
        #region INodeProvider Members

        internal MockNodeProvider()
        {
            nodeDescriptions = new List<INodeDescription>();
            buildRequestsSubmittedToProvider = new List<BuildRequest>();
            buildResultsSubmittedToProvider = new List<BuildResult>();
        }
        void INodeProvider.Initialize(string configuration, IEngineCallback engineCallback, BuildPropertyGroup parentGlobalProperties,
                       ToolsetDefinitionLocations toolsetSearchLocations, string startDirectory)
        {
            this.initConfiguration = configuration;
            this.initEngineCallback = engineCallback;
            this.parentGlobalProperties = parentGlobalProperties;
            this.toolsetSearchLocations = toolsetSearchLocations;
            this.startDirectory = startDirectory;
        }

        INodeDescription[] INodeProvider.QueryNodeDescriptions()
        {
            return nodeDescriptions.ToArray();
        }

        void INodeProvider.RegisterNodeLogger(LoggerDescription description )
        {
            if ( description == null )
            {
                throw new ArgumentException("Logger description should be non-null");
            } 
        }

        void INodeProvider.PostBuildRequestToNode(int nodeIndex, BuildRequest buildRequest)
        {
            if (nodeIndex > nodeDescriptions.Count)
            {
                throw new ArgumentException("Node index is out of range");
            }
            buildRequestsSubmittedToProvider.Add(buildRequest);
        }

        void INodeProvider.PostBuildResultToNode(int nodeIndex, BuildResult buildResultToPost)
        {
            if (nodeIndex > nodeDescriptions.Count)
            {
                throw new ArgumentException("Node index is out of range");
            }
            buildResultsSubmittedToProvider.Add(buildResultToPost);
        }

        void INodeProvider.ShutdownNodes(Node.NodeShutdownLevel nodeShutdownLevel)
        {
            for (int i = 0; i < NodeDescriptions.Count; i++)
            {
                NodeDescriptions[i] = null;
            }
        }


        #endregion

        #region INodeProvider Members


        public void UpdateSettings(bool enableOutOfProcLogging, bool enableOnlyLogCriticalEvents, bool useBreadthFirstTraversalSetting)
        {
          
        }

        #endregion

        #region INodeProvider Members


        public void AssignNodeIdentifiers(int[] nodeIdentifiers)
        {
           
        }

        public void RequestNodeStatus(int nodeIndex, int requestId)
        {
        }

       public void PostIntrospectorCommand(int nodeIndex, TargetInProgessState child, TargetInProgessState parent)
       {
       }

        #endregion
    }

}
