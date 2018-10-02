// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using System.Collections;

using NUnit.Framework;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;
using System.Threading;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class TaskExecutionState_Test
    {

        [SetUp]
        public void SetUp()
        {
            // Whole bunch of setup code.
            XmlElement taskNode = new XmlDocument().CreateElement("MockTask");
            LoadedType taskClass = new LoadedType(typeof(MockTask), new AssemblyLoadInfo(typeof(MockTask).Assembly.FullName, null));
            Engine engine = new Engine(@"c:\");
            loggingHelper = new EngineLoggingServicesHelper();
            engine.LoggingServices = loggingHelper;
            Project project = new Project(engine);
            taskExecutionModule = new MockTaskExecutionModule(new EngineCallback(engine));
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
            nodeProxyId = engine.EngineCallback.CreateTaskContext(project, null, null, taskNode, EngineCallback.inProcNode, new BuildEventContext(BuildEventContext.InvalidNodeId, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId));
            executionDirectory = Directory.GetCurrentDirectory();
            projectId = project.Id;
        }

        /// <summary>
        /// Test the case where the execution directory does not equal the current directory. In that 
        /// case the current directory should be switched to the execution directory
        /// </summary>
        [Test]
        public void ExecuteTaskCurrentDirNotEqualExecutionDir()
        {
            string theCurrentDirectory = Directory.GetCurrentDirectory();
            try
            {
                string executionDirectory = "C:\\";
                TaskExecutionMode howToExecuteTask = TaskExecutionMode.InferOutputsOnly;
                XmlDocument doc = new XmlDocument();
                XmlElement taskNode = doc.CreateElement("Foo");
                TaskExecutionStateHelper executionState = new TaskExecutionStateHelper(
                    howToExecuteTask,
                    LookupHelpers.CreateLookup(projectLevelProprtiesForInference, projectLevelItemsForInference),
                    LookupHelpers.CreateLookup(projectLevelPropertiesForExecution, projectLevelItemsForExecution),
                    taskNode,
                    hostObject,
                    projectFileOfTaskNode,
                    parentProjectFullFileName,
                    executionDirectory,
                    nodeProxyId);
                executionState.ParentModule = taskExecutionModule;
                executionState.LoggingService = loggingHelper;
                executionState.TargetInferenceSuccessful = true;
                executionState.ExecuteTask();
                Assert.IsTrue(string.Compare(Directory.GetCurrentDirectory(), "C:\\", StringComparison.OrdinalIgnoreCase) == 0, "Expected current directory to be c:\\ which should show up as an empty directory string");
            }
            finally
            {

                Directory.SetCurrentDirectory(theCurrentDirectory);
            }

        }

        [Test]
        public void TaskExecutionStateTestProperties()
        {
            TaskExecutionMode howToExecuteTask = TaskExecutionMode.ExecuteTaskAndGatherOutputs;
            XmlDocument doc = new XmlDocument();
            XmlElement taskNode = doc.CreateElement("Foo");
            TaskExecutionStateHelper executionState = new TaskExecutionStateHelper(
                howToExecuteTask,
                LookupHelpers.CreateLookup(projectLevelProprtiesForInference, projectLevelItemsForInference),
                LookupHelpers.CreateLookup(projectLevelPropertiesForExecution, projectLevelItemsForExecution),
                taskNode,
                hostObject,
                projectFileOfTaskNode,
                parentProjectFullFileName,
                executionDirectory,
                nodeProxyId);

            executionState.HandleId = 1;
            Assert.AreEqual(1, executionState.HandleId, "Expected NodeProxyId to be equal to 1");

            executionState.LoggingService = loggingHelper;
            Assert.AreEqual(loggingHelper, executionState.LoggingService, "Expected LoggingService to be equal to the loggingService set in the LoggingService property setter");
        }

        #region Data

        // Some class variables which are used in the unit tests
        BuildPropertyGroup projectLevelProprtiesForInference;
        BuildPropertyGroup projectLevelPropertiesForExecution;
        Hashtable[] inferenceBucketItemsByName;
        Hashtable[] inferenceBucketMetaData;
        Hashtable projectLevelItemsForInference;
        Hashtable[] executionBucketItemsByName;
        Hashtable[] executionBucketMetaData;
        Hashtable projectLevelItemsForExecution;
        ITaskHost hostObject;
        MockTaskExecutionModule taskExecutionModule;
        EngineLoggingServicesHelper loggingHelper;
        string projectFileOfTaskNode;
        string parentProjectFullFileName;
        int nodeProxyId;
        int projectId;
        string executionDirectory;
        #endregion
    }
    #region TaskExecutionStateHelper
    /// <summary>
    /// Since we cannot override task engine inside of taskExecutionState we need to override some methods
    /// inside of teakexecutionstate which will allow us to replace the taskEngineCalls
    /// within taskExecutionState with some mock methods
    /// </summary>
    internal class TaskExecutionStateHelper : TaskExecutionState
    {
        bool targetInferenceSuccessful;

        // Sometimes we want to know what thread we are executing on
        int threadId;

        // Managed threadId where the execution method was run
        public int ThreadId
        {
            get { return threadId; }
        }

        public delegate void ExecuteTaskDelegate(object parameter);
        private object executionTaskDelegateParameter;

        public object ExecutionTaskDelegateParameter
        {
            get { return executionTaskDelegateParameter; }
            set { executionTaskDelegateParameter = value; }
        }

        private ExecuteTaskDelegate executeDelegate;

        public ExecuteTaskDelegate ExecuteDelegate
        {
            get { return executeDelegate; }
            set { executeDelegate = value; }
        }

        public bool TargetInferenceSuccessful
        {
            get { return targetInferenceSuccessful; }
            set { targetInferenceSuccessful = value; }
        }


        internal TaskExecutionStateHelper
            (
               TaskExecutionMode howToExecuteTask,
               Lookup lookupForInference,
               Lookup lookupForExecution,
               XmlElement taskXmlNode,
               ITaskHost hostObject,
               string projectFileOfTaskNode,
               string parentProjectFullFileName,
               string executionDirectory,
               int nodeProxyId
            )
            : base(howToExecuteTask,
                lookupForInference,
                lookupForExecution, 
                taskXmlNode,
                hostObject,
                projectFileOfTaskNode,
                parentProjectFullFileName,
                executionDirectory,
                nodeProxyId,
            null
            )
        {
            // Dont need to do anything
        }

        internal override void ExecuteTask()
        {
            threadId = Thread.CurrentThread.ManagedThreadId;
            if (executeDelegate == null)
            {
                base.ExecuteTask();
            }
            else
            {
                executeDelegate(executionTaskDelegateParameter);
            }
        }

        /// <summary>
        /// Mock out and override the method inside of TaskExecutionState which makes the calls to the task engine.
        /// </summary>
        internal override bool TaskEngineExecuteTask(TaskEngine taskEngine, TaskExecutionMode howToExecuteTask, Lookup lookup)
        {
            return targetInferenceSuccessful;
        }
    }
    #endregion

}
