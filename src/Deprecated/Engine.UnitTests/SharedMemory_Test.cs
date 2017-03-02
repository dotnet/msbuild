// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.Framework;
using System.Collections;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class SharedMemory_Test
    {
        [Test]
        public void TestItemsInandOutOfSharedMemory()
        {
            string name = Guid.NewGuid().ToString();
            // Create the shared memory buffer
            SharedMemory readSharedMemory =
                  new SharedMemory
                  (
                        name,
                        SharedMemoryType.ReadOnly,
                        true
                  );


            SharedMemory writeSharedMemory =
                new SharedMemory
                (
                    name,
                    SharedMemoryType.WriteOnly,
                    true
                );

            DualQueue<LocalCallDescriptor> queue = new DualQueue<LocalCallDescriptor>();
            DualQueue<LocalCallDescriptor> hiPriQueue = new DualQueue<LocalCallDescriptor>();
            LocalCallDescriptorForPostLoggingMessagesToHost LargeLogEvent = CreatePostMessageCallDescriptor(1);
            LocalCallDescriptorForUpdateNodeSettings updateNodeSettings = new LocalCallDescriptorForUpdateNodeSettings(true, true, true);
            LocalCallDescriptorForPostBuildResult buildResult = new LocalCallDescriptorForPostBuildResult(CreateBuildResult());
            LocalCallDescriptorForPostBuildRequests buildRequests = new LocalCallDescriptorForPostBuildRequests(CreateBuildRequest());
            LocalCallDescriptorForRequestStatus requestStatus = new LocalCallDescriptorForRequestStatus(4);
            LocalCallDescriptorForPostStatus nodeStatusNoExcept = new LocalCallDescriptorForPostStatus(new NodeStatus(1, true, 2, 3, 4, true));
            LocalCallDescriptorForPostStatus nodeStatusExcept = new LocalCallDescriptorForPostStatus(new NodeStatus(new Exception("I am bad")));
            LocalCallDescriptorForShutdownNode shutdownNode = new LocalCallDescriptorForShutdownNode(Node.NodeShutdownLevel.BuildCompleteSuccess, true);
            LocalCallDescriptorForShutdownComplete shutdownComplete = new LocalCallDescriptorForShutdownComplete(Node.NodeShutdownLevel.BuildCompleteFailure, 0);
            LocalCallDescriptorForInitializationComplete initializeComplete = new LocalCallDescriptorForInitializationComplete(99);

            BuildPropertyGroup propertyGroup = new BuildPropertyGroup();
            BuildProperty propertyToAdd = new BuildProperty("PropertyName", "Value");
            propertyGroup.SetProperty(propertyToAdd);
            CacheEntry[] entries = CreateCacheEntries();
            LocalCallDescriptorForGettingCacheEntriesFromHost getCacheEntries = new LocalCallDescriptorForGettingCacheEntriesFromHost(new string[] { "Hi", "Hello" }, "Name", propertyGroup, "3.5", CacheContentType.Properties);
            LocalCallDescriptorForPostingCacheEntriesToHost postCacheEntries = new LocalCallDescriptorForPostingCacheEntriesToHost(entries, "ScopeName", propertyGroup, "3.5", CacheContentType.BuildResults);
            LocalReplyCallDescriptor replyDescriptor1 = new LocalReplyCallDescriptor(1, entries);
            LocalReplyCallDescriptor replyDescriptor2 = new LocalReplyCallDescriptor(6, "Foo");

            IDictionary environmentVariables = Environment.GetEnvironmentVariables();
            Hashtable environmentVariablesHashtable = new Hashtable(environmentVariables);

            string className = "Class";
            string loggerAssemblyName = "Class";
            string loggerFileAssembly = null;
            string loggerSwitchParameters = "Class";
            LoggerVerbosity verbosity = LoggerVerbosity.Detailed;
            LoggerDescription description = new LoggerDescription(className, loggerAssemblyName, loggerFileAssembly, loggerSwitchParameters, verbosity);
            LocalCallDescriptorForInitializeNode initializeNode = new LocalCallDescriptorForInitializeNode(environmentVariablesHashtable, new LoggerDescription[] { description }, 4, propertyGroup, ToolsetDefinitionLocations.ConfigurationFile, 5, String.Empty);

            queue.Enqueue(LargeLogEvent);
            queue.Enqueue(updateNodeSettings);
            queue.Enqueue(buildResult);
            queue.Enqueue(buildRequests);
            queue.Enqueue(requestStatus);
            queue.Enqueue(nodeStatusNoExcept);
            queue.Enqueue(nodeStatusExcept);
            queue.Enqueue(shutdownNode);
            queue.Enqueue(shutdownComplete);
            queue.Enqueue(initializeComplete);
            queue.Enqueue(getCacheEntries);
            queue.Enqueue(postCacheEntries);
            queue.Enqueue(replyDescriptor1);
            queue.Enqueue(replyDescriptor2);
            queue.Enqueue(initializeNode);
            writeSharedMemory.Write(queue, hiPriQueue, false);

            IList localCallDescriptorList = readSharedMemory.Read();
            Assert.IsTrue(localCallDescriptorList.Count == 15);

            LocalCallDescriptorForPostLoggingMessagesToHost messageCallDescriptor = localCallDescriptorList[0] as LocalCallDescriptorForPostLoggingMessagesToHost;
            VerifyPostMessagesToHost(messageCallDescriptor, 1);

            LocalCallDescriptorForUpdateNodeSettings updateSettingsCallDescriptor = localCallDescriptorList[1] as LocalCallDescriptorForUpdateNodeSettings;
            VerifyUpdateSettings(updateSettingsCallDescriptor);

            LocalCallDescriptorForPostBuildResult buildResultCallDescriptor = localCallDescriptorList[2] as LocalCallDescriptorForPostBuildResult;
            CompareBuildResult(buildResultCallDescriptor);

            LocalCallDescriptorForPostBuildRequests buildRequestsCallDescriptor = localCallDescriptorList[3] as LocalCallDescriptorForPostBuildRequests;
            ComparebuildRequests(buildRequestsCallDescriptor);

            LocalCallDescriptorForRequestStatus requestStatusCallDescriptor = localCallDescriptorList[4] as LocalCallDescriptorForRequestStatus;
            Assert.IsTrue(requestStatusCallDescriptor.RequestId == 4);

            LocalCallDescriptorForPostStatus nodeStatus1CallDescriptor = localCallDescriptorList[5] as LocalCallDescriptorForPostStatus;
            VerifyNodeStatus1(nodeStatus1CallDescriptor);

            LocalCallDescriptorForPostStatus nodeStatus2CallDescriptor = localCallDescriptorList[6] as LocalCallDescriptorForPostStatus;
            VerifyNodeStatus2(nodeStatus2CallDescriptor);

            LocalCallDescriptorForShutdownNode shutdownNodeCallDescriptor = localCallDescriptorList[7] as LocalCallDescriptorForShutdownNode;
            Assert.IsTrue(shutdownNodeCallDescriptor.ShutdownLevel == Node.NodeShutdownLevel.BuildCompleteSuccess);
            Assert.IsTrue(shutdownNodeCallDescriptor.ExitProcess);

            LocalCallDescriptorForShutdownComplete shutdownNodeCompleteCallDescriptor = localCallDescriptorList[8] as LocalCallDescriptorForShutdownComplete;
            Assert.IsTrue(shutdownNodeCompleteCallDescriptor.ShutdownLevel == Node.NodeShutdownLevel.BuildCompleteFailure);

            LocalCallDescriptorForInitializationComplete initializeCompleteCallDescriptor = localCallDescriptorList[9] as LocalCallDescriptorForInitializationComplete;
            Assert.IsTrue(initializeCompleteCallDescriptor.ProcessId == 99);

            LocalCallDescriptorForGettingCacheEntriesFromHost getCacheEntriesCallDescriptor = localCallDescriptorList[10] as LocalCallDescriptorForGettingCacheEntriesFromHost;
            VerifyGetCacheEntryFromHost(getCacheEntriesCallDescriptor);

            LocalCallDescriptorForPostingCacheEntriesToHost postCacheEntriesCallDescriptor = localCallDescriptorList[11] as LocalCallDescriptorForPostingCacheEntriesToHost;
            Assert.IsTrue(string.Compare(postCacheEntriesCallDescriptor.ScopeName, "ScopeName", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(string.Compare(postCacheEntriesCallDescriptor.ScopeProperties["PropertyName"].Value, "Value", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(string.Compare(postCacheEntriesCallDescriptor.ScopeToolsVersion, "3.5", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(postCacheEntriesCallDescriptor.ContentType == CacheContentType.BuildResults);
            VerifyGetCacheEntries(postCacheEntriesCallDescriptor.Entries);

            LocalReplyCallDescriptor reply1CallDescriptor = localCallDescriptorList[12] as LocalReplyCallDescriptor;
            Assert.IsTrue(reply1CallDescriptor.RequestingCallNumber == 1);
            VerifyGetCacheEntries((CacheEntry[])reply1CallDescriptor.ReplyData);

            LocalReplyCallDescriptor reply2CallDescriptor = localCallDescriptorList[13] as LocalReplyCallDescriptor;
            Assert.IsTrue(reply2CallDescriptor.RequestingCallNumber == 6);
            Assert.IsTrue(string.Compare("Foo", (string)reply2CallDescriptor.ReplyData, StringComparison.OrdinalIgnoreCase) == 0);

            LocalCallDescriptorForInitializeNode initializeCallDescriptor = localCallDescriptorList[14] as LocalCallDescriptorForInitializeNode;
            Assert.IsTrue(initializeCallDescriptor.ParentProcessId == 5);
            Assert.IsTrue(initializeCallDescriptor.NodeId == 4);
            Assert.IsTrue(initializeCallDescriptor.ToolsetSearchLocations == ToolsetDefinitionLocations.ConfigurationFile);
            Assert.IsTrue(string.Compare(initializeCallDescriptor.ParentGlobalProperties["PropertyName"].Value, "Value", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(string.Compare(initializeCallDescriptor.NodeLoggers[0].Name, "Class", StringComparison.OrdinalIgnoreCase) == 0);

            IDictionary variables = Environment.GetEnvironmentVariables();

            Assert.IsTrue(variables.Count == initializeCallDescriptor.EnvironmentVariables.Count);
            foreach (string key in variables.Keys)
            {
                Assert.IsTrue(string.Compare((string)initializeCallDescriptor.EnvironmentVariables[key], (string)variables[key], StringComparison.OrdinalIgnoreCase) == 0);
            }

            writeSharedMemory.Reset();
            readSharedMemory.Reset();
            readSharedMemory = null;
            writeSharedMemory = null;
        }

        [Test]
        public void TestLargeSharedMemorySend()
        {
            string name = Guid.NewGuid().ToString();
            // Create the shared memory buffer
            SharedMemory readSharedMemory =
                  new SharedMemory
                  (
                        name,
                        SharedMemoryType.ReadOnly,
                        true
                  );


            SharedMemory writeSharedMemory =
                new SharedMemory
                (
                    name,
                    SharedMemoryType.WriteOnly,
                    true
                );

            DualQueue<LocalCallDescriptor> queue = new DualQueue<LocalCallDescriptor>();
            DualQueue<LocalCallDescriptor> hiPriQueue = new DualQueue<LocalCallDescriptor>();

            int numberOfEvents = 2500;
            LocalCallDescriptorForPostLoggingMessagesToHost LargeLogEvent = CreatePostMessageCallDescriptor(numberOfEvents);
            queue.Enqueue(LargeLogEvent);
            writeSharedMemory.Write(queue, hiPriQueue, false);
            IList localCallDescriptorList = readSharedMemory.Read();
            while (localCallDescriptorList == null || localCallDescriptorList.Count == 0)
            {
                writeSharedMemory.Write(queue, hiPriQueue, false);
                localCallDescriptorList = readSharedMemory.Read();
            }
            VerifyPostMessagesToHost((LocalCallDescriptorForPostLoggingMessagesToHost)localCallDescriptorList[0], numberOfEvents);
            writeSharedMemory.Reset();
            readSharedMemory.Reset();
            readSharedMemory = null;
            writeSharedMemory = null;
        }

        [Test]
        public void TestHiPrioritySend()
        {
            string name = Guid.NewGuid().ToString();
            // Create the shared memory buffer
            SharedMemory readSharedMemory =
                  new SharedMemory
                  (
                        name,
                        SharedMemoryType.ReadOnly,
                        true
                  );


            SharedMemory writeSharedMemory =
                new SharedMemory
                (
                    name,
                    SharedMemoryType.WriteOnly,
                    true
                );

            DualQueue<LocalCallDescriptor> queue = new DualQueue<LocalCallDescriptor>();
            DualQueue<LocalCallDescriptor> hiPriQueue = new DualQueue<LocalCallDescriptor>();


            int numberOfEvents = 20;
            LocalCallDescriptorForPostLoggingMessagesToHost LargeLogEvent = CreatePostMessageCallDescriptor(numberOfEvents);
            queue.Enqueue(LargeLogEvent);
            LocalCallDescriptorForPostStatus nodeStatusExcept = new LocalCallDescriptorForPostStatus(new NodeStatus(new Exception("I am bad")));
            hiPriQueue.Enqueue(nodeStatusExcept);

            writeSharedMemory.Write(queue, hiPriQueue, true);
            IList localCallDescriptorList = readSharedMemory.Read();

            Assert.IsTrue(localCallDescriptorList.Count == 2);

            VerifyNodeStatus2((LocalCallDescriptorForPostStatus)localCallDescriptorList[0]);
            VerifyPostMessagesToHost((LocalCallDescriptorForPostLoggingMessagesToHost)localCallDescriptorList[1], numberOfEvents);
            writeSharedMemory.Reset();
            readSharedMemory.Reset();
            readSharedMemory = null;
            writeSharedMemory = null;
        }


        [Test]
        public void TestExistingBufferDetection()
        {
            string name = Guid.NewGuid().ToString();
            // Create the shared memory buffer
            SharedMemory sharedMemory =
                  new SharedMemory
                  (
                        name,
                        SharedMemoryType.ReadOnly,
                        false // disallow duplicates
                  );

            Assert.IsTrue(sharedMemory.IsUsable, "Shared memory should be usable");

            SharedMemory sharedMemoryDuplicate =
                new SharedMemory
                (
                    name,
                    SharedMemoryType.ReadOnly,
                    false // disallow duplicates
                );

            Assert.IsFalse(sharedMemoryDuplicate.IsUsable, "Shared memory should not be usable");
            sharedMemoryDuplicate.Dispose();

            sharedMemoryDuplicate =
                new SharedMemory
                (
                    name,
                    SharedMemoryType.ReadOnly,
                    true // allow duplicates
                );

            Assert.IsTrue(sharedMemoryDuplicate.IsUsable, "Shared memory should be usable");
            sharedMemoryDuplicate.Dispose();

            sharedMemory.Dispose();
        }

        private void VerifyGetCacheEntries(CacheEntry[] entries)
        {
            Assert.IsTrue(entries[0] is BuildItemCacheEntry);
            Assert.IsTrue(string.Compare(entries[0].Name, "Badger" , StringComparison.OrdinalIgnoreCase) == 0);
            BuildItem[] buildItemArray = ((BuildItemCacheEntry)entries[0]).BuildItems;
            Assert.IsTrue(buildItemArray.Length == 2);
            Assert.IsTrue(string.Compare(buildItemArray[0].Include, "TestInclude1", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(string.Compare(buildItemArray[1].Include, "TestInclude2", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(string.Compare(buildItemArray[1].Name, "BuildItem2", StringComparison.OrdinalIgnoreCase) == 0);

            Assert.IsTrue(entries[1] is BuildResultCacheEntry);
            Assert.IsTrue(string.Compare(entries[1].Name, "Koi", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(((BuildResultCacheEntry)entries[1]).BuildResult);
            buildItemArray = ((BuildResultCacheEntry)entries[1]).BuildItems;
            Assert.IsTrue(buildItemArray.Length == 2);
            Assert.IsTrue(string.Compare(buildItemArray[0].Include, "TestInclude1", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(string.Compare(buildItemArray[1].Include, "TestInclude2", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(string.Compare(buildItemArray[1].Name, "BuildItem2", StringComparison.OrdinalIgnoreCase) == 0);

            Assert.IsTrue(entries[2] is PropertyCacheEntry);
            Assert.IsTrue(string.Compare(((PropertyCacheEntry)entries[2]).Name, "Seagull", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(string.Compare(((PropertyCacheEntry)entries[2]).Value, "bread", StringComparison.OrdinalIgnoreCase) == 0);
        }

        private static CacheEntry[] CreateCacheEntries()
        {
            CacheEntry[] entries = new CacheEntry[3];

            BuildItem buildItem1 = new BuildItem("BuildItem1", "Item1");
            BuildItem buildItem2 = new BuildItem("BuildItem2", "Item2");
            buildItem1.Include = "TestInclude1";
            buildItem2.Include = "TestInclude2";
            BuildItem[] buildItems = new BuildItem[2];
            buildItems[0] = buildItem1;
            buildItems[1] = buildItem2;

            entries[0] = new BuildItemCacheEntry("Badger", buildItems);
            entries[1] = new BuildResultCacheEntry("Koi", buildItems, true);
            entries[2] = new PropertyCacheEntry("Seagull", "bread");
            return entries;
        }

        private static void VerifyGetCacheEntryFromHost(LocalCallDescriptorForGettingCacheEntriesFromHost getCacheEntriesCallDescriptor)
        {
            Assert.IsTrue(string.Compare(getCacheEntriesCallDescriptor.Names[0], "Hi", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(string.Compare(getCacheEntriesCallDescriptor.Names[1], "Hello", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(string.Compare(getCacheEntriesCallDescriptor.ScopeName, "Name", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(string.Compare(getCacheEntriesCallDescriptor.ScopeProperties["PropertyName"].Value, "Value", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(string.Compare(getCacheEntriesCallDescriptor.ScopeToolsVersion, "3.5", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(getCacheEntriesCallDescriptor.ContentType == CacheContentType.Properties);
        }

        private static void VerifyNodeStatus2(LocalCallDescriptorForPostStatus nodeStatus2CallDescriptor)
        {
            Assert.IsTrue(nodeStatus2CallDescriptor.StatusOfNode.IsActive);
            Assert.IsFalse(nodeStatus2CallDescriptor.StatusOfNode.IsLaunchInProgress);
            Assert.IsTrue(nodeStatus2CallDescriptor.StatusOfNode.QueueDepth == 0);
            Assert.IsTrue(nodeStatus2CallDescriptor.StatusOfNode.RequestId == -1);
            Assert.IsTrue(string.Compare(nodeStatus2CallDescriptor.StatusOfNode.UnhandledException.Message, "I am bad", StringComparison.OrdinalIgnoreCase) == 0);
        }

        private static void VerifyNodeStatus1(LocalCallDescriptorForPostStatus nodeStatus1CallDescriptor)
        {
            Assert.IsTrue(nodeStatus1CallDescriptor.StatusOfNode.IsActive);
            Assert.IsTrue(nodeStatus1CallDescriptor.StatusOfNode.IsLaunchInProgress);
            Assert.IsTrue(nodeStatus1CallDescriptor.StatusOfNode.LastLoopActivity == 4);
            Assert.IsTrue(nodeStatus1CallDescriptor.StatusOfNode.LastTaskActivity == 3);
            Assert.IsTrue(nodeStatus1CallDescriptor.StatusOfNode.QueueDepth == 2);
            Assert.IsTrue(nodeStatus1CallDescriptor.StatusOfNode.RequestId == 1);
            Assert.IsTrue(nodeStatus1CallDescriptor.StatusOfNode.UnhandledException == null);
        }

        private static void ComparebuildRequests(LocalCallDescriptorForPostBuildRequests buildRequestsCallDescriptor)
        {
            BuildRequest[] requests = buildRequestsCallDescriptor.BuildRequests;
            Assert.IsTrue(requests.Length == 2);
            BuildEventContext testContext = new BuildEventContext(1, 2, 3, 4); ;
            foreach (BuildRequest request1 in requests)
            {
                Assert.IsTrue(request1.HandleId == 4, "Expected HandleId to Match");
                Assert.IsTrue(request1.RequestId == 1, "Expected Request to Match");
                Assert.IsTrue(string.Compare(request1.ProjectFileName, "ProjectFileName", StringComparison.OrdinalIgnoreCase) == 0, "Expected ProjectFileName to Match");
                Assert.IsTrue(string.Compare(request1.TargetNames[0], "Build", StringComparison.OrdinalIgnoreCase) == 0, "Expected TargetNames to Match");
                Assert.IsTrue(string.Compare(request1.ToolsetVersion, "Tool35", StringComparison.OrdinalIgnoreCase) == 0, "Expected ToolsetVersion to Match");
                Assert.IsTrue(request1.TargetNames.Length == 1, "Expected there to be one TargetName");
                Assert.IsTrue(request1.UnloadProjectsOnCompletion, "Expected UnloadProjectsOnCompletion to be true");
                Assert.IsTrue(request1.UseResultsCache, "Expected UseResultsCache to be true");
                Assert.IsTrue(string.Compare(request1.GlobalProperties["PropertyName"].Value, "Value", StringComparison.OrdinalIgnoreCase) == 0);
                Assert.AreEqual(request1.ParentBuildEventContext, testContext, "Expected BuildEventContext to Match");
            }
        }

        private static BuildRequest[] CreateBuildRequest()
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
            BuildRequest[] requests = new BuildRequest[2];
            requests[0] = new BuildRequest(handleId, projectFileName, targetNames, globalProperties, toolsVersion, requestId, true, true);
            requests[0].ParentBuildEventContext = new BuildEventContext(1, 2, 3, 4);
            requests[1] = new BuildRequest(handleId, projectFileName, targetNames, globalProperties, toolsVersion, requestId, true, true);
            requests[1].ParentBuildEventContext = new BuildEventContext(1, 2, 3, 4);
            return requests;
        }

        private static void CompareBuildResult(LocalCallDescriptorForPostBuildResult buildResultCallDescriptor)
        {
            BuildResult result = buildResultCallDescriptor.ResultOfBuild;
            Assert.IsTrue(result.ResultByTarget.Count == 1);
            Assert.IsTrue(((Target.BuildState)result.ResultByTarget["ONE"]) == Target.BuildState.CompletedSuccessfully);
            Assert.AreEqual(result.HandleId, 0, "Expected HandleId to Match");
            Assert.AreEqual(result.RequestId, 1, "Expected RequestId to Match");
            Assert.AreEqual(result.UseResultCache, true, "Expected UseResultCache to Match");
            Assert.IsTrue(string.Compare(result.InitialTargets, "Fighter", StringComparison.OrdinalIgnoreCase) == 0, "Expected InitialTargets to Match");
            Assert.IsTrue(string.Compare(result.DefaultTargets, "Foo", StringComparison.OrdinalIgnoreCase) == 0, "Expected DefaultTargets to Match");
            BuildItem[] buildItemArray = ((BuildItem[])result.OutputsByTarget["TaskItems"]);
            Assert.IsTrue(buildItemArray.Length == 3);
            Assert.IsTrue(string.Compare(buildItemArray[0].Include, "TestInclude1", StringComparison.OrdinalIgnoreCase) == 0);
            Assertion.AssertEquals("m1", buildItemArray[0].GetMetadata("m"));
            Assertion.AssertEquals("n1", buildItemArray[0].GetMetadata("n"));
            Assert.IsTrue(string.Compare(buildItemArray[1].Include, "TestInclude2", StringComparison.OrdinalIgnoreCase) == 0);
            Assert.IsTrue(string.Compare(buildItemArray[1].Name, "BuildItem2", StringComparison.OrdinalIgnoreCase) == 0);
            Assertion.AssertEquals("m1", buildItemArray[2].GetMetadata("m"));
            Assertion.AssertEquals("n1", buildItemArray[2].GetMetadata("n"));
            Assertion.AssertEquals("o2", buildItemArray[2].GetMetadata("o"));
            Assert.AreEqual(result.TotalTime, 1, "Expected TotalTime to Match");
            Assert.AreEqual(result.EngineTime, 2, "Expected EngineTime to Match");
            Assert.AreEqual(result.TaskTime, 3, "Expected TaskTime to Match");
        }
        private static BuildResult CreateBuildResult()
        {
            BuildItem buildItem1 = new BuildItem(null, "Item1");
            BuildItem buildItem2 = new BuildItem("BuildItem2", "Item2");
            BuildItem buildItem3 = BuildItem_Tests.GetXmlBackedItemWithDefinitionLibrary(); // default metadata m=m1 and o=o1
            buildItem1.Include = "TestInclude1";
            buildItem2.Include = "TestInclude2";
            buildItem1.SetMetadata("m", "m1");
            buildItem1.SetMetadata("n", "n1");
            buildItem3.SetMetadata("n", "n1");
            buildItem3.SetMetadata("o", "o2");
            BuildItem[] taskItems = new BuildItem[3];
            taskItems[0] = buildItem1;
            taskItems[1] = buildItem2;
            taskItems[2] = buildItem3;

            Dictionary<object, object> dictionary = new Dictionary<object, object>();
            dictionary.Add("TaskItems", taskItems);

            BuildResult resultWithOutputs = new BuildResult(dictionary, new Hashtable(StringComparer.OrdinalIgnoreCase), true, 0, 1, 2, true, "Foo", "Fighter", 1, 2, 3);
            resultWithOutputs.ResultByTarget.Add("ONE", Target.BuildState.CompletedSuccessfully);
            resultWithOutputs.HandleId = 0;
            resultWithOutputs.RequestId = 1;
            return resultWithOutputs;
        }
        private static void VerifyUpdateSettings(LocalCallDescriptorForUpdateNodeSettings updateSettingsCallDescriptor)
        {
            Assert.IsNotNull(updateSettingsCallDescriptor);
            Assert.IsTrue(updateSettingsCallDescriptor.LogOnlyCriticalEvents);
            Assert.IsTrue(updateSettingsCallDescriptor.UseBreadthFirstTraversal);
            Assert.IsTrue(updateSettingsCallDescriptor.CentralizedLogging);
        }
        private static void VerifyPostMessagesToHost(LocalCallDescriptorForPostLoggingMessagesToHost messageCallDescriptor, int count)
        {
            Assert.IsTrue(messageCallDescriptor.BuildEvents.Length == count);
            for (int i = 0; i < count; i++)
            {
                Assert.IsTrue(string.Compare("aaaaaaaaaaaaaaa", messageCallDescriptor.BuildEvents[i].BuildEvent.Message, StringComparison.OrdinalIgnoreCase) == 0);
            }
        }
        private static LocalCallDescriptorForPostLoggingMessagesToHost CreatePostMessageCallDescriptor(int numberEvents)
        {
            NodeLoggingEvent[] eventArray = new NodeLoggingEvent[numberEvents];
            for (int i = 0; i< numberEvents; i++)
            {
                BuildMessageEventArgs message = new BuildMessageEventArgs("aaaaaaaaaaaaaaa", "aaa", "a", MessageImportance.High);
                message.BuildEventContext = new BuildEventContext(1, 2, 3, 4);
                eventArray[i] = new NodeLoggingEvent(message);
            }
            LocalCallDescriptorForPostLoggingMessagesToHost LargeLogEvent = new LocalCallDescriptorForPostLoggingMessagesToHost(eventArray);
            return LargeLogEvent;
        }
    }
}
