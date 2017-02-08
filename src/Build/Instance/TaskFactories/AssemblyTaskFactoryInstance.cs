//-----------------------------------------------------------------------
// <copyright file="AssemblyTaskFactoryInstance.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>The assembly task factory Instance is used to wrap and construct tasks which are from .net assemblies.</summary>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.Reflection;
using System.IO;
using System.Xml;

using TaskLoggingContext = Microsoft.Build.BackEnd.Logging.TaskLoggingContext;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The assembly task factory is used to wrap and construct tasks which are from .net assemblies.
    /// </summary>
    internal class AssemblyTaskFactoryInstance : ITaskFactory
    {
        #region Data

        /// <summary>
        /// Name of the task wrapped by the task factory
        /// </summary>
        private string taskName = null;

        /// <summary>
        /// The appdomain the task will run in if it requires its own appdomain
        /// </summary>
        private AppDomain taskAppDomain = null;

        /// <summary>
        /// Does the task need to run in its own appdomain
        /// </summary>
        private bool separateAppDomain = false;

        /// <summary>
        /// Instance of the task which is wrapped by the task factory
        /// </summary>
        private ITask taskInstance = null;

        /// <summary>
        /// The loaded type (type, assembly name / file) of the task wrapped by the factory
        /// </summary>
        private LoadedType loadedType;
        #endregion

        /// <summary>
        /// We just need the loaded type information because the task factory which is not instance specific should 
        /// already have loaded the assembly and got the information.
        /// </summary>
        public AssemblyTaskFactoryInstance(string taskName, LoadedType loadedType)
        {
            ErrorUtilities.VerifyThrowArgumentLength(taskName, "taskName");
            ErrorUtilities.VerifyThrowArgumentNull(loadedType, "loadedType");

            this.taskName = taskName;
            this.loadedType = loadedType;
        }

        #region Properties

        /// <summary>
        /// Name of the factory. In this case the name is the assembly name which is wrapped by the factory
        /// </summary>
        public string FactoryName
        {
            get
            {
                return loadedType.Assembly.AssemblyLocation;
            }
        }

        #endregion

        #region Public Members

        /// <summary>
        /// Create an instance of the wrapped ITask for a run of the task in a batch
        /// </summary>
        public ITask CreateTask(IBuildEngine taskfactoryLoggingHost)
        {
            throw new NotImplementedException("Use Internal call for AssemblyFactories instead");
        }

        /// <summary>
        /// Create an instance of the wrapped ITask for a batch run of the task.
        /// </summary>
        public ITask CreateTaskInstance(ElementLocation taskLocation, TaskLoggingContext taskLoggingContext, AppDomainSetup appDomainSetup, bool isOutOfProc)
        {
            separateAppDomain = false;
            separateAppDomain = loadedType.HasLoadInSeparateAppDomainAttribute();

            taskAppDomain = null;

            if (separateAppDomain)
            {
                if (!loadedType.Type.IsMarshalByRef)
                {
                    taskLoggingContext.LogError
                    (
                        new BuildEventFileInfo(taskLocation),
                        "TaskNotMarshalByRef",
                        taskName
                     );

                    return null;
                }
                else
                {
                    // Our task depend on this name to be precisely that, so if you change it make sure
                    // you also change the checks in the tasks run in separate AppDomains. Better yet, just don't change it.

                    // Make sure we copy the appdomain configuration and send it to the appdomain we create so that if the creator of the current appdomain
                    // has done the binding redirection in code, that we will get those settings as well.
                    AppDomainSetup appDomainInfo = new AppDomainSetup();

                    // Get the current app domain setup settings
                    byte[] currentAppdomainBytes = appDomainSetup.GetConfigurationBytes();

                    // Apply the appdomain settings to the new appdomain before creating it
                    appDomainInfo.SetConfigurationBytes(currentAppdomainBytes);
                    taskAppDomain = AppDomain.CreateDomain(isOutOfProc ? "taskAppDomain (out-of-proc)" : "taskAppDomain (in-proc)", null, appDomainInfo);

                    // Hook up last minute dumping of any exceptions 
                    taskAppDomain.UnhandledException += new UnhandledExceptionEventHandler(ExceptionHandling.UnhandledExceptionHandler);
                }
            }

            // instantiate the task in given domain
            if (taskAppDomain == null || taskAppDomain == AppDomain.CurrentDomain)
            {
                // perf improvement for the same appdomain case - we already have the type object
                // and don't want to go through reflection to recreate it from the name.
                taskInstance = (ITask)Activator.CreateInstance(loadedType.Type);

                return taskInstance;
            }

            if (loadedType.Assembly.AssemblyFile != null)
            {
                taskInstance = (ITask)taskAppDomain.CreateInstanceFromAndUnwrap(loadedType.Assembly.AssemblyFile, loadedType.Type.FullName);

                // this will force evaluation of the task class type and try to load the task assembly
                Type taskType = taskInstance.GetType();

                // If the types don't match, we have a problem. It means that our AppDomain was able to load
                // a task assembly using Load, and loaded a different one. I don't see any other choice than
                // to fail here.
                if (taskType != loadedType.Type)
                {
                    taskLoggingContext.LogError
                    (
                    new BuildEventFileInfo(taskLocation),
                    "ConflictingTaskAssembly",
                    loadedType.Assembly.AssemblyFile,
                    loadedType.Type.Assembly.Location
                    );

                    taskInstance = null;
                }
            }
            else
            {
                taskInstance = (ITask)taskAppDomain.CreateInstanceAndUnwrap(loadedType.Type.Assembly.FullName, loadedType.Type.FullName);
            }

            return taskInstance;
        }

        /// <summary>
        /// Given an instantiated task, this helper method sets the specified parameter.
        /// All exceptions from this method will be caught in the taskExecution host and logged as a fatal task error
        /// </summary>
        /// <returns>true, if successful</returns>
        public bool SetTaskParameterValue
        (
            PropertyInfo parameter,
            object parameterValue
        )
        {
            parameter.SetValue(taskInstance, parameterValue, null);
            return true;
        }

        /// <summary>
        /// Given a task parameter get its value;
        /// </summary>
        public object GetTaskParameterValue(PropertyInfo parameter)
        {
            return parameter.GetValue(taskInstance, null);
        }

         /// <summary>
        /// Release any context information related to CreateInstanceForBatch. The task should hold no state
        /// between batches.
        /// </summary>
        public void CleanupTask()
        {
            if (separateAppDomain && taskAppDomain != null)
            {
                AppDomain.Unload(taskAppDomain);
                taskAppDomain = null;
            }
        }
        #endregion
    }
}
