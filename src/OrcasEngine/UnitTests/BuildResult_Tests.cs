// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Collections;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;

using NUnit.Framework;

using Microsoft.Build.BuildEngine;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class BuildResult_Tests
    {
        // A build result where the taskoutputs are null
        private BuildResult resultNoOutputs;
        
        // A build result where the task outputs are acutal values
        private BuildResult resultWithOutputs;

        // Create an uninitialized item to test one of the code paths in build result
        private  BuildItem buildItem1 = new BuildItem(null, "Item1");
        // Create an initialized item
        private  BuildItem buildItem2 = new BuildItem("BuildItem2", "Item2");

        [SetUp]
        public void Initialize()
        {
            // Create some items and place them in a dictionary
            // Add some include information so that when we check the final 
            // item spec we can verify that the item was recreated properly
            buildItem1.Include = "TestInclude1";
            buildItem2.Include = "TestInclude2";

            BuildItem[] taskItems = new BuildItem[2];
            taskItems[0] = buildItem1;
            taskItems[1] = buildItem2;

            Dictionary<object, object> dictionary = new Dictionary<object, object>();
            dictionary.Add("TaskItems", taskItems);
            resultNoOutputs = new BuildResult(null, new Hashtable(StringComparer.OrdinalIgnoreCase), true, 0, 1, 2, true, string.Empty, string.Empty, 0, 0, 0);
            resultWithOutputs = new BuildResult(dictionary, new Hashtable(StringComparer.OrdinalIgnoreCase), true, 0, 1, 2, true, string.Empty, string.Empty, 0, 0, 0);
        }

        /// <summary>
        /// Test the constructor and properties when taskOutputs have been placed into the item
        /// </summary>
        [Test]
        public void TestConstructorsAndProperties()
        {
            BuildResult resultWithOutputsFromBuildResult = new BuildResult(resultWithOutputs, false /* shallow copy */);
            Assert.IsNotNull(resultWithOutputsFromBuildResult.OutputsByTarget, "Exepcted resultWithOutputsFromBuildResult.OutputsByTarget to not be null");
            Assert.IsTrue(resultWithOutputsFromBuildResult.EvaluationResult, "Expected resultWithOutputsFromBuildResult.EvaluationResult to be true");
            Assert.AreEqual(0, resultWithOutputsFromBuildResult.HandleId, "Expected resultWithOutputsFromBuildResult.NodeProxyId to be 0");
            Assert.AreEqual(1, resultWithOutputsFromBuildResult.RequestId, "Expected resultWithOutputsFromBuildResult.RequestId to be 1");
            Assert.AreEqual(2, resultWithOutputsFromBuildResult.ProjectId, "Expected resultWithOutputsFromBuildResult.ProjectId to be 1");

            // Test some setters which are not set otherwise during the tests
            resultWithOutputsFromBuildResult.HandleId = 3;
            resultWithOutputsFromBuildResult.RequestId = 4;
            Assert.AreEqual(3, resultWithOutputsFromBuildResult.HandleId, "Expected resultWithOutputsFromBuildResult.NodeProxyId to be 3");
            Assert.AreEqual(4, resultWithOutputsFromBuildResult.RequestId, "Expected resultWithOutputsFromBuildResult.RequestId to be 4");

            resultWithOutputsFromBuildResult = new BuildResult(resultWithOutputs, true /* deep copy */);
            Assert.IsNotNull(resultWithOutputsFromBuildResult.OutputsByTarget, "Exepcted resultWithOutputsFromBuildResult.OutputsByTarget to not be null");
            Assert.IsTrue(resultWithOutputsFromBuildResult.EvaluationResult, "Expected resultWithOutputsFromBuildResult.EvaluationResult to be true");
            Assert.AreEqual(0, resultWithOutputsFromBuildResult.HandleId, "Expected resultWithOutputsFromBuildResult.NodeProxyId to be 0");
            Assert.AreEqual(1, resultWithOutputsFromBuildResult.RequestId, "Expected resultWithOutputsFromBuildResult.RequestId to be 1");
            Assert.AreEqual(2, resultWithOutputsFromBuildResult.ProjectId, "Expected resultWithOutputsFromBuildResult.ProjectId to be 1");
            
            // Test some setters which are not set otherwise during the tests
            resultWithOutputsFromBuildResult.HandleId = 3;
            resultWithOutputsFromBuildResult.RequestId = 4;
            Assert.AreEqual(3, resultWithOutputsFromBuildResult.HandleId, "Expected resultWithOutputsFromBuildResult.NodeProxyId to be 3");
            Assert.AreEqual(4, resultWithOutputsFromBuildResult.RequestId, "Expected resultWithOutputsFromBuildResult.RequestId to be 4");
            
            // Test the setting of RequestId
            resultWithOutputsFromBuildResult.RequestId = 4;
            Assert.AreEqual(4, resultWithOutputsFromBuildResult.RequestId, "Expected resultWithOutputsFromBuildResult.RequestId to be 4");
        }

        /// <summary>
        /// Test the constructor and the properties when the taskOutputs are null
        /// </summary>
        [Test]
        public void TestConstructorsAndPropertiesNullOutputs()
        {
            Assert.IsNull(resultNoOutputs.OutputsByTarget, "Exepcted resultNoOutputs.OutputsByTarget to be null");
            Assert.IsTrue(resultNoOutputs.EvaluationResult, "Expected resultNoOutputs.EvaluationResult to be true");
            Assert.AreEqual(0, resultNoOutputs.HandleId, "Expected resultNoOutputs.NodeProxyId to be 0");
            Assert.AreEqual(1, resultNoOutputs.RequestId, "Expected resultNoOutputs.RequestId to be 1");
            Assert.AreEqual(2, resultNoOutputs.ProjectId, "Expected resultNoOutputs.ProjectId to be 2");
        }

        /// <summary>
        /// Check that a build result constructor which is passed a null buildresult gives the proper exception
        /// </summary>
        [Test]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestNullParameterInConstructor()
        {
            BuildResult shouldFail = new BuildResult(null, false /* shallow copy */);
        }
        /// <summary>
        /// Test the constructor and the properties when the taskOutputs are null
        /// </summary>
        [Test]
        public void TestUncacheableProperty()
        {
            BuildResult resultCacheable = new BuildResult(null, new Hashtable(StringComparer.OrdinalIgnoreCase), true, 0, 1, 2, false, string.Empty, string.Empty, 0, 0, 0);
            Assert.IsFalse(resultCacheable.UseResultCache, "Expected resultCacheable.UseResultCache to be false");
            BuildResult resultUncacheable = new BuildResult(null, new Hashtable(StringComparer.OrdinalIgnoreCase), true, 0, 1, 2, true, string.Empty, string.Empty, 0, 0, 0);
            Assert.IsTrue(resultUncacheable.UseResultCache, "Expected resultUncacheable.UseResultCache to be true");
        }

        [Test]
        public void ConvertToTaskItems()
        {

            resultWithOutputs.ConvertToTaskItems();
            Assert.AreEqual(1, resultWithOutputs.OutputsByTarget.Count, "Expected Number of Item arrays to be 1");
            string[] keys = new string[resultWithOutputs.OutputsByTarget.Count];
            resultWithOutputs.OutputsByTarget.Keys.CopyTo(keys, 0);
            TaskItem[] taskItems = (TaskItem[])resultWithOutputs.OutputsByTarget[keys[0]];
            bool foundFirstItem = false;
            bool foundSecondItem = false;
            foreach (TaskItem taskItem in taskItems)
            {

                if ((taskItem.item.IsUninitializedItem) && (string.Compare(taskItem.item.FinalItemSpec, "TestInclude1", StringComparison.OrdinalIgnoreCase) == 0))
                {
                    foundFirstItem = true;
                }
                else if ((string.Compare(taskItem.item.Name, "BuildItem2", StringComparison.OrdinalIgnoreCase) == 0) && (string.Compare(taskItem.item.FinalItemSpec, "TestInclude2", StringComparison.OrdinalIgnoreCase) == 0))
                {
                    foundSecondItem = true;
                }
            }
            Assert.IsTrue(foundFirstItem && foundSecondItem, "Expected to find both items converted to taskItems");
        }

        [Test]
        public void CustomSerialization()
        {
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            BinaryReader reader = new BinaryReader(stream);
            try
            {
                stream.Position = 0;
                BuildResult result1 = new BuildResult(resultWithOutputs, false /* shallow copy */);
                result1.HandleId = 2;
                result1.RequestId = 3;
                result1.WriteToStream(writer);

                BuildResult result2 = new BuildResult(resultWithOutputs, true /* deep copy */);
                result2.HandleId = 2;
                result2.RequestId = 3;
                result2.WriteToStream(writer);

                BuildResult result3 = new BuildResult(null, new Hashtable(StringComparer.OrdinalIgnoreCase), true, 0, 1, 2, true, null, null, 0, 0, 0);
                result3.HandleId = 2;
                result3.RequestId = 3;
                result3.WriteToStream(writer);

                BuildResult result4 = new BuildResult(null, new Hashtable(StringComparer.OrdinalIgnoreCase), true, 0, 1, 2, true, "DefaultTarget", "InitialTarget", 0, 0, 0);
                result4.HandleId = 2;
                result4.RequestId = 3;
                result4.ResultByTarget.Add("ONE", Target.BuildState.CompletedSuccessfully);
                result4.WriteToStream(writer);
                long streamWriteEndPosition = stream.Position;
                stream.Position = 0;

                BuildResult result5 = BuildResult.CreateFromStream(reader);
                BuildResult result6 = BuildResult.CreateFromStream(reader);
                BuildResult result7 = BuildResult.CreateFromStream(reader);
                BuildResult result8 = BuildResult.CreateFromStream(reader);
                long streamReadEndPosition = stream.Position;
                Assert.IsTrue(streamWriteEndPosition == streamReadEndPosition, "Stream end positions should be equal");
                CompareBuildResult(result1, result5);
                CompareBuildResult(result2, result6);
                CompareBuildResult(result3, result7);
                CompareBuildResult(result4, result8);
                Assert.IsTrue(result8.ResultByTarget.Count == 1);
                Assert.IsTrue(((Target.BuildState)result8.ResultByTarget["ONE"]) == Target.BuildState.CompletedSuccessfully);
                BuildItem[] buildItemArray = ((BuildItem[])result1.OutputsByTarget["TaskItems"]);
                Assert.IsTrue(buildItemArray.Length == 2);
                Assert.IsTrue(string.Compare(buildItemArray[0].Include, buildItem1.Include, StringComparison.OrdinalIgnoreCase)==0);
                Assert.IsTrue(string.Compare(buildItemArray[1].Include, buildItem2.Include, StringComparison.OrdinalIgnoreCase) == 0);
                Assert.IsTrue(string.Compare(buildItemArray[1].Name, buildItem2.Name, StringComparison.OrdinalIgnoreCase) == 0);
            }
            finally
            {
                reader.Close();
                writer = null;
                stream = null;
            }
        }

        private static void CompareBuildResult(BuildResult result1, BuildResult result2)
        {
            Assert.AreEqual(result1.HandleId, result2.HandleId, "Expected HandleId to Match");
            Assert.AreEqual(result1.RequestId, result2.RequestId, "Expected RequestId to Match");
            Assert.AreEqual(result1.ProjectId, result2.ProjectId, "Expected ProjectId to Match");
            Assert.AreEqual(result1.UseResultCache, result2.UseResultCache, "Expected UseResultCache to Match");
            Assert.IsTrue(string.Compare(result1.InitialTargets, result2.InitialTargets, StringComparison.OrdinalIgnoreCase) == 0, "Expected InitialTargets to Match");
            Assert.IsTrue(string.Compare(result1.DefaultTargets, result2.DefaultTargets, StringComparison.OrdinalIgnoreCase) == 0, "Expected DefaultTargets to Match");
        }
    }
}
