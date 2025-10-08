// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
#if FEATURE_APPDOMAIN
using System.Threading.Tasks;
#endif

using Microsoft.Build.BackEnd.Components.RequestBuilder;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
#if NETFRAMEWORK
using Microsoft.IO;
#else
using System.IO;
#endif

using ElementLocation = Microsoft.Build.Construction.ElementLocation;
using TargetLoggingContext = Microsoft.Build.BackEnd.Logging.TargetLoggingContext;
using TaskLoggingContext = Microsoft.Build.BackEnd.Logging.TaskLoggingContext;
using Microsoft.Build.Execution;
using Microsoft.Build.Internal;

#nullable disable

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
        ///  Parameters owned by this particular task host.
        /// </summary>
        private TaskHostParameters _factoryIdentityParameters;

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
        [Obsolete]
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
            return _loadedType.Properties;
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
        [Obsolete("Use CreateTask with TaskHostParameters instead.")]
        public ITask CreateTask(IBuildEngine taskFactoryLoggingHost, IDictionary<string, string> taskIdentityParameters)
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
        public ITask CreateTask(IBuildEngine taskFactoryLoggingHost, TaskHostParameters taskIdentityParameters)
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
            ErrorUtilities.VerifyThrowArgumentNull(task);
#if FEATURE_APPDOMAIN
            AppDomain appDomain;
            if (_tasksAndAppDomains.TryGetValue(task, out appDomain))
            {
                _tasksAndAppDomains.Remove(task);

                if (appDomain != null)
                {
                    AssemblyLoadsTracker.StopTracking(appDomain);
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
        /// Initialize the factory from the task registry.
        /// </summary>
        internal LoadedType InitializeFactory(
            AssemblyLoadInfo loadInfo,
            string taskName,
            IDictionary<string, TaskPropertyInfo> taskParameters,
            string taskElementContents,
            TaskHostParameters taskFactoryIdentityParameters,
            bool taskHostExplicitlyRequested,
            TargetLoggingContext targetLoggingContext,
            ElementLocation elementLocation,
            string taskProjectFile)
        {
            ErrorUtilities.VerifyThrowArgumentNull(loadInfo);
            VerifyThrowIdentityParametersValid(taskFactoryIdentityParameters, elementLocation, taskName, "Runtime", "Architecture");

            if (!taskFactoryIdentityParameters.IsEmpty)
            {
                _factoryIdentityParameters = taskFactoryIdentityParameters;
            }

            _taskHostFactoryExplicitlyRequested = taskHostExplicitlyRequested;

            try
            {
                ErrorUtilities.VerifyThrowArgumentLength(taskName);
                _taskName = taskName;

                string assemblyName = loadInfo.AssemblyName ?? Path.GetFileName(loadInfo.AssemblyFile);
                using var assemblyLoadsTracker = AssemblyLoadsTracker.StartTracking(targetLoggingContext, AssemblyLoadingContext.TaskRun, assemblyName);

                _loadedType = _typeLoader.Load(taskName, loadInfo, _taskHostFactoryExplicitlyRequested);
                ProjectErrorUtilities.VerifyThrowInvalidProject(_loadedType != null, elementLocation, "TaskLoadFailure", taskName, loadInfo.AssemblyLocation, String.Empty);
            }
            catch (TargetInvocationException e)
            {
                // Exception thrown by the called code itself
                // Log the stack, so the task vendor can fix their code
                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "TaskLoadFailure", taskName, loadInfo.AssemblyLocation, Environment.NewLine + e.InnerException.ToString());
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

                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "TaskLoadFailure", taskName, loadInfo.AssemblyLocation, e.Message);
            }
            catch (ArgumentNullException e)
            {
                // taskName may be null
                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "TaskLoadFailure", taskName, loadInfo.AssemblyLocation, e.Message);
            }
            catch (Exception e) when (!ExceptionHandling.NotExpectedReflectionException(e))
            {
                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "TaskLoadFailure", taskName, loadInfo.AssemblyLocation, e.Message);
            }

            return _loadedType;
        }

        /// <summary>
        /// Create an instance of the wrapped ITask for a batch run of the task.
        /// </summary>
        internal ITask CreateTaskInstance(
            ElementLocation taskLocation,
            TaskLoggingContext taskLoggingContext,
            IBuildComponentHost buildComponentHost,
            TaskHostParameters taskIdentityParameters,
#if FEATURE_APPDOMAIN
            AppDomainSetup appDomainSetup,
#endif
            bool isOutOfProc,
            int scheduledNodeId,
            Func<string, ProjectPropertyInstance> getProperty)
        {
            bool useTaskFactory = false;
            TaskHostParameters mergedParameters = new();
            _taskLoggingContext = taskLoggingContext;

            // Optimization for the common (vanilla AssemblyTaskFactory) case -- only calculate
            // the task factory parameters if we have any to calculate; otherwise even if we
            // still launch the task factory, it will be with parameters corresponding to the
            // current process.
            if (!_factoryIdentityParameters.IsEmpty || !taskIdentityParameters.IsEmpty)
            {
                VerifyThrowIdentityParametersValid(taskIdentityParameters, taskLocation, _taskName, "MSBuildRuntime", "MSBuildArchitecture");

                mergedParameters = MergeTaskFactoryParameterSets(_factoryIdentityParameters, taskIdentityParameters);
                useTaskFactory = _taskHostFactoryExplicitlyRequested || !TaskHostParametersMatchCurrentProcess(mergedParameters);
            }
            else
            {
                // if we don't have any task host parameters specified on either the using task or the
                // task invocation, then we will run in-proc UNLESS "TaskHostFactory" is explicitly specified
                // as the task factory.
                useTaskFactory = _taskHostFactoryExplicitlyRequested;
            }

            _taskLoggingContext?.TargetLoggingContext?.ProjectLoggingContext?.ProjectTelemetry?.AddTaskExecution(GetType().FullName, isTaskHost: useTaskFactory);

            if (useTaskFactory)
            {
                ErrorUtilities.VerifyThrowInternalNull(buildComponentHost);

                string runtime = mergedParameters.Runtime ?? XMakeAttributes.GetCurrentMSBuildRuntime();
                string architecture = mergedParameters.Architecture ?? XMakeAttributes.GetCurrentMSBuildArchitecture();

                mergedParameters = AddNetHostParamsIfNeeded(runtime, architecture, getProperty);

#pragma warning disable SA1111, SA1009 // Closing parenthesis should be on line of last parameter
                TaskHostTask task = new TaskHostTask(
                    taskLocation,
                    taskLoggingContext,
                    buildComponentHost,
                    mergedParameters,
                    _loadedType,
                    taskHostFactoryExplicitlyRequested: taskIdentityParameters.IsTaskHostFactory ?? false,
#if FEATURE_APPDOMAIN
                    appDomainSetup,
#endif
                    scheduledNodeId);
                return task;
            }
            else
            {
#if FEATURE_APPDOMAIN
                AppDomain taskAppDomain = null;
#endif

#pragma warning disable SA1111, SA1009 // Closing parenthesis should be on line of last parameter
                ITask taskInstance = TaskLoader.CreateTask(
                    _loadedType,
                    _taskName,
                    taskLocation.File,
                    taskLocation.Line,
                    taskLocation.Column,
                    new TaskLoader.LogError(ErrorLoggingDelegate),
#if FEATURE_APPDOMAIN
                    appDomainSetup,
                    appDomain => AssemblyLoadsTracker.StartTracking(taskLoggingContext, AssemblyLoadingContext.TaskRun, _loadedType.Type, appDomain),
#endif
                    isOutOfProc
#if FEATURE_APPDOMAIN
                    , out taskAppDomain
#endif
                    );
#pragma warning restore SA1111, SA1009 // Closing parenthesis should be on line of last parameter

#if FEATURE_APPDOMAIN
                if (taskAppDomain != null && taskInstance != null)
                {
                    _tasksAndAppDomains[taskInstance] = taskAppDomain;
                }
                else if (taskAppDomain != null)
                {
                    AssemblyLoadsTracker.StopTracking(taskAppDomain);
                }
#endif

                return taskInstance;
            }
        }

        /// <summary>
        /// Is the given task name able to be created by the task factory. In the case of an assembly task factory
        /// this question is answered by checking the assembly wrapped by the task factory to see if it exists.
        /// </summary>
        internal bool TaskNameCreatableByFactory(string taskName, TaskHostParameters taskIdentityParameters, string taskProjectFile, TargetLoggingContext targetLoggingContext, ElementLocation elementLocation)
        {
            if (!TaskIdentityParametersMatchFactory(_factoryIdentityParameters, taskIdentityParameters))
            {
                return false;
            }

            try
            {
                ErrorUtilities.VerifyThrowArgumentLength(taskName, "TaskName");
                // Parameters match, so now we check to see if the task exists.
                return _typeLoader.ReflectionOnlyLoad(taskName, _loadedType.Assembly) != null;
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
            catch (Exception e) when (!ExceptionHandling.NotExpectedReflectionException(e))
            {
                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "TaskLoadFailure", taskName, _loadedType.Assembly.AssemblyLocation, e.Message);
            }

            return false;
        }

        #endregion

        #region Private members

        /// <summary>
        /// Validates the given set of parameters, logging the appropriate errors as necessary.
        /// </summary>
        private static void VerifyThrowIdentityParametersValid(TaskHostParameters identityParameters, IElementLocation errorLocation, string taskName, string runtimeName, string architectureName)
        {
            // validate the task factory parameters
            if (identityParameters.Runtime != null)
            {
                if (!XMakeAttributes.IsValidMSBuildRuntimeValue(identityParameters.Runtime))
                {
                    ProjectErrorUtilities.ThrowInvalidProject(
                        errorLocation,
                        "TaskLoadFailureInvalidTaskHostFactoryParameter",
                        taskName,
                        identityParameters.Runtime,
                        runtimeName,
                        XMakeAttributes.MSBuildRuntimeValues.clr2,
                        XMakeAttributes.MSBuildRuntimeValues.clr4,
                        XMakeAttributes.MSBuildRuntimeValues.currentRuntime,
                        XMakeAttributes.MSBuildRuntimeValues.any);
                }
            }

            if (identityParameters.Architecture != null)
            {
                if (!XMakeAttributes.IsValidMSBuildArchitectureValue(identityParameters.Architecture))
                {
                    ProjectErrorUtilities.ThrowInvalidProject(
                        errorLocation,
                        "TaskLoadFailureInvalidTaskHostFactoryParameter",
                        taskName,
                        identityParameters.Architecture,
                        architectureName,
                        XMakeAttributes.MSBuildArchitectureValues.x86,
                        XMakeAttributes.MSBuildArchitectureValues.x64,
                        XMakeAttributes.MSBuildArchitectureValues.currentArchitecture,
                        XMakeAttributes.MSBuildArchitectureValues.any);
                }
            }
        }

        /// <summary>
        /// Given the set of parameters that are set to the factory, and the set of parameters coming from the task invocation that we're searching for
        /// a matching record to, determine whether the parameters match this record.
        /// </summary>
        private static bool TaskIdentityParametersMatchFactory(TaskHostParameters factoryIdentityParameters, TaskHostParameters taskIdentityParameters)
        {
            if (taskIdentityParameters.IsEmpty || factoryIdentityParameters.IsEmpty)
            {
                // either the task or the using task doesn't care about anything, in which case we match by default.
                return true;
            }

            if (XMakeAttributes.RuntimeValuesMatch(taskIdentityParameters.Runtime, factoryIdentityParameters.Runtime))
            {
                if (XMakeAttributes.ArchitectureValuesMatch(taskIdentityParameters.Architecture, factoryIdentityParameters.Architecture))
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
        private static TaskHostParameters MergeTaskFactoryParameterSets(
            TaskHostParameters factoryIdentityParameters,
            TaskHostParameters taskIdentityParameters)
        {
            // If one is empty, just use the other
            if (factoryIdentityParameters.IsEmpty)
            {
                return taskIdentityParameters.IsEmpty
                    ? TaskHostParameters.Empty
                    : new TaskHostParameters(
                        runtime: XMakeAttributes.GetExplicitMSBuildRuntime(taskIdentityParameters.Runtime),
                        architecture: XMakeAttributes.GetExplicitMSBuildArchitecture(taskIdentityParameters.Architecture));
            }

            if (taskIdentityParameters.IsEmpty)
            {
                return new TaskHostParameters(
                    runtime: XMakeAttributes.GetExplicitMSBuildRuntime(factoryIdentityParameters.Runtime),
                    architecture: XMakeAttributes.GetExplicitMSBuildArchitecture(factoryIdentityParameters.Architecture));
            }

            // Both have values - need to merge them
            if (!XMakeAttributes.TryMergeRuntimeValues(taskIdentityParameters.Runtime, factoryIdentityParameters.Runtime, out var mergedRuntime))
            {
                ErrorUtilities.ThrowInternalError("How did we get two runtime values that were unmergeable?");
            }

            if (!XMakeAttributes.TryMergeArchitectureValues(taskIdentityParameters.Architecture, factoryIdentityParameters.Architecture, out var mergedArchitecture))
            {
                ErrorUtilities.ThrowInternalError("How did we get two architecture values that were unmergeable?");
            }

            return new TaskHostParameters(
                runtime: mergedRuntime,
                architecture: mergedArchitecture);
        }

        /// <summary>
        /// Adds the properties necessary for NET task host instantiation.
        /// </summary>
        /// <summary>
        /// Adds the properties necessary for .NET task host instantiation if the runtime is .NET.
        /// Returns a new TaskHostParameters with .NET host parameters added, or the original if not needed.
        /// </summary>
        private static TaskHostParameters AddNetHostParamsIfNeeded(
            string runtime,
            string architecture,
            Func<string, ProjectPropertyInstance> getProperty)
        {
            // Only add .NET host parameters if runtime is .NET
            if (!runtime.Equals(XMakeAttributes.MSBuildRuntimeValues.net, StringComparison.OrdinalIgnoreCase))
            {
                return new TaskHostParameters(runtime, architecture);
            }

            string dotnetHostPath = getProperty(Constants.DotnetHostPathEnvVarName)?.EvaluatedValue;
            string ridGraphPath = getProperty(Constants.RuntimeIdentifierGraphPath)?.EvaluatedValue;
            string msBuildAssemblyPath = !string.IsNullOrEmpty(ridGraphPath)
                ? Path.GetDirectoryName(ridGraphPath) ?? string.Empty
                : string.Empty;

            // Only create new parameters if we have valid .NET host paths
            return string.IsNullOrEmpty(dotnetHostPath) || string.IsNullOrEmpty(msBuildAssemblyPath)
                ? new TaskHostParameters(runtime, architecture)
                : new TaskHostParameters(
                    runtime: runtime,
                    architecture: architecture,
                    dotnetHostPath: dotnetHostPath,
                    msBuildAssemblyPath: msBuildAssemblyPath);
        }

        /// <summary>
        /// Returns true if the provided set of task host parameters matches the current process,
        /// and false otherwise.
        /// </summary>
        private static bool TaskHostParametersMatchCurrentProcess(TaskHostParameters mergedParameters)
        {
            if (mergedParameters.IsEmpty)
            {
                // We don't care, so they match by default.
                return true;
            }

            if (mergedParameters.Runtime != null)
            {
                string currentRuntime = XMakeAttributes.GetExplicitMSBuildRuntime(XMakeAttributes.MSBuildRuntimeValues.currentRuntime);

                if (!currentRuntime.Equals(XMakeAttributes.GetExplicitMSBuildRuntime(mergedParameters.Runtime), StringComparison.OrdinalIgnoreCase))
                {
                    // runtime doesn't match
                    return false;
                }
            }

            if (mergedParameters.Architecture != null)
            {
                string currentArchitecture = XMakeAttributes.GetCurrentMSBuildArchitecture();

                if (!currentArchitecture.Equals(XMakeAttributes.GetExplicitMSBuildArchitecture(mergedParameters.Architecture), StringComparison.OrdinalIgnoreCase))
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

        public bool Initialize(string taskName, TaskHostParameters factoryIdentityParameters, IDictionary<string, TaskPropertyInfo> parameterGroup, string taskBody, IBuildEngine taskFactoryLoggingHost)
        {
            ErrorUtilities.ThrowInternalError("Use internal call to properly initialize the assembly task factory");
            return false;
        }

        #endregion
    }
}
