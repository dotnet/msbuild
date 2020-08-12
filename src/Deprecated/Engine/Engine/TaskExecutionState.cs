// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is a wrapper used to contain the data needed to execute a task. This class
    /// is initially instantiated on the engine side by the scheduler and submitted to the node.
    /// The node completes the class instantiating by providing the object with node side data.
    /// This class is distinct from the task engine in that it (possibly) travels cross process
    /// between the engine and the node carrying with it the data needed to instantiate the task
    /// engine. The task engine can't subsume this class because the task engine is bound to the 
    /// node process and can't travel cross process.
    /// </summary>
    internal class TaskExecutionState
    {
        #region Constructors
        /// <summary>
        /// The constructor obtains the state information and the
        /// callback delegate.
        /// </summary>
        internal TaskExecutionState
        (
            TaskExecutionMode howToExecuteTask,
            Lookup lookupForInference,
            Lookup lookupForExecution,
            XmlElement taskXmlNode,
            ITaskHost hostObject,
            string projectFileOfTaskNode,
            string parentProjectFullFileName,
            string executionDirectory,
            int handleId,
            BuildEventContext buildEventContext
        )
        {
            ErrorUtilities.VerifyThrow(taskXmlNode != null, "Must have task node");

            this.howToExecuteTask = howToExecuteTask;
            this.lookupForInference = lookupForInference;
            this.lookupForExecution = lookupForExecution;
            this.hostObject = hostObject;
            this.projectFileOfTaskNode = projectFileOfTaskNode;
            this.parentProjectFullFileName = parentProjectFullFileName;
            this.executionDirectory = executionDirectory;
            this.handleId = handleId;
            this.buildEventContext = buildEventContext;
            this.taskXmlNode = taskXmlNode;
        }
        #endregion

        #region Properties

        internal int HandleId
        {
            get
            {
                return this.handleId;
            }
            set
            {
                this.handleId = value;
            }
        }

        internal EngineLoggingServices LoggingService
        {
            get
            {
                return this.loggingService;
            }
            set
            {
                this.loggingService = value;
            }
        }

        internal TaskExecutionModule ParentModule
        {
            get
            {
                return this.parentModule;
            }
            set
            {
                this.parentModule = value;
            }
        }

        internal string ExecutionDirectory
        {
            get
            {
                return this.executionDirectory;
            }
        }

        internal bool ProfileExecution
        {
            get
            {
                return this.profileExecution;
            }
            set
            {
                this.profileExecution = value;
            }
        }

        #endregion

        #region Methods
        /// <summary>
        /// The thread procedure executes the tasks and calls callback once it is done
        /// </summary>
        virtual internal void ExecuteTask()
        {
            bool taskExecutedSuccessfully = true;

            Exception thrownException = null;
            bool dontPostOutputs = false;

            if (profileExecution)
            {
                startTime = DateTime.Now.Ticks;
            }

            try
            {
                TaskEngine taskEngine = new TaskEngine(
                            taskXmlNode,
                            hostObject,
                            projectFileOfTaskNode,
                            parentProjectFullFileName,
                            loggingService,
                            handleId,
                            parentModule,
                            buildEventContext);

                // Set the directory to the one appropriate for the task
                if (FileUtilities.GetCurrentDirectoryStaticBuffer(currentDirectoryBuffer) != executionDirectory)
                {
                    Directory.SetCurrentDirectory(executionDirectory);
                }
                // if we're skipping task execution because the target is up-to-date, we
                // need to go ahead and infer all the outputs that would have been emitted;
                // alternatively, if we're doing an incremental build, we need to infer the
                // outputs that would have been produced if all the up-to-date items had
                // been built by the task
                if ((howToExecuteTask & TaskExecutionMode.InferOutputsOnly) != TaskExecutionMode.Invalid)
                {
                    bool targetInferenceSuccessful = TaskEngineExecuteTask
                        (taskEngine,
                         TaskExecutionMode.InferOutputsOnly,
                         lookupForInference);

                    ErrorUtilities.VerifyThrow(targetInferenceSuccessful, "A task engine should never fail to infer its task's up-to-date outputs.");
                }

                // execute the task using the items that need to be (re)built
                if ((howToExecuteTask & TaskExecutionMode.ExecuteTaskAndGatherOutputs) != TaskExecutionMode.Invalid)
                {
                    taskExecutedSuccessfully =
                      TaskEngineExecuteTask
                        (   taskEngine,
                            TaskExecutionMode.ExecuteTaskAndGatherOutputs,
                            lookupForExecution
                        );
                }
            }
            // We want to catch all exceptions and pass them on to the engine
            catch (Exception e)
            {
                thrownException = e;
                taskExecutedSuccessfully = false;

                // In single threaded mode the exception can be thrown on the current thread
                if (parentModule.RethrowTaskExceptions())
                {
                    dontPostOutputs = true;
                    throw;
                }
            }
            finally
            {
                if (!dontPostOutputs)
                {
                    long executionTime = profileExecution ? DateTime.Now.Ticks - startTime : 0;
                    // Post the outputs to the engine
                    parentModule.PostTaskOutputs(handleId, taskExecutedSuccessfully, thrownException, executionTime);
                }
            }
        }

        /// <summary>
        /// This method is called to adjust the execution time for the task by subtracting the time
        /// spent waiting for results
        /// </summary>
        /// <param name="entryTime"></param>
        internal void NotifyOfWait(long waitStartTime)
        {
            // Move the start time forward by the period of the wait
            startTime += (DateTime.Now.Ticks - waitStartTime);
        }

        #region MethodsNeededForUnitTesting
        /// <summary>
        /// Since we could not derrive from TaskEngine and have no Interface, we need to overide the method in here and 
        /// replace the calls when testing the class because of the calls to TaskEngine. If at a future time we get a mock task 
        /// engine, Interface or a non sealed TaskEngine these methods can disappear.
        /// </summary>
        /// <returns></returns>
        virtual internal bool TaskEngineExecuteTask(
            TaskEngine taskEngine,
            TaskExecutionMode howTaskShouldBeExecuted,
            Lookup lookup
        )
        {
            return taskEngine.ExecuteTask
                 (
                     howTaskShouldBeExecuted,
                     lookup
                 );
        }
        #endregion
  
        #endregion

        #region Fields set by the Engine thread

        private TaskExecutionMode howToExecuteTask;

        private Lookup lookupForInference;
        private Lookup lookupForExecution;
        
        private ITaskHost hostObject;
        private string projectFileOfTaskNode;
        private string parentProjectFullFileName;
        private string executionDirectory;
        private int handleId;
        private BuildEventContext buildEventContext;

        #endregion

        #region Fields set by the Task thread

        private TaskExecutionModule parentModule;
        private EngineLoggingServices loggingService;
        private XmlElement taskXmlNode;
        private long startTime;
        private bool profileExecution;
        private static StringBuilder currentDirectoryBuffer = new StringBuilder(270);

        #endregion
    }
}
