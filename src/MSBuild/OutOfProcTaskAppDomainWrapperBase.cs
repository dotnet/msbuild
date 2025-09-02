// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
#if FEATURE_APPDOMAIN
using System.Threading;
#endif
using System.Reflection;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// Class for executing a task in an AppDomain
    /// </summary>
    [Serializable]
    internal class OutOfProcTaskAppDomainWrapperBase
#if FEATURE_APPDOMAIN
        : MarshalByRefObject
#endif
    {
        /// <summary>
        /// This is the actual user task whose instance we will create and invoke Execute
        /// </summary>
        private ITask wrappedTask;

#if FEATURE_APPDOMAIN
        /// <summary>
        /// This is an appDomain instance if any is created for running this task
        /// </summary>
        /// <comments>
        /// TaskAppDomain's non-serializability should never be an issue since even if we start running the wrapper
        /// in a separate appdomain, we will not be trying to load the task on one side of the serialization
        /// boundary and run it on the other.
        /// </comments>
        [NonSerialized]
        private AppDomain _taskAppDomain;
#endif

        /// <summary>
        /// Need to keep the build engine around in order to log from the task loader.
        /// </summary>
        private IBuildEngine buildEngine;

        /// <summary>
        /// Need to keep track of the task name also so that we can log valid information
        /// from the task loader.
        /// </summary>
        private string taskName;

        /// <summary>
        /// This is the actual user task whose instance we will create and invoke Execute
        /// </summary>
        public ITask WrappedTask
        {
            get { return wrappedTask; }
        }

        /// <summary>
        /// We have a cancel already requested
        /// This can happen before we load the module and invoke execute.
        /// </summary>
        internal bool CancelPending
        {
            get;
            set;
        }

        /// <summary>
        /// This is responsible for invoking Execute on the Task
        /// Any method calling ExecuteTask must remember to call CleanupTask
        /// </summary>
        /// <remarks>
        /// We also allow the Task to have a reference to the BuildEngine by design
        /// at ITask.BuildEngine
        /// </remarks>
        /// <param name="oopTaskHostNode">The OutOfProcTaskHostNode as the BuildEngine</param>
        /// <param name="taskName">The name of the task to be executed</param>
        /// <param name="taskLocation">The path of the task binary</param>
        /// <param name="taskFile">The path to the project file in which the task invocation is located.</param>
        /// <param name="taskLine">The line in the project file where the task invocation is located.</param>
        /// <param name="taskColumn">The column in the project file where the task invocation is located.</param>
        /// <param name="appDomainSetup">The AppDomainSetup that we want to use to launch our AppDomainIsolated tasks</param>
        /// <param name="taskParams">Parameters that will be passed to the task when created</param>
        /// <returns>Task completion result showing success, failure or if there was a crash</returns>
        internal OutOfProcTaskHostTaskResult ExecuteTask(
                IBuildEngine oopTaskHostNode,
                string taskName,
                string taskLocation,
                string taskFile,
                int taskLine,
                int taskColumn,
#if FEATURE_APPDOMAIN
                AppDomainSetup appDomainSetup,
#endif
                IDictionary<string, TaskParameter> taskParams)
        {
            buildEngine = oopTaskHostNode;
            this.taskName = taskName;

#if FEATURE_APPDOMAIN
            _taskAppDomain = null;
#endif
            wrappedTask = null;

            LoadedType taskType = null;
            try
            {
                TypeLoader typeLoader = new TypeLoader(TaskLoader.IsTaskClass);
                taskType = typeLoader.Load(taskName, AssemblyLoadInfo.Create(null, taskLocation), false);
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                // If it's a TargetInvocationException, we only care about the contents of the inner exception,
                // so just save that instead.
                Exception exceptionToReturn = e is TargetInvocationException ? e.InnerException : e;

                return new OutOfProcTaskHostTaskResult(
                                TaskCompleteType.CrashedDuringInitialization,
                                exceptionToReturn,
                                "TaskInstantiationFailureError",
                                new string[] { taskName, taskLocation, String.Empty });
            }

            OutOfProcTaskHostTaskResult taskResult;
            if (taskType.HasSTAThreadAttribute)
            {
#if FEATURE_APARTMENT_STATE
                taskResult = InstantiateAndExecuteTaskInSTAThread(oopTaskHostNode, taskType, taskName, taskLocation, taskFile, taskLine, taskColumn,
#if FEATURE_APPDOMAIN
                    appDomainSetup,
#endif
                    taskParams);
#else
                return new OutOfProcTaskHostTaskResult(
                                                    TaskCompleteType.CrashedDuringInitialization,
                                                    null,
                                                    "TaskInstantiationFailureNotSupported",
                                                    new string[] { taskName, taskLocation, typeof(RunInSTAAttribute).FullName });
#endif
            }
            else
            {
                taskResult = InstantiateAndExecuteTask(oopTaskHostNode, taskType, taskName, taskLocation, taskFile, taskLine, taskColumn,
#if FEATURE_APPDOMAIN
                    appDomainSetup,
#endif
                    taskParams);
            }

            return taskResult;
        }

        /// <summary>
        /// This is responsible for cleaning up the task after the OutOfProcTaskHostNode has gathered everything it needs from this execution
        /// For example: We will need to hold on new AppDomains created until we finish getting all outputs from the task
        /// Add any other cleanup tasks here. Any method calling ExecuteTask must remember to call CleanupTask.
        /// </summary>
        internal void CleanupTask()
        {
#if FEATURE_APPDOMAIN
            if (_taskAppDomain != null)
            {
                AppDomain.Unload(_taskAppDomain);
            }

            TaskLoader.RemoveAssemblyResolver();
#endif
            wrappedTask = null;
        }

#if FEATURE_APARTMENT_STATE
        /// <summary>
        /// Execute a task on the STA thread.
        /// </summary>
        /// <comment>
        /// STA thread launching code lifted from XMakeBuildEngine\BackEnd\Components\RequestBuilder\TaskBuilder.cs, ExecuteTaskInSTAThread method.
        /// Any bug fixes made to this code, please ensure that you also fix that code.
        /// </comment>
        private OutOfProcTaskHostTaskResult InstantiateAndExecuteTaskInSTAThread(
                IBuildEngine oopTaskHostNode,
                LoadedType taskType,
                string taskName,
                string taskLocation,
                string taskFile,
                int taskLine,
                int taskColumn,
#if FEATURE_APPDOMAIN
                AppDomainSetup appDomainSetup,
#endif
                IDictionary<string, TaskParameter> taskParams)
        {
            ManualResetEvent taskRunnerFinished = new ManualResetEvent(false);
            OutOfProcTaskHostTaskResult taskResult = null;
            Exception exceptionFromExecution = null;

            try
            {
                ThreadStart taskRunnerDelegate = delegate ()
                {
                    try
                    {
                        taskResult = InstantiateAndExecuteTask(
                                                oopTaskHostNode,
                                                taskType,
                                                taskName,
                                                taskLocation,
                                                taskFile,
                                                taskLine,
                                                taskColumn,
#if FEATURE_APPDOMAIN
                                                appDomainSetup,
#endif
                                                taskParams);
                    }
                    catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                    {
                        exceptionFromExecution = e;
                    }
                    finally
                    {
                        taskRunnerFinished.Set();
                    }
                };

                Thread staThread = new Thread(taskRunnerDelegate);
                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Name = "MSBuild STA task runner thread";
                staThread.CurrentCulture = Thread.CurrentThread.CurrentCulture;
                staThread.CurrentUICulture = Thread.CurrentThread.CurrentUICulture;
                staThread.Start();

                // TODO: Why not just Join on the thread???
                taskRunnerFinished.WaitOne();
            }
            finally
            {
#if CLR2COMPATIBILITY
                taskRunnerFinished.Close();
#else
                taskRunnerFinished.Dispose();
#endif
                taskRunnerFinished = null;
            }

            if (exceptionFromExecution != null)
            {
                // Unfortunately this will reset the callstack
                throw exceptionFromExecution;
            }

            return taskResult;
        }
#endif

        /// <summary>
        /// Do the work of actually instantiating and running the task.
        /// </summary>
        private OutOfProcTaskHostTaskResult InstantiateAndExecuteTask(
                IBuildEngine oopTaskHostNode,
                LoadedType taskType,
                string taskName,
                string taskLocation,
                string taskFile,
                int taskLine,
                int taskColumn,
#if FEATURE_APPDOMAIN
                AppDomainSetup appDomainSetup,
#endif
                IDictionary<string, TaskParameter> taskParams)
        {
#if FEATURE_APPDOMAIN
            _taskAppDomain = null;
#endif
            wrappedTask = null;

            try
            {
#pragma warning disable SA1111, SA1009 // Closing parenthesis should be on line of last parameter
                wrappedTask = TaskLoader.CreateTask(
                    taskType,
                    taskName,
                    taskFile,
                    taskLine,
                    taskColumn,
                    new TaskLoader.LogError(LogErrorDelegate),
#if FEATURE_APPDOMAIN
                    appDomainSetup,
                    // custom app domain assembly loading won't be available for task host
                    null,
#endif
                    true /* always out of proc */
#if FEATURE_APPDOMAIN
                    , out _taskAppDomain
#endif
                    );
#pragma warning restore SA1111, SA1009 // Closing parenthesis should be on line of last parameter
                wrappedTask.BuildEngine = oopTaskHostNode;
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                Exception exceptionToReturn = e;

                // If it's a TargetInvocationException, we only care about the contents of the inner exception,
                // so just save that instead.
                if (e is TargetInvocationException)
                {
                    exceptionToReturn = e.InnerException;
                }

                return new OutOfProcTaskHostTaskResult(
                    TaskCompleteType.CrashedDuringInitialization,
                    exceptionToReturn,
                    "TaskInstantiationFailureError",
                    new string[] { taskName, taskLocation, String.Empty });
            }

            foreach (KeyValuePair<string, TaskParameter> param in taskParams)
            {
                try
                {
                    PropertyInfo paramInfo = wrappedTask.GetType().GetProperty(param.Key, BindingFlags.Instance | BindingFlags.Public);
                    paramInfo.SetValue(wrappedTask, param.Value?.WrappedParameter, null);
                }
                catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                {
                    return new OutOfProcTaskHostTaskResult(
                                TaskCompleteType.CrashedDuringInitialization,
                                // If it's a TargetInvocationException, we only care about the contents of the inner exception, so save that instead.
                                e is TargetInvocationException ? e.InnerException : e,
                                "InvalidTaskAttributeError",
                                new string[] { param.Key, param.Value.ToString(), taskName });
                }
            }

            bool success = false;
            try
            {
                if (CancelPending)
                {
                    return new OutOfProcTaskHostTaskResult(TaskCompleteType.Failure);
                }

                // If it didn't crash and return before now, we're clear to go ahead and execute here.
                success = wrappedTask.Execute();
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                return new OutOfProcTaskHostTaskResult(TaskCompleteType.CrashedDuringExecution, e);
            }

            PropertyInfo[] finalPropertyValues = wrappedTask.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);

            IDictionary<string, Object> finalParameterValues = new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase);
            foreach (PropertyInfo value in finalPropertyValues)
            {
                // only record outputs
                if (value.GetCustomAttributes(typeof(OutputAttribute), true).Length > 0)
                {
                    try
                    {
                        finalParameterValues[value.Name] = value.GetValue(wrappedTask, null);
                    }
                    catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                    {
                        // If it's not a critical exception, we assume there's some sort of problem in the parameter getter --
                        // so save the exception, and we'll re-throw once we're back on the main node side of the
                        // communications pipe.
                        finalParameterValues[value.Name] = e;
                    }
                }
            }

            return new OutOfProcTaskHostTaskResult(success ? TaskCompleteType.Success : TaskCompleteType.Failure, finalParameterValues);
        }

        /// <summary>
        /// Logs errors from TaskLoader
        /// </summary>
        private void LogErrorDelegate(string taskLocation, int taskLine, int taskColumn, string message, params object[] messageArgs)
        {
            buildEngine.LogErrorEvent(new BuildErrorEventArgs(
                                                    null,
                                                    null,
                                                    taskLocation,
                                                    taskLine,
                                                    taskColumn,
                                                    0,
                                                    0,
                                                    ResourceUtilities.FormatString(AssemblyResources.GetString(message), messageArgs),
                                                    null,
                                                    taskName));
        }
    }
}
