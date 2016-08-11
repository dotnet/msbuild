// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Collections;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using NUnit.Framework;
using System.IO;

using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class BuildRequest_Tests
    {
        /// <summary>
        /// Test one of the constructors and some of the properties to make sure that they are set
        /// </summary>
        [Test]
        public void TestConstructor1andProperties()
        {

            int nodeProxyId = 1;
            string projectFileName = "ProjectFileName";
            string[] targetNames = new string[] { "Build" };
            BuildPropertyGroup globalProperties = null;
            int requestId = 1;

            BuildRequest firstConstructorRequest = new BuildRequest(nodeProxyId, projectFileName, targetNames, globalProperties, null, requestId, false, false);
            Assert.AreEqual(1, firstConstructorRequest.HandleId, "Expected firstConstructorRequest.NodeProxyId to be 1");
            firstConstructorRequest.HandleId = 2;
            Assert.AreEqual(2, firstConstructorRequest.HandleId, "Expected firstConstructorRequest.NodeProxyId to be 2");
            Assert.AreEqual(1, firstConstructorRequest.RequestId, "Expected firstConstructorRequest.RequestId to be 1");
            firstConstructorRequest.RequestId = 2;
            Assert.AreEqual(2, firstConstructorRequest.RequestId, "Expected firstConstructorRequest.RequestId to be 2");
            Assert.IsNull(firstConstructorRequest.GlobalProperties, "Expected firstConstructorRequest.GlobalProperties to be null");
            firstConstructorRequest.GlobalProperties = new BuildPropertyGroup();
            Assert.IsNotNull(firstConstructorRequest.GlobalProperties, "Expected firstConstructorRequest.GlobalProperties to not be null");
            Assert.IsTrue((firstConstructorRequest.TargetNames.Length == 1) && (string.Compare("Build", firstConstructorRequest.TargetNames[0], StringComparison.OrdinalIgnoreCase) == 0), "Expected to have one target with a value of Build in firstConstructorRequest.TargetNames");
            Assert.IsTrue(string.Compare("ProjectFileName", firstConstructorRequest.ProjectFileName, StringComparison.OrdinalIgnoreCase) == 0, "Expected firstConstructorRequest.ProjectFileName to be called ProjecFileName");

            globalProperties = new BuildPropertyGroup();
            BuildProperty propertyToAdd = new BuildProperty("PropertyName", "Value");
            globalProperties.SetProperty(propertyToAdd);

            firstConstructorRequest = new BuildRequest(nodeProxyId, projectFileName, targetNames, globalProperties, null, requestId, false, false);
            Assert.IsNotNull(firstConstructorRequest.GlobalPropertiesPassedByTask, "Expected GlobalPropertiesPassedByTask to not be null");
            Assert.IsNotNull(firstConstructorRequest.GlobalProperties, "Expected GlobalPropertiesPassedByTask to not be null");
            Assert.IsTrue(string.Compare(firstConstructorRequest.GlobalProperties["PropertyName"].Value, "Value", StringComparison.OrdinalIgnoreCase) == 0, "Expected GlobalProperties, propertyname to be equal to value");

            string buildProperty = ((Hashtable)firstConstructorRequest.GlobalPropertiesPassedByTask)["PropertyName"] as string;
            Assert.IsTrue(string.Compare(buildProperty, "Value", StringComparison.OrdinalIgnoreCase) == 0, "Expected hashtable to contain a property group with a value of value");
            Assert.IsTrue((firstConstructorRequest.TargetNames.Length == 1) && (string.Compare("Build", firstConstructorRequest.TargetNames[0], StringComparison.OrdinalIgnoreCase) == 0), "Expected to have one target with a value of Build");
            Assert.IsTrue(string.Compare("ProjectFileName", firstConstructorRequest.ProjectFileName, StringComparison.OrdinalIgnoreCase) == 0, "Expected project file to be called ProjecFileName");
      
        
        }

        /// <summary>
        /// Check the second constructor and more of the properties to make sure they are set
        /// </summary>
        [Test]
        public void TestConstructor2andProperties()
        {

            int nodeProxyId = 1;
            string projectFileName = "ProjectFileName";
            string[] targetNames = new string[] { "Build" };
            Dictionary<string, string> dictionary = null;
            int requestId = 1;

            // Check that the initial values of the properties are set based on the constructor being run
            BuildRequest secondConstructorRequest = new BuildRequest(nodeProxyId, projectFileName, targetNames, (IDictionary)dictionary, null, requestId, false, false);
            Assert.AreEqual(1, secondConstructorRequest.HandleId, "Expected NodeProxyId to be 1");
            Assert.AreEqual(1, secondConstructorRequest.RequestId, "Expected RequestId to be 1");
            Assert.IsNull(secondConstructorRequest.GlobalProperties, "Expected GlobalProperties to be null");
            Assert.IsNull(secondConstructorRequest.GlobalPropertiesPassedByTask, "Expected GlobalPropertiesPassedByTask to be null");
            Assert.IsTrue((secondConstructorRequest.TargetNames.Length == 1) && (string.Compare("Build", secondConstructorRequest.TargetNames[0], StringComparison.OrdinalIgnoreCase) == 0), "Expected to have one target with a value of Build");
            Assert.IsTrue(string.Compare("ProjectFileName", secondConstructorRequest.ProjectFileName, StringComparison.OrdinalIgnoreCase) == 0, "Expected project file to be called ProjecFileName");
            Assert.IsNull(secondConstructorRequest.ParentEngine, "Expected parent Engine to be null");
            Assert.IsNull(secondConstructorRequest.OutputsByTarget, "Expected outputs by target to be null");
            Assert.IsFalse(secondConstructorRequest.BuildSucceeded, "Expected BuildSucceeded to be false");
            Assert.AreEqual(secondConstructorRequest.BuildSettings, BuildSettings.None, "Expected BuildSettings to be none");
            Assert.IsNull(secondConstructorRequest.ProjectToBuild, "Expected ProjectToBuild to be null");
            Assert.IsFalse(secondConstructorRequest.FireProjectStartedFinishedEvents, " Expected FireProjectStartedFinishedEvents to be false");
            Assert.IsTrue(secondConstructorRequest.IsGeneratedRequest, "Expected GeneratedRequest to be true");

            // Test that if a nodeProxyId is set to NodeProxy.invalidEngineHandle then we should get a not null dependency chain
            secondConstructorRequest = new BuildRequest(EngineCallback.invalidEngineHandle, projectFileName, targetNames, (IDictionary)dictionary, null, requestId, false, false);
            Assert.IsFalse(secondConstructorRequest.IsGeneratedRequest, "Expected GeneratedRequest to be false");

            // Create a dictionary and hash table so that we can test the second constructor more
            dictionary = new Dictionary<string, string>();
            dictionary.Add("PropertyName", "Value");
            Hashtable propertyHash = new Hashtable();
            propertyHash.Add("PropertyName", "Value");

            // If a dictionary is passed then it will be converted to a hashtable by copying the items out, we shoud
            // therefore make sure that the hashtable has the correct items inside of it
            secondConstructorRequest = new BuildRequest(nodeProxyId, projectFileName, targetNames, (IDictionary)dictionary, null, requestId, false, false);
            string buildPropertyValue = ((Hashtable)secondConstructorRequest.GlobalPropertiesPassedByTask)["PropertyName"] as string;
            Assert.IsTrue(string.Compare(buildPropertyValue, "Value", StringComparison.OrdinalIgnoreCase) == 0, "Expected buildPropertyValue to be value");

            // If a hashtable is passed then the GlobalPropertiesPassedByTask are set to that hashtable
            secondConstructorRequest = new BuildRequest(nodeProxyId, projectFileName, targetNames, propertyHash, null, requestId, false, false);
            Assert.AreEqual(propertyHash, ((Hashtable)secondConstructorRequest.GlobalPropertiesPassedByTask), "Expected propertyHash to be equal to GlobalPropertiesPassedByTask");

            //Change the parentNode to verify the working of isExternalRequest
            secondConstructorRequest.IsExternalRequest = false;
            Assert.IsFalse(secondConstructorRequest.IsExternalRequest, "Expected IsExternalRequest to return false");
            secondConstructorRequest.IsExternalRequest = true;
            Assert.IsTrue(secondConstructorRequest.IsExternalRequest, "Expected IsExternalRequest to return true");

            // Verify that the parentEngine can be set through a property
            secondConstructorRequest.ParentEngine = new Engine();
            Assert.IsNotNull(secondConstructorRequest.ParentEngine, "Expected parent Engine to not be null");
        }

        /// <summary>
        /// Check to make sure that the nonserialized defaults are reset to their correct values
        /// </summary>
        [Test]
        public void RestoreNonSerializedDefaults()
        {

            int nodeProxyId = 1;
            string projectFileName = "ProjectFileName";
            string[] targetNames = new string[] { "Build" };
            Dictionary<string, string> dictionary = null;
            int requestId = 1;

            // Check that the initial values of the properties are set based on the constructor being run
            BuildRequest buildRequest = new BuildRequest(nodeProxyId, projectFileName, targetNames, (IDictionary)dictionary, null, requestId, false, false);
            buildRequest.RestoreNonSerializedDefaults();
            Assert.IsNotNull(buildRequest.OutputsByTarget, "Expected OutputsByTarget to not be null");
            Assert.IsNotNull(buildRequest.ResultByTarget, "Expected ResultByTarget to not be null");
            Assert.IsNull(buildRequest.ProjectToBuild, "Expected ProjectToBuild to be null");
            Assert.IsTrue(buildRequest.BuildSettings == BuildSettings.None, "Expected BuildSettings to be none");
            Assert.IsTrue(buildRequest.FireProjectStartedFinishedEvents, "Expected FireProjectStartedFinishedEvents to be true");
            Assert.AreEqual(EngineCallback.invalidNode, buildRequest.NodeIndex, "Expected NodeIndex to be -2");
            Assert.IsFalse(buildRequest.BuildCompleted, "Expected buildCompleted to be false");
            Assert.IsFalse(buildRequest.BuildSucceeded, "Expected BuildSucceeded to be false");
        }

        /// <summary>
        /// Try some different targetNames combinations and make sure the concatonated list is correct
        /// </summary>
        [Test]
        public void GetTargetNamesList()
        {
            int nodeProxyId = 1;
            string projectFileName = "ProjectFileName";
            string[] targetNames = null; 
            
            Dictionary<string, string> dictionary = null;
            int requestId = 1;

            // Test the case where we pass in null targets
            BuildRequest buildRequest = new BuildRequest(nodeProxyId, projectFileName, targetNames, (IDictionary)dictionary, null, requestId, false, false);
            Assert.IsNull(buildRequest.GetTargetNamesList(), "Expected GetTargetNamesList to be null");

            // Test the case where we pass in one target
            targetNames = new string[] { "Build" };
            buildRequest = new BuildRequest(nodeProxyId, projectFileName, targetNames, (IDictionary)dictionary, null, requestId, false, false);
            Assert.IsTrue(string.Compare("Build", buildRequest.GetTargetNamesList(),StringComparison.OrdinalIgnoreCase)==0, "Expected to see Build as the targetNamesList");
           
            //Test the case where we pass in multiple targets
            targetNames = new string[] {"Build","Build2"};
            buildRequest = new BuildRequest(nodeProxyId, projectFileName, targetNames, (IDictionary)dictionary, null, requestId, false, false);
            Assert.IsTrue(string.Compare("Build;Build2;", buildRequest.GetTargetNamesList(),StringComparison.OrdinalIgnoreCase)==0, "Expected to see Build;Build2; as the targetNamesList");
         }

        /// <summary>
        /// See if a non null buildresult is returned, since there are already BuildResult tests we do not care what is inside it
        /// </summary>
        [Test]
        public void GetBuildResult()
        {
            int nodeProxyId = 1;
            string projectFileName = "ProjectFileName";
            string[] targetNames = null;

            Dictionary<string, string> dictionary = null;
            int requestId = 1;

            // Test the case where we pass in null targets
            BuildRequest buildRequest = new BuildRequest(nodeProxyId, projectFileName, targetNames, (IDictionary)dictionary, null, requestId, false, false);
            Assert.IsNotNull(buildRequest.GetBuildResult(),"Expected GetBuildResult to return a non null BuildRequest");
        }

        [Test]
        public void InitializeFromCachedResult()
        {
            BuildResult result = new BuildResult(null, new Hashtable(StringComparer.OrdinalIgnoreCase), true, 0, 1, 1, true, string.Empty, string.Empty, 0, 0, 0);
            BuildRequest request = new BuildRequest();
            request.InitializeFromCachedResult(result);
            Assert.IsTrue(request.OutputsByTarget == result.OutputsByTarget);
            Assert.IsTrue(request.BuildSucceeded);
            Assert.IsTrue(request.BuildCompleted);
            Assert.IsTrue(request.RestoredFromCache);
            Assert.IsTrue(string.Compare(request.DefaultTargets, result.DefaultTargets, StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(string.Compare(request.InitialTargets, result.InitialTargets, StringComparison.OrdinalIgnoreCase) == 0);
        }

        [Test]
        public void GetBuildRequestTimingData()
        {
            BuildRequest request = new BuildRequest();
            long time = DateTime.Now.Ticks;
            request.StartTime = time;
            Assert.IsTrue(request.StartTime == time);
            request.ProcessingStartTime = time;
            Assert.IsTrue(request.ProcessingStartTime == time);
            request.ProcessingTotalTime = time;
            Assert.IsTrue(request.ProcessingTotalTime == time);
            Assert.IsNotNull(request.GetBuildResult());
        }


        [Test]
        public void TestCustomSerialization()
        {
            string projectFileName = "ProjectFileName";
            string[] targetNames = new string[] { "Build" };
            BuildPropertyGroup globalProperties = null;
            string toolsVersion = "Tool35";
            int requestId = 1;
            int handleId = 4;

            globalProperties = new BuildPropertyGroup();
            BuildProperty propertyToAdd = new BuildProperty("PropertyName", "Value");
            globalProperties.SetProperty(propertyToAdd);
            BuildRequest request1 = new BuildRequest(handleId, projectFileName, targetNames, globalProperties, toolsVersion, requestId, true, true);
            request1.ParentBuildEventContext = new BuildEventContext(1, 2, 3, 4);

            BuildRequest request2 = new BuildRequest(handleId, projectFileName, null, globalProperties, toolsVersion, requestId, true, true);
            request2.GlobalProperties = null;
            request2.ProjectFileName = null;
            request2.DefaultTargets = null;
            request2.InitialTargets = null;
            request2.UseResultsCache = false;
            request2.ParentBuildEventContext = null;

            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            BinaryReader reader = new BinaryReader(stream);
            try
            {
                stream.Position = 0;
                request1.WriteToStream(writer);
                long streamWriteEndPosition = stream.Position;
                request2.WriteToStream(writer);
                long streamWriteEndPosition2 = stream.Position;
                stream.Position = 0;
                BuildRequest request3 = BuildRequest.CreateFromStream(reader);
                long streamReadEndPosition = stream.Position;
                Assert.IsTrue(streamWriteEndPosition == streamReadEndPosition, "Stream end positions should be equal");
                Assert.IsTrue(request1.HandleId == request3.HandleId, "Expected HandleId to Match");
                Assert.IsTrue(request1.RequestId == request3.RequestId, "Expected Request to Match");
                Assert.IsTrue(string.Compare(request1.ProjectFileName, request3.ProjectFileName, StringComparison.OrdinalIgnoreCase) == 0, "Expected ProjectFileName to Match");
                Assert.IsTrue(string.Compare(targetNames[0], request3.TargetNames[0], StringComparison.OrdinalIgnoreCase) == 0, "Expected TargetNames to Match");
                Assert.IsTrue(string.Compare(toolsVersion, request3.ToolsetVersion, StringComparison.OrdinalIgnoreCase) == 0, "Expected ToolsetVersion to Match");
                Assert.IsTrue(request3.TargetNames.Length == 1, "Expected there to be one TargetName");
                Assert.IsTrue(request3.UnloadProjectsOnCompletion, "Expected UnloadProjectsOnCompletion to be true");
                Assert.IsTrue(request3.UseResultsCache, "Expected UseResultsCache to be true");
                Assert.IsTrue(string.Compare(request3.GlobalProperties["PropertyName"].Value,"Value",StringComparison.OrdinalIgnoreCase)==0);
                Assert.AreEqual(request1.ParentBuildEventContext, request3.ParentBuildEventContext, "Expected BuildEventContext to Match");

                BuildRequest request4 = BuildRequest.CreateFromStream(reader);
                streamReadEndPosition = stream.Position;
                Assert.IsTrue(streamWriteEndPosition2 == streamReadEndPosition, "Stream end positions should be equal");
                Assert.IsTrue(request2.HandleId == request4.HandleId, "Expected HandleId to Match");
                Assert.IsTrue(request2.RequestId == request4.RequestId, "Expected Request to Match");
                Assert.IsNull(request4.ProjectFileName);
                Assert.IsNull(request4.TargetNames);
                Assert.IsNull(request4.GlobalProperties);
                Assert.IsNull(request4.ParentBuildEventContext);
            }
            finally
            {
                reader.Close();
                writer = null;
                stream = null;
            }
        }
    }
}
