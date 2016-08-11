// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Collections;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading;

using NUnit.Framework;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;
using System.IO;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class TaskWorkerThread_Tests
    {
        private Engine engine;

        private List<TaskExecutionStateHelper> InitializeTaskState()
        {
            BuildPropertyGroup projectLevelProprtiesForInference;
            BuildPropertyGroup projectLevelPropertiesForExecution;
            Hashtable[] inferenceBucketItemsByName;
            Hashtable[] inferenceBucketMetaData;
            Hashtable projectLevelItemsForInference;
            Hashtable[] executionBucketItemsByName;
            Hashtable[] executionBucketMetaData;
            Hashtable projectLevelItemsForExecution;
            ITaskHost hostObject;
            EngineLoggingServicesHelper loggingHelper;
            string projectFileOfTaskNode;
            string parentProjectFullFileName;
            int nodeProxyId;
            int projectId;
            string executionDirectory;

            XmlElement taskNode = new XmlDocument().CreateElement("MockTask");
            LoadedType taskClass = new LoadedType(typeof(MockTask), new AssemblyLoadInfo(typeof(MockTask).Assembly.FullName, null));
            loggingHelper = new EngineLoggingServicesHelper();
            engine.LoggingServices = loggingHelper;
            Project project = new Project(engine);

            nodeProxyId = engine.EngineCallback.CreateTaskContext(project, null, null, taskNode, EngineCallback.inProcNode, new BuildEventContext(BuildEventContext.InvalidNodeId, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId));
            // Set up some "fake data" which will be passed to the Task Execution State object
            Hashtable[] fakeArray = new Hashtable[1];
            fakeArray[0] = new Hashtable();
            projectLevelProprtiesForInference = new BuildPropertyGroup();
            projectLevelPropertiesForExecution = new BuildPropertyGroup();
            inferenceBucketItemsByName = fakeArray;
            inferenceBucketMetaData = fakeArray;
            projectLevelItemsForInference = new Hashtable();
            executionBucketItemsByName = fakeArray;
            executionBucketMetaData = fakeArray;
            projectLevelItemsForExecution = new Hashtable();
            hostObject = null;
            projectFileOfTaskNode = "In Memory";
            parentProjectFullFileName = project.FullFileName;

            executionDirectory = Directory.GetCurrentDirectory();
            projectId = project.Id;


            MockTaskExecutionModule taskExecutionModule = taskExecutionModule = new MockTaskExecutionModule(new EngineCallback(engine));
            TaskExecutionMode howToExecuteTask = TaskExecutionMode.InferOutputsOnly;

            List<TaskExecutionStateHelper> executionStates = new List<TaskExecutionStateHelper>();

            TaskExecutionStateHelper executionStateNormal1 = new TaskExecutionStateHelper(
                howToExecuteTask,
                LookupHelpers.CreateLookup(projectLevelProprtiesForInference, projectLevelItemsForInference),
                LookupHelpers.CreateLookup(projectLevelPropertiesForExecution, projectLevelItemsForExecution),
                taskNode,
                hostObject,
                projectFileOfTaskNode,
                parentProjectFullFileName,
                executionDirectory,
                nodeProxyId);
            executionStateNormal1.LoggingService = loggingHelper;
            executionStateNormal1.TargetInferenceSuccessful = true;
            executionStateNormal1.ParentModule = taskExecutionModule;

            executionStates.Add(executionStateNormal1);


            TaskExecutionStateHelper executionStateCallBack = new TaskExecutionStateHelper(
                howToExecuteTask,
                LookupHelpers.CreateLookup(projectLevelProprtiesForInference, projectLevelItemsForInference),
                LookupHelpers.CreateLookup(projectLevelPropertiesForExecution, projectLevelItemsForExecution),
                taskNode,
                hostObject,
                projectFileOfTaskNode,
                parentProjectFullFileName,
                executionDirectory,
                nodeProxyId);
            executionStateCallBack.LoggingService = loggingHelper;
            executionStateCallBack.TargetInferenceSuccessful = true;

            executionStates.Add(executionStateCallBack);


            TaskExecutionStateHelper executionStateNormal2 = new TaskExecutionStateHelper(
               howToExecuteTask,
               LookupHelpers.CreateLookup(projectLevelProprtiesForInference, projectLevelItemsForInference),
               LookupHelpers.CreateLookup(projectLevelPropertiesForExecution, projectLevelItemsForExecution),
               taskNode,
               hostObject,
               projectFileOfTaskNode,
               parentProjectFullFileName,
               executionDirectory,
               nodeProxyId);
            executionStateNormal2.LoggingService = loggingHelper;
            executionStateNormal2.TargetInferenceSuccessful = true;
            executionStateNormal2.ParentModule = taskExecutionModule;
            executionStates.Add(executionStateNormal2);

            TaskExecutionStateHelper executionStateNormal3 = new TaskExecutionStateHelper(
              howToExecuteTask,
              LookupHelpers.CreateLookup(projectLevelProprtiesForInference, projectLevelItemsForInference),
              LookupHelpers.CreateLookup(projectLevelPropertiesForExecution, projectLevelItemsForExecution),
              taskNode,
              hostObject,
              projectFileOfTaskNode,
              parentProjectFullFileName,
              executionDirectory,
              nodeProxyId);
            executionStateNormal3.LoggingService = loggingHelper;
            executionStateNormal3.TargetInferenceSuccessful = true;
            executionStateNormal3.ParentModule = taskExecutionModule;
            executionStates.Add(executionStateNormal3);

            return executionStates;
        }


        /// <summary>
        /// Right now we are just testing the fact that the TaskWorker thread will take in a couple of tasks, some doing blocking
        /// callbacks and make sure that each of the tasks completed correctly. Since the tasks are the ones which will 
        /// in the end set the exit event, if the test does not complete then the test has failed.
        /// </summary>
        [Test]
        public void TaskWorkerThreadTest()
        {
            // This event will be triggered right before a "engine" call back is made. 
            // Once this event is fired we insert another item into the queue
            ManualResetEvent rightBeforeCallbackBlock = new ManualResetEvent(false);

            engine = new Engine(@"c:\");
            TaskExecutionModule TEM = new TaskExecutionModule(new EngineCallback(engine), TaskExecutionModule.TaskExecutionModuleMode.MultiProcFullNodeMode, false);

            // Create a worker thread and make it the active node thread
            TaskWorkerThread workerThread = TEM.GetWorkerThread();

            // Get some tasks which we can then provide execution methods to
            List<TaskExecutionStateHelper> tasks = InitializeTaskState();

            tasks[1].ExecutionTaskDelegateParameter = rightBeforeCallbackBlock;
            tasks[1].ExecuteDelegate = delegate(object parameter)
            {
                ((ManualResetEvent)parameter).Set();

                workerThread.WaitForResults(tasks[1].HandleId, new BuildResult[] { new BuildResult(null, new Hashtable(StringComparer.OrdinalIgnoreCase), true, tasks[1].HandleId, 0, 2, false, string.Empty, string.Empty, 0, 0, 0) }, new BuildRequest[1]);
            };

            // Task 0 will cause a baseActiveThread to start up and run
            workerThread.PostWorkItem(tasks[0]);

            // Since this will do a callback and will generate a waitingActiveThread
            workerThread.PostWorkItem(tasks[1]);

            workerThread.ActivateThread();

            // Wait for the call back to happen     
            rightBeforeCallbackBlock.WaitOne();

            // Lets insert a execution task which and post a work item which will cause a localDoneEvent to be set
            tasks[2].ExecutionTaskDelegateParameter = null;
            tasks[2].ExecuteDelegate = null;
            //  TaskWorkerThread.PostBuildResult(new BuildResult(null, true, tasks[2].NodeProxyId, 0));
            workerThread.PostWorkItem(tasks[2]);

            //Post a build Result while one of the threads is waiting active, this should cause us to reuse the first thread
            workerThread.PostBuildResult(new BuildResult(null, new Hashtable(StringComparer.OrdinalIgnoreCase), true, tasks[2].HandleId, 0, 2, false, string.Empty, string.Empty, 0, 0, 0));

            tasks[3].ExecutionTaskDelegateParameter = null;
            tasks[3].ExecuteDelegate = null;

            workerThread.PostWorkItem(tasks[3]);
            TEM.Shutdown();

            // Count up the number of threads used during the execution of the tasks
            List<int> threadsUsedForExecution = new List<int>();
            foreach (TaskExecutionStateHelper state in tasks)
            {
                // If the list does not contain the threadId add it to the list
                if (!threadsUsedForExecution.Contains(state.ThreadId))
                {
                    threadsUsedForExecution.Add(state.ThreadId);
                }
            }
            // Make sure we use less threads then the number of sumbitted tasks which would indicate that threads are reused
            Assert.IsTrue(threadsUsedForExecution.Count < tasks.Count, "Expected for the number of unique threads to be less than the number of tasks as threads should have been reused");
        }

    }
}
