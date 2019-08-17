// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

using ElementLocation = Microsoft.Build.Construction.ElementLocation;
using TargetLoggingContext = Microsoft.Build.BackEnd.Logging.TargetLoggingContext;
using TaskLoggingContext = Microsoft.Build.BackEnd.Logging.TaskLoggingContext;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The assembly task factory is used to wrap and construct tasks which are from .net assemblies.
    /// </summary>
    internal class AssemblyTaskFactory : ITaskFactory2
    {
        #region Data

        /// <summary>
        /// The type loader to load types which derrive from ITask or ITask2
        /// </summary>
        private readonly TypeLoader _typeLoader = new TypeLoader(TaskLoader.IsTaskClass);

        /// <summary>
        /// Name of the task wrapped by the task factory
        /// </summary>
        private string _taskName = null;

        /// <summary>
        /// The loaded type (type, assembly name / file) of the task wrapped by the factory
        /// </summary>
        private LoadedType _loadedType;

#if FEATURE_APPDOMAIN
        /// <summary>
        /// A cache of tasks and the AppDomains they are loaded in.
        /// </summary>
        private Dictionary<ITask, AppDomain> _tasksAndAppDomains = new Dictionary<ITask, AppDomain>();
#endif

        /// <summary>
        ///  the set of parameters owned by this particular task host
        /// </summary>
        private IDictionary<string, string> _factoryIdentityParameters;

        /// <summary>
        /// Tracks whether, in the UsingTask invocation, we were specifically asked to use 
        /// the task host.  If so, that overrides all other concerns, and we will launch 
        /// the task host even if the requested runtime / architecture match that of the 
        /// current MSBuild process. 
        /// </summary>
        private bool _taskHostFactoryExplicitlyRequested;

        /// <summary>
        /// Need to store away the taskloggingcontext used by CreateTaskInstance so that 
        /// TaskLoader will be able to call back with errors.  
        /// </summary>
        private TaskLoggingContext _taskLoggingContext;

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyTaskFactory"/> class.
        /// </summary>
        internal AssemblyTaskFactory()
        {
        }

        #region Public Members

        /// <summary>
        /// Name of the factory. In this case the name is the assembly name which is wrapped by the factory
        /// </summary>
        public string FactoryName
        {
            get
            {
                return _loadedType.Assembly.AssemblyLocation;
            }
        }

        /// <summary>
        /// Gets the type of task this factory creates.
        /// </summary>
        public Type TaskType
        {
            get { return _loadedType.Type; }
        }

        /// <summary>
        /// Initializes this factory for instantiating tasks with a particular inline task block.
        /// </summary>
        /// <param name="taskName">Name of the task.</param>
        /// <param name="parameterGroup">The parameter group.</param>
        /// <param name="taskBody">The task body.</param>
        /// <param name="taskFactoryLoggingHost">The task factory logging host.</param>
        /// <returns>A value indicating whether initialization was successful.</returns>
        /// <remarks>
        /// <para>MSBuild engine will call this to initialize the factory. This should initialize the factory enough so that the factory can be asked
        /// whether or not task names can be created by the factory.</para>
        /// <para>
        /// The taskFactoryLoggingHost will log messages in the context of the target where the task is first used.
        /// </para>
        /// </remarks>
        public bool Initialize(string taskName, IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody, IBuildEngine taskFactoryLoggingHost)
        {
            ErrorUtilities.ThrowInternalError("Use internal call to properly initialize the assembly task factory");
            return false;
        }

        /// <summary>
        /// Initializes this factory for instantiating tasks with a particular inline task block and a set of UsingTask parameters.
        /// </summary>
        /// <param name="taskName">Name of the task.</param>
        /// <param name="factoryIdentityParameters">Special parameters that the task factory can use to modify how it executes tasks, 
        /// such as Runtime and Architecture.  The key is the name of the parameter and the value is the parameter's value. This 
        /// is the set of parameters that was set on the UsingTask using e.g. the UsingTask Runtime and Architecture parameters.</param>
        /// <param name="parameterGroup">The parameter group.</param>
        /// <param name="taskBody">The task body.</param>
        /// <param name="taskFactoryLoggingHost">The task factory logging host.</param>
        /// <returns>A value indicating whether initialization was successful.</returns>
        /// <remarks>
        /// <para>MSBuild engine will call this to initialize the factory. This should initialize the factory enough so that the 
        /// factory can be asked whether or not task names can be created by the factory.  If a task factory implements ITaskFactory2, 
        /// this Initialize method will be called in place of ITaskFactory.Initialize.</para>
        /// <para>
        /// The taskFactoryLoggingHost will log messages in the context of the target where the task is first used.
        /// </para>
        /// </remarks>
        public bool Initialize(string taskName, IDictionary<string, string> factoryIdentityParameters, IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody, IBuildEngine taskFactoryLoggingHost)
        {
            ErrorUtilities.ThrowInternalError("Use internal call to properly initialize the assembly task factory");
            return false;
        }

        /// <summary>
        /// Get a list of parameters for the task.
        /// </summary>
        public TaskPropertyInfo[] GetTaskParameters()
        {
            PropertyInfo[] infos = _loadedType.Type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            var propertyInfos = new TaskPropertyInfo[infos.Length];
            for (int i = 0; i < infos.Length; i++)
            {
                propertyInfos[i] = new ReflectableTaskPropertyInfo(infos[i]);
            }

            return propertyInfos;
        }

        /// <summary>
        /// Create an instance of the task to be used.
        /// The task factory logging host will log messages in the context of the task.
        /// </summary>
        /// <param name="taskFactoryLoggingHost">
        /// The task factory logging host will log messages in the context of the task.
        /// </param>
        /// <returns>
        /// The generated task, or <c>null</c> if the task failed to be created.
        /// </returns>
        public ITask CreateTask(IBuildEngine taskFactoryLoggingHost)
        {
            ErrorUtilities.ThrowInternalError("Use internal call to properly create a task instance from the assembly task factory");
            return null;
        }

        /// <summary>
        /// Create an instance of the task to be used.
        /// </summary>
        /// <param name="taskFactoryLoggingHost">
        /// The task factory logging host will log messages in the context of the task.
        /// </param>
        /// <param name="taskIdentityParameters">
        /// Special parameters that the task factory can use to modify how it executes tasks, such as Runtime and Architecture.  
        /// The key is the name of the parameter and the value is the parameter's value.  This is the set of parameters that was 
        /// set to the task invocation itself, via e.g. the special MSBuildRuntime and MSBuildArchitecture parameters.  
        /// </param>
        /// <remarks>
        /// If a task factory implements ITaskFactory2, MSBuild will call this method instead of ITaskFactory.CreateTask.  
        /// </remarks>
        /// <returns>
        /// The generated task, or <c>null</c> if the task failed to be created.
        /// </returns>
        public ITask CreateTask(IBuildEngine taskFactoryLoggingHost, IDictionary<string, string> taskIdentityParameters)
        {
            ErrorUtilities.ThrowInternalError("Use internal call to properly create a task instance from the assembly task factory");
            return null;
        }

        /// <summary>
        /// Cleans up any context or state that may have been built up for a given task.
        /// </summary>
        /// <param name="task">The task to clean up.</param>
        /// <remarks>
        /// For many factories, this method is a no-op.  But some factories may have built up
        /// an AppDomain as part of an individual task instance, and this is their opportunity
        /// to shutdown the AppDomain.
        /// </remarks>
        public void CleanupTask(ITask task)
        {
            ErrorUtilities.VerifyThrowArgumentNull(task, "task");
#if FEATURE_APPDOMAIN
            AppDomain appDomain;
            if (_tasksAndAppDomains.TryGetValue(task, out appDomain))
            {
                _tasksAndAppDomains.Remove(task);

                if (appDomain != null)
                {
                    // Unload the AppDomain asynchronously to avoid a deadlock that can happen because
                    // AppDomain.Unload blocks for the process's one Finalizer thread to finalize all
                    // objects. Some objects are RCWs for STA COM objects and as such would need the
                    // VS main thread to be processing messages in order to finalize. But if the main thread
                    // is blocked in a non-pumping wait waiting for this build request to complete, we would
                    // deadlock. By unloading asynchronously, the AppDomain unload can block till the main
                    // thread is available, even if it isn't available until after this MSBuild Task has
                    // finished executing.
                    Task.Run(() => AppDomain.Unload(appDomain));
                }
            }
#endif

            TaskHostTask taskAsTaskHostTask = task as TaskHostTask;
            if (taskAsTaskHostTask != null)
            {
                taskAsTaskHostTask.Cleanup();
            }
            else
            {
#if FEATURE_APPDOMAIN
                // It's really not necessary to do it for TaskHostTasks
                TaskLoader.RemoveAssemblyResolver();
#endif
            }
        }

        #endregion

        #region Internal Members

        /// <summary>
        /// Initialize the factory from the task registry
        /// </summary>
        internal LoadedType InitializeFactory
            (
                AssemblyLoadInfo loadInfo,
                string taskName,
                IDictionary<string, TaskPropertyInfo> taskParameters,
                string taskElementContents,
                IDictionary<string, string> taskFactoryIdentityParameters,
                bool taskHostFactoryExplicitlyRequested,
                TargetLoggingContext targetLoggingContext,
                ElementLocation elementLocation,
                string taskProjectFile
            )
        {
            ErrorUtilities.VerifyThrowArgumentNull(loadInfo, "loadInfo");
            VerifyThrowIdentityParametersValid(taskFactoryIdentityParameters, elementLocation, taskName, "Runtime", "Architecture");

            if (taskFactoryIdentityParameters != null)
            {
                _factoryIdentityParameters = new Dictionary<string, string>(taskFactoryIdentityParameters, StringComparer.OrdinalIgnoreCase);
            }

            _taskHostFactoryExplicitlyRequested = taskHostFactoryExplicitlyRequested;

            try
            {
                ErrorUtilities.VerifyThrowArgumentLength(taskName, "taskName");
                _taskName = taskName;
                _loadedType = _typeLoader.Load(taskName, loadInfo);
                ProjectErrorUtilities.VerifyThrowInvalidProject(_loadedType != null, elementLocation, "TaskLoadFailure", taskName, loadInfo.AssemblyLocation, String.Empty);
            }
            catch (TargetInvocationException e)
            {
                // Exception thrown by the called code itself
                // Log the stack, so the task vendor can fix their code
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, elementLocation, "TaskLoadFailure", taskName, loadInfo.AssemblyLocation, Environment.NewLine + e.InnerException.ToString());
            }
            catch (ReflectionTypeLoadException e)
            {
                // ReflectionTypeLoadException.LoaderExceptions may contain nulls
                foreach (Exception exception in e.LoaderExceptions)
                {
                    if (exception != null)
                    {
                        targetLoggingContext.LogError(new BuildEventFileInfo(taskProjectFile), "TaskLoadFailure", taskName, loadInfo.AssemblyLocation, exception.Message);
                    }
                }

                ProjectErrorUtilities.VerifyThrowInvalidProject(false, elementLocation, "TaskLoadFailure", taskName, loadInfo.AssemblyLocation, e.Message);
            }
            catch (ArgumentNullException e)
            {
                // taskName may be null
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, elementLocation, "TaskLoadFailure", taskName, loadInfo.AssemblyLocation, e.Message);
            }
            catch (Exception e) // Catching Exception, but rethrowing unless it's a well-known exception.
            {
                if (ExceptionHandling.NotExpectedReflectionException(e))
                {
                    throw;
                }

                ProjectErrorUtilities.VerifyThrowInvalidProject(false, elementLocation, "TaskLoadFailure", taskName, loadInfo.AssemblyLocation, e.Message);
            }

            return _loadedType;
        }

        /// <summary>
        /// Create an instance of the wrapped ITask for a batch run of the task.
        /// </summary>
        internal ITask CreateTaskInstance(ElementLocation taskLocation, TaskLoggingContext taskLoggingContext, IBuildComponentHost buildComponentHost, IDictionary<string, string> taskIdentityParameters,
#if FEATURE_APPDOMAIN
            AppDomainSetup appDomainSetup,
#endif
            bool isOutOfProc)
        {
            bool useTaskFactory = false;
            IDictionary<string, string> mergedParameters = null;
            _taskLoggingContext = taskLoggingContext;

            // Optimization for the common (vanilla AssemblyTaskFactory) case -- only calculate 
            // the task factory parameters if we have any to calculate; otherwise even if we 
            // still launch the task factory, it will be with parameters corresponding to the 
            // current process. 
            if ((_factoryIdentityParameters != null && _factoryIdentityParameters.Count > 0) || (taskIdentityParameters != null && taskIdentityParameters.Count > 0))
            {
                VerifyThrowIdentityParametersValid(taskIdentityParameters, taskLocation, _taskName, "MSBuildRuntime", "MSBuildArchitecture");

                mergedParameters = MergeTaskFactoryParameterSets(_factoryIdentityParameters, taskIdentityParameters);
                useTaskFactory = !NativeMethodsShared.IsMono
                                 && (_taskHostFactoryExplicitlyRequested
                                     || !TaskHostParametersMatchCurrentProcess(mergedParameters));
            }
            else
            {
                // if we don't have any task host parameters specified on either the using task or the 
                // task invocation, then we will run in-proc UNLESS "TaskHostFactory" is explicitly specified
                // as the task factory.  
                useTaskFactory = _taskHostFactoryExplicitlyRequested;
            }

            if (useTaskFactory)
            {
                ErrorUtilities.VerifyThrowInternalNull(buildComponentHost, "buildComponentHost");

                mergedParameters = mergedParameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                string runtime = null;
                string architecture = null;

                if (!mergedParameters.TryGetValue(XMakeAttributes.runtime, out runtime))
                {
                    mergedParameters[XMakeAttributes.runtime] = XMakeAttributes.MSBuildRuntimeValues.clr4;
                }

                if (!mergedParameters.TryGetValue(XMakeAttributes.architecture, out architecture))
                {
                    mergedParameters[XMakeAttributes.architecture] = XMakeAttributes.GetCurrentMSBuildArchitecture();
                }

                TaskHostTask task = new TaskHostTask(taskLocation, taskLoggingContext, buildComponentHost, mergedParameters, _loadedType
#if FEATURE_APPDOMAIN
                    , appDomainSetup
#endif
                    );
                return task;
            }
            else
            {
#if FEATURE_APPDOMAIN
                AppDomain taskAppDomain = null;
#endif

                ITask taskInstance = TaskLoader.CreateTask(_loadedType, _taskName, taskLocation.File, taskLocation.Line, taskLocation.Column, new TaskLoader.LogError(ErrorLoggingDelegate)
#if FEATURE_APPDOMAIN
                    , appDomainSetup
#endif
                    , isOutOfProc
#if FEATURE_APPDOMAIN
                    , out taskAppDomain
#endif
                    );

#if FEATURE_APPDOMAIN
                if (taskAppDomain != null)
                {
                    _tasksAndAppDomains[taskInstance] = taskAppDomain;
                }
#endif

                return taskInstance;
            }
        }

        /// <summary>
        /// Is the given task name able to be created by the task factory. In the case of an assembly task factory 
        /// this question is answered by checking the assembly wrapped by the task factory to see if it exists. 
        /// </summary>
        internal bool TaskNameCreatableByFactory(string taskName, IDictionary<string, string> taskIdentityParameters, string taskProjectFile, TargetLoggingContext targetLoggingContext, ElementLocation elementLocation)
        {
            if (!TaskIdentityParametersMatchFactory(_factoryIdentityParameters, taskIdentityParameters))
            {
                return false;
            }

            // Parameters match, so now we check to see if the task exists. 
            LoadedType taskClass = null;
            try
            {
                ErrorUtilities.VerifyThrowArgumentLength(taskName, "TaskName");
                taskClass = _typeLoader.ReflectionOnlyLoad(taskName, _loadedType.Assembly);
                if (taskClass != null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (TargetInvocationException e)
            {
                // Exception thrown by the called code itself
                // Log the stack, so the task vendor can fix their code
                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "TaskLoadFailure", taskName, _loadedType.Assembly.AssemblyLocation, Environment.NewLine + e.InnerException.ToString());
            }
            catch (ReflectionTypeLoadException e)
            {
                // ReflectionTypeLoadException.LoaderExceptions may contain nulls
                foreach (Exception exception in e.LoaderExceptions)
                {
                    if (exception != null)
                    {
                        targetLoggingContext.LogError(new BuildEventFileInfo(taskProjectFile), "TaskLoadFailure", taskName, _loadedType.Assembly.AssemblyLocation, exception.Message);
                    }
                }

                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "TaskLoadFailure", taskName, _loadedType.Assembly.AssemblyLocation, e.Message);
            }
            catch (ArgumentNullException e)
            {
                // taskName may be null
                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "TaskLoadFailure", taskName, _loadedType.Assembly.AssemblyLocation, e.Message);
            }
            catch (Exception e) // Catching Exception, but rethrowing unless it's a well-known exception.
            {
                if (ExceptionHandling.NotExpectedReflectionException(e))
                {
                    throw;
                }

                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "TaskLoadFailure", taskName, _loadedType.Assembly.AssemblyLocation, e.Message);
            }

            return false;
        }

        #endregion

        #region Private members

        /// <summary>
        /// Validates the given set of parameters, logging the appropriate errors as necessary. 
        /// </summary>
        private static void VerifyThrowIdentityParametersValid(IDictionary<string, string> identityParameters, IElementLocation errorLocation, string taskName, string runtimeName, string architectureName)
        {
            // validate the task factory parameters
            if (identityParameters != null && identityParameters.Count > 0)
            {
                string runtime = null;
                if (identityParameters.TryGetValue(XMakeAttributes.runtime, out runtime))
                {
                    if (!XMakeAttributes.IsValidMSBuildRuntimeValue(runtime))
                    {
                        ProjectErrorUtilities.ThrowInvalidProject
                                (
                                    errorLocation,
                                    "TaskLoadFailureInvalidTaskHostFactoryParameter",
                                    taskName,
                                    runtime,
                                    runtimeName,
                                    XMakeAttributes.MSBuildRuntimeValues.clr2,
                                    XMakeAttributes.MSBuildRuntimeValues.clr4,
                                    XMakeAttributes.MSBuildRuntimeValues.currentRuntime,
                                    XMakeAttributes.MSBuildRuntimeValues.any
                                );
                    }
                }

                string architecture = null;
                if (identityParameters.TryGetValue(XMakeAttributes.architecture, out architecture))
                {
                    if (!XMakeAttributes.IsValidMSBuildArchitectureValue(architecture))
                    {
                        ProjectErrorUtilities.ThrowInvalidProject
                                (
                                    errorLocation,
                                    "TaskLoadFailureInvalidTaskHostFactoryParameter",
                                    taskName,
                                    architecture,
                                    architectureName,
                                    XMakeAttributes.MSBuildArchitectureValues.x86,
                                    XMakeAttributes.MSBuildArchitectureValues.x64,
                                    XMakeAttributes.MSBuildArchitectureValues.currentArchitecture,
                                    XMakeAttributes.MSBuildArchitectureValues.any
                                );
                    }
                }
            }
        }

        /// <summary>
        /// Given the set of parameters that are set to the factory, and the set of parameters coming from the task invocation that we're searching for 
        /// a matching record to, determine whether the parameters match this record.  
        /// </summary>
        private static bool TaskIdentityParametersMatchFactory(IDictionary<string, string> factoryIdentityParameters, IDictionary<string, string> taskIdentityParameters)
        {
            if (taskIdentityParameters == null || taskIdentityParameters.Count == 0 || factoryIdentityParameters == null || factoryIdentityParameters.Count == 0)
            {
                // either the task or the using task doesn't care about anything, in which case we match by default.  
                return true;
            }

            string taskRuntime = null;
            string taskArchitecture = null;
            string usingTaskRuntime = null;
            string usingTaskArchitecture = null;

            taskIdentityParameters.TryGetValue(XMakeAttributes.runtime, out taskRuntime);
            factoryIdentityParameters.TryGetValue(XMakeAttributes.runtime, out usingTaskRuntime);

            if (XMakeAttributes.RuntimeValuesMatch(taskRuntime, usingTaskRuntime))
            {
                taskIdentityParameters.TryGetValue(XMakeAttributes.architecture, out taskArchitecture);
                factoryIdentityParameters.TryGetValue(XMakeAttributes.architecture, out usingTaskArchitecture);

                if (XMakeAttributes.ArchitectureValuesMatch(taskArchitecture, usingTaskArchitecture))
                {
                    // both match
                    return true;
                }
            }

            // one or more does not match, so we don't match.  
            return false;
        }

        /// <summary>
        /// Given a set of task parameters from the UsingTask and from the task invocation, generate a dictionary that combines the two, or throws if the merge
        /// is impossible (we shouldn't ever get to this point if it is ...)
        /// </summary>
        private static IDictionary<string, string> MergeTaskFactoryParameterSets(IDictionary<string, string> factoryIdentityParameters, IDictionary<string, string> taskIdentityParameters)
        {
            IDictionary<string, string> mergedParameters = null;
            string mergedRuntime = null;
            string mergedArchitecture = null;

            if (factoryIdentityParameters == null || factoryIdentityParameters.Count == 0)
            {
                mergedParameters = new Dictionary<string, string>(taskIdentityParameters, StringComparer.OrdinalIgnoreCase);
            }
            else if (taskIdentityParameters == null || taskIdentityParameters.Count == 0)
            {
                mergedParameters = new Dictionary<string, string>(factoryIdentityParameters, StringComparer.OrdinalIgnoreCase);
            }

            if (mergedParameters != null)
            {
                mergedParameters.TryGetValue(XMakeAttributes.runtime, out mergedRuntime);
                mergedParameters.TryGetValue(XMakeAttributes.architecture, out mergedArchitecture);

                mergedParameters[XMakeAttributes.runtime] = XMakeAttributes.GetExplicitMSBuildRuntime(mergedRuntime);
                mergedParameters[XMakeAttributes.architecture] = XMakeAttributes.GetExplicitMSBuildArchitecture(mergedArchitecture);
            }
            else
            {
                string taskRuntime = null;
                string taskArchitecture = null;
                string usingTaskRuntime = null;
                string usingTaskArchitecture = null;

                mergedParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                taskIdentityParameters.TryGetValue(XMakeAttributes.runtime, out taskRuntime);
                factoryIdentityParameters.TryGetValue(XMakeAttributes.runtime, out usingTaskRuntime);

                if (!XMakeAttributes.TryMergeRuntimeValues(taskRuntime, usingTaskRuntime, out mergedRuntime))
                {
                    ErrorUtilities.ThrowInternalError("How did we get two runtime values that were unmergeable?");
                }
                else
                {
                    mergedParameters.Add(XMakeAttributes.runtime, mergedRuntime);
                }

                taskIdentityParameters.TryGetValue(XMakeAttributes.architecture, out taskArchitecture);
                factoryIdentityParameters.TryGetValue(XMakeAttributes.architecture, out usingTaskArchitecture);

                if (!XMakeAttributes.TryMergeArchitectureValues(taskArchitecture, usingTaskArchitecture, out mergedArchitecture))
                {
                    ErrorUtilities.ThrowInternalError("How did we get two runtime values that were unmergeable?");
                }
                else
                {
                    mergedParameters.Add(XMakeAttributes.architecture, mergedArchitecture);
                }
            }

            return mergedParameters;
        }

        /// <summary>
        /// Returns true if the provided set of task host parameters matches the current process, 
        /// and false otherwise. 
        /// </summary>
        private static bool TaskHostParametersMatchCurrentProcess(IDictionary<string, string> mergedParameters)
        {
            if (mergedParameters == null || mergedParameters.Count == 0)
            {
                // We don't care, so they match by default. 
                return true;
            }

            string runtime;
            if (mergedParameters.TryGetValue(XMakeAttributes.runtime, out runtime))
            {
                string currentRuntime = XMakeAttributes.GetExplicitMSBuildRuntime(XMakeAttributes.MSBuildRuntimeValues.currentRuntime);

                if (!currentRuntime.Equals(XMakeAttributes.GetExplicitMSBuildRuntime(runtime), StringComparison.OrdinalIgnoreCase))
                {
                    // runtime doesn't match
                    return false;
                }
            }

            string architecture;
            if (mergedParameters.TryGetValue(XMakeAttributes.architecture, out architecture))
            {
                string currentArchitecture = XMakeAttributes.GetCurrentMSBuildArchitecture();

                if (!currentArchitecture.Equals(XMakeAttributes.GetExplicitMSBuildArchitecture(architecture), StringComparison.OrdinalIgnoreCase))
                {
                    // architecture doesn't match
                    return false;
                }
            }

            // if it doesn't not match, then it matches
            return true;
        }

        /// <summary>
        /// Log errors from TaskLoader. 
        /// </summary>
        private void ErrorLoggingDelegate(string taskLocation, int taskLine, int taskColumn, string message, params object[] messageArgs)
        {
            _taskLoggingContext.LogError(new BuildEventFileInfo(taskLocation, taskLine, taskColumn), message, messageArgs);
        }

        #endregion
    }
}
