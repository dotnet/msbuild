// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
#if FEATURE_APPDOMAIN
using System.Runtime.Remoting;
#endif
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The TaskExecutionHost is responsible for instantiating tasks, setting their parameters and gathering outputs using
    /// reflection, and executing the task in the appropriate context.The TaskExecutionHost does not deal with any part of the task declaration or
    /// XML.
    /// </summary>
    internal class TaskExecutionHost : ITaskExecutionHost, IDisposable
    {
        /// <summary>
        /// Time interval in miliseconds to wait between receiving a cancelation signal and emitting the first warning that a non-cancelable task has not finished
        /// </summary>
        private const int CancelFirstWarningWaitInterval = 5000;

        /// <summary>
        /// Time interval in miliseconds between subsequent warnings that a non-cancelable task has not finished
        /// </summary>
        private const int CancelWarningWaitInterval = 15000;

#if FEATURE_APPDOMAIN
        /// <summary>
        /// Resolver to assist in resolving types when a new appdomain is created
        /// </summary>
        private TaskEngineAssemblyResolver _resolver;
#endif

        /// <summary>
        /// The interface used to call back into the build engine.
        /// </summary>
        private IBuildEngine2 _buildEngine;

        /// <summary>
        /// The project instance in whose context we are executing
        /// </summary>
        private ProjectInstance _projectInstance;

        // Items required for all batches of a task

        /// <summary>
        /// The logging context for the target.
        /// </summary>
        private TargetLoggingContext _targetLoggingContext;

        /// <summary>
        /// The logging context for the task.
        /// </summary>
        private TaskLoggingContext _taskLoggingContext;

        /// <summary>
        /// The registration which handles the callback when task cancellation is invoked.
        /// </summary>
        private CancellationTokenRegistration _cancellationTokenRegistration;

        /// <summary>
        /// The name of the task to execute.
        /// </summary>
        private string _taskName;

        /// <summary>
        /// The XML location of the task element.
        /// </summary>
        private ElementLocation _taskLocation;

        /// <summary>
        /// The arbitrary task host object.
        /// </summary>
        private ITaskHost _taskHost;

        // Items required for a particular batch of a task

        /// <summary>
        /// The bucket used to evaluate items and properties.
        /// </summary>
        private ItemBucket _batchBucket;

        /// <summary>
        /// The task type retrieved from the assembly.
        /// </summary>
        private TaskFactoryWrapper _taskFactoryWrapper;

        /// <summary>
        /// Set to true if the execution has been cancelled.
        /// </summary>
        private bool _cancelled;

        /// <summary>
        /// Event which is signalled when a task is not executing.  Used for cancellation.
        /// </summary>
        private readonly ManualResetEvent _taskExecutionIdle = new ManualResetEvent(true);

        /// <summary>
        /// The task items that we remoted across the appdomain boundary
        /// we use this list to disconnect the task items once we're done.
        /// </summary>
        private List<TaskItem> _remotedTaskItems;

        /// <summary>
        /// We need access to the build component host so that we can get at the 
        /// task host node provider when running a task wrapped by TaskHostTask
        /// </summary>
        private readonly IBuildComponentHost _buildComponentHost;

        /// <summary>
        /// The set of intrinsic tasks mapped for this process.
        /// </summary>
        private readonly Dictionary<string, TaskFactoryWrapper> _intrinsicTasks = new Dictionary<string, TaskFactoryWrapper>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Constructor
        /// </summary>
        internal TaskExecutionHost(IBuildComponentHost host)
        {
            _buildComponentHost = host;
            if (host?.BuildParameters != null)
            {
                LogTaskInputs = host.BuildParameters.LogTaskInputs;
            }

            // If this is false, check the environment variable to see if it's there:
            if (!LogTaskInputs)
            {
                LogTaskInputs = Traits.Instance.EscapeHatches.LogTaskInputs;
            }
        }

        /// <summary>
        /// Constructor, for unit testing only.  
        /// </summary>
        internal TaskExecutionHost()
        {
            // do nothing
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~TaskExecutionHost()
        {
            Debug.Fail("Unexpected finalization.  Dispose should already have been called.");
            Dispose(false);
        }

        /// <summary>
        /// Flag to determine whether or not to log task inputs.
        /// </summary>
        public bool LogTaskInputs { get; }

        /// <summary>
        /// The associated project.
        /// </summary>
        ProjectInstance ITaskExecutionHost.ProjectInstance => _projectInstance;

        /// <summary>
        /// Gets the task instance
        /// </summary>
        internal ITask TaskInstance { get; private set; }

        /// <summary>
        /// FOR UNIT TESTING ONLY
        /// </summary>
        internal TaskFactoryWrapper _UNITTESTONLY_TaskFactoryWrapper
        {
            get => _taskFactoryWrapper;
            set => _taskFactoryWrapper = value;
        }

#if FEATURE_APPDOMAIN
        /// <summary>
        /// App domain configuration.
        /// </summary>
        internal AppDomainSetup AppDomainSetup { get; set; }
#endif

        /// <summary>
        /// Whether or not this is out-of-proc.
        /// </summary>
        internal bool IsOutOfProc { get; set; }

        /// <summary>
        /// Implementation of IDisposable
        /// </summary>
        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #region ITaskExecutionHost Members

        /// <summary>
        /// Initialize to run a specific task.
        /// </summary>
        void ITaskExecutionHost.InitializeForTask(IBuildEngine2 buildEngine, TargetLoggingContext loggingContext, ProjectInstance projectInstance, string taskName, ElementLocation taskLocation, ITaskHost taskHost, bool continueOnError,
#if FEATURE_APPDOMAIN
            AppDomainSetup appDomainSetup,
#endif
            bool isOutOfProc, CancellationToken cancellationToken)
        {
            _buildEngine = buildEngine;
            _projectInstance = projectInstance;
            _targetLoggingContext = loggingContext;
            _taskName = taskName;
            _taskLocation = taskLocation;
            _cancellationTokenRegistration = cancellationToken.Register(Cancel);
            _taskHost = taskHost;
            _taskExecutionIdle.Set();
#if FEATURE_APPDOMAIN
            AppDomainSetup = appDomainSetup;
#endif
            IsOutOfProc = isOutOfProc;
        }

        /// <summary>
        /// Ask the task host to find its task in the registry and get it ready for initializing the batch
        /// </summary>
        /// <returns>True if the task is found in the task registry false if otherwise.</returns>
        TaskRequirements? ITaskExecutionHost.FindTask(IDictionary<string, string> taskIdentityParameters)
        {
            if (_taskFactoryWrapper == null)
            {
                _taskFactoryWrapper = FindTaskInRegistry(taskIdentityParameters);
            }

            if (_taskFactoryWrapper == null)
            {
                return null;
            }

            TaskRequirements requirements = TaskRequirements.None;

            if (_taskFactoryWrapper.TaskFactoryLoadedType.HasSTAThreadAttribute())
            {
                requirements |= TaskRequirements.RequireSTAThread;
            }

            if (_taskFactoryWrapper.TaskFactoryLoadedType.HasLoadInSeparateAppDomainAttribute())
            {
                requirements |= TaskRequirements.RequireSeparateAppDomain;

                // we're going to be remoting across the appdomain boundary, so
                // create the list that we'll use to disconnect the taskitems once we're done
                _remotedTaskItems = new List<TaskItem>();
            }

            return requirements;
        }

        /// <summary>
        /// Initialize to run a specific batch of the current task.
        /// </summary>
        bool ITaskExecutionHost.InitializeForBatch(TaskLoggingContext loggingContext, ItemBucket batchBucket, IDictionary<string, string> taskIdentityParameters)
        {
            ErrorUtilities.VerifyThrowArgumentNull(loggingContext, nameof(loggingContext));
            ErrorUtilities.VerifyThrowArgumentNull(batchBucket, nameof(batchBucket));

            _taskLoggingContext = loggingContext;
            _batchBucket = batchBucket;

            if (_taskFactoryWrapper == null)
            {
                return false;
            }

#if FEATURE_APPDOMAIN
            // If the task assembly is loaded into a separate AppDomain using LoadFrom, then we have a problem
            // to solve - when the task class Type is marshalled back into our AppDomain, it's not just transferred
            // here. Instead, NDP will try to Load (not LoadFrom!) the task assembly into our AppDomain, and since
            // we originally used LoadFrom, it will fail miserably not knowing where to find it.
            // We need to temporarily subscribe to the AppDomain.AssemblyResolve event to fix it.
            if (_resolver == null)
            {
                _resolver = new TaskEngineAssemblyResolver();
                _resolver.Initialize(_taskFactoryWrapper.TaskFactoryLoadedType.Assembly.AssemblyFile);
                _resolver.InstallHandler();
            }
#endif

            // We instantiate a new task object for each batch
            TaskInstance = InstantiateTask(taskIdentityParameters);

            if (TaskInstance == null)
            {
                return false;
            }

            TaskInstance.BuildEngine = _buildEngine;
            TaskInstance.HostObject = _taskHost;

            return true;
        }

        /// <summary>
        /// Sets all of the specified parameters on the task.
        /// </summary>
        /// <param name="parameters">The name/value pairs for the parameters.</param>
        /// <returns>True if the parameters were set correctly, false otherwise.</returns>
        bool ITaskExecutionHost.SetTaskParameters(IDictionary<string, (string, ElementLocation)> parameters)
        {
            ErrorUtilities.VerifyThrowArgumentNull(parameters, nameof(parameters));

            bool taskInitialized = true;

            // Get the properties that exist on this task.  We need to gather all of the ones that are marked
            // "required" so that we can keep track of whether or not they all get set.
            var setParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            IDictionary<string, string> requiredParameters = GetNamesOfPropertiesWithRequiredAttribute();

            // look through all the attributes of the task element
            foreach (KeyValuePair<string, (string, ElementLocation)> parameter in parameters)
            {
                bool taskParameterSet = false;  // Did we actually call the setter on this task parameter?
                bool success;

                try
                {
                    success = SetTaskParameter(parameter.Key, parameter.Value.Item1, parameter.Value.Item2, requiredParameters.ContainsKey(parameter.Key), out taskParameterSet);
                }
                catch (Exception e) // Catching Exception, but rethrowing unless it's a well-known exception.
                {
                    if (ExceptionHandling.NotExpectedReflectionException(e))
                    {
                        throw;
                    }

                    // Reflection related exception
                    _taskLoggingContext.LogError(new BuildEventFileInfo(_taskLocation), "TaskParametersError", _taskName, e.Message);

                    success = false;
                }

                if (!success)
                {
                    // stop processing any more attributes
                    taskInitialized = false;
                    break;
                }
                else if (taskParameterSet)
                {
                    // Keep track that we've set a value for this property.  Note that this will
                    // keep track of non-required properties as well, but that's okay.  We just
                    // to check at the end that there are no values in the requiredParameters
                    // table that aren't also in the setParameters table.
                    setParameters[parameter.Key] = String.Empty;
                }
            }

            if (taskInitialized)
            {
                // See if any required properties were not set
                foreach (KeyValuePair<string, string> requiredParameter in requiredParameters)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject
                    (
                        setParameters.ContainsKey(requiredParameter.Key),
                        _taskLocation,
                        "RequiredPropertyNotSetError",
                        _taskName,
                        requiredParameter.Key
                    );
                }
            }

            return taskInitialized;
        }

        /// <summary>
        /// Retrieve the outputs from the task.
        /// </summary>
        /// <returns>True of the outputs were gathered successfully, false otherwise.</returns>
        bool ITaskExecutionHost.GatherTaskOutputs(string parameterName, ElementLocation parameterLocation, bool outputTargetIsItem, string outputTargetName)
        {
            ErrorUtilities.VerifyThrow(_taskFactoryWrapper != null, "Need a taskFactoryWrapper to retrieve outputs from.");

            bool gatheredGeneratedOutputsSuccessfully = true;

            try
            {
                TaskPropertyInfo parameter = _taskFactoryWrapper.GetProperty(parameterName);

                // flag an error if we find a parameter that has no .NET property equivalent
                ProjectErrorUtilities.VerifyThrowInvalidProject
                (
                    parameter != null,
                    parameterLocation,
                    "UnexpectedTaskOutputAttribute",
                    parameterName,
                    _taskName
                );

                // output parameters must have their corresponding .NET properties marked with the Output attribute
                ProjectErrorUtilities.VerifyThrowInvalidProject
                (
                    _taskFactoryWrapper.GetNamesOfPropertiesWithOutputAttribute.ContainsKey(parameterName),
                    parameterLocation,
                    "UnmarkedOutputTaskParameter",
                    parameter.Name,
                    _taskName
                );

                // grab the outputs from the task's designated output parameter (which is a .NET property)
                Type type = parameter.PropertyType;

                EnsureParameterInitialized(parameter, _batchBucket.Lookup);

                if (TaskParameterTypeVerifier.IsAssignableToITask(type))
                {
                    ITaskItem[] outputs = GetItemOutputs(parameter);
                    GatherTaskItemOutputs(outputTargetIsItem, outputTargetName, outputs, parameterLocation, parameter);
                }
                else if (TaskParameterTypeVerifier.IsValueTypeOutputParameter(type))
                {
                    string[] outputs = GetValueOutputs(parameter);
                    GatherArrayStringAndValueOutputs(outputTargetIsItem, outputTargetName, outputs, parameterLocation, parameter);
                }
                else
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject
                    (
                        false,
                        parameterLocation,
                        "UnsupportedTaskParameterTypeError",
                        parameter.PropertyType.FullName,
                        parameter.Name,
                        _taskName
                    );
                }
            }
            catch (InvalidOperationException e)
            {
                // handle invalid TaskItems in task outputs
                _targetLoggingContext.LogError
                (
                    new BuildEventFileInfo(parameterLocation),
                    "InvalidTaskItemsInTaskOutputs",
                    _taskName,
                    parameterName,
                    e.Message
                );

                gatheredGeneratedOutputsSuccessfully = false;
            }
            catch (TargetInvocationException e)
            {
                // handle any exception thrown by the task's getter
                // Exception thrown by the called code itself
                // Log the stack, so the task vendor can fix their code
                // Log the task line number, whatever the value of ContinueOnError;
                // because this will be a hard error anyway.
                _targetLoggingContext.LogFatalTaskError
                (
                    e.InnerException,
                    new BuildEventFileInfo(parameterLocation),
                    _taskName);

                // We do not recover from a task exception while getting outputs,
                // so do not merely set gatheredGeneratedOutputsSuccessfully = false; here
                ProjectErrorUtilities.VerifyThrowInvalidProject
                (
                    false,
                    parameterLocation,
                    "FailedToRetrieveTaskOutputs",
                    _taskName,
                    parameterName,
                    e.InnerException?.Message
                );
            }
            catch (Exception e)
            {
                // Catching Exception, but rethrowing unless it's a well-known exception.
                if (ExceptionHandling.NotExpectedReflectionException(e))
                {
                    throw;
                }

                ProjectErrorUtilities.VerifyThrowInvalidProject
                (
                    false,
                    parameterLocation,
                    "FailedToRetrieveTaskOutputs",
                    _taskName,
                    parameterName,
                    e.Message
                );
            }

            return gatheredGeneratedOutputsSuccessfully;
        }

        /// <summary>
        /// Cleans up after running a batch.
        /// </summary>
        void ITaskExecutionHost.CleanupForBatch()
        {
            try
            {
                if (_taskFactoryWrapper != null && TaskInstance != null)
                {
                    _taskFactoryWrapper.TaskFactory.CleanupTask(TaskInstance);
                }
            }
            finally
            {
                TaskInstance = null;
            }
        }

        /// <summary>
        /// Cleans up after running the task.
        /// </summary>
        void ITaskExecutionHost.CleanupForTask()
        {
#if FEATURE_APPDOMAIN
            if (_resolver != null)
            {
                _resolver.RemoveHandler();
                _resolver = null;
            }
#endif

            _taskFactoryWrapper = null;

            // We must null this out because it could be a COM object (or any other ref-counted object) which needs to
            // be released.
            _taskHost = null;
            CleanupCancellationToken();

            ErrorUtilities.VerifyThrow(TaskInstance == null, "Task Instance should be null");
        }

        /// <summary>
        /// Executes the task.
        /// </summary>
        bool ITaskExecutionHost.Execute()
        {
            // If cancel is called before we get here, we simply don't execute and return failure.  If cancel is called after this check
            // the task needs to be able to handle the possibility that Cancel has been called before the task has done anything meaningful,
            // and Execute may not even have been called yet.
            _taskExecutionIdle.Reset();

            if (_cancelled)
            {
                _taskExecutionIdle.Set();
                return false;
            }

            bool taskReturnValue;

            try
            {
                taskReturnValue = TaskInstance.Execute();
            }
            finally
            {
                _taskExecutionIdle.Set();
            }

            return taskReturnValue;
        }

        #endregion

        /// <summary>
        /// Implementation of IDisposable
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _taskExecutionIdle.Dispose();
                CleanupCancellationToken();
            }

#if FEATURE_APPDOMAIN
            // if we've been asked to remote these items then
            // we need to disconnect them from .NET Remoting now we're all done with them
            if (_remotedTaskItems != null)
            {
                foreach (TaskItem item in _remotedTaskItems)
                {
                    // Tell remoting to forget connections to the taskitem
                    RemotingServices.Disconnect(item);
                }
            }

            _remotedTaskItems = null;
#endif
        }

        /// <summary>
        /// Disposes of the cancellation token registration.
        /// </summary>
        private void CleanupCancellationToken()
        {
            _cancellationTokenRegistration.Dispose();
        }

        /// <summary>
        /// Cancels the currently-running task.
        /// Kick off a task to wait for the currently-running task and log the wait message.
        /// </summary>
        private void Cancel()
        {
            // This will prevent the current and any future tasks from running on this TaskExecutionHost, because we don't reset the cancelled flag.
            _cancelled = true;

            ITask currentInstance = TaskInstance;
            ICancelableTask cancellableTask = null;
            if (currentInstance != null)
            {
                cancellableTask = currentInstance as ICancelableTask;
            }

            if (cancellableTask != null)
            {
                try
                {
                    cancellableTask.Cancel();
                }
                catch (Exception e)
                {
                    if (ExceptionHandling.IsCriticalException(e))
                    {
                        throw;
                    }

                    try
                    {
                        _taskLoggingContext.LogFatalTaskError(e, new BuildEventFileInfo(_taskLocation), ((ProjectTaskInstance)_taskLoggingContext.Task).Name);
                    }
                    catch (InternalErrorException)
                    {
                        // If this fails it could be due to the task logging context no longer being valid due to a race condition where the task completes while we
                        // are in this method.  In that case we simply ignore the exception and carry on since we can't log anything anyhow.
                        if (_taskLoggingContext.IsValid)
                        {
                            throw;
                        }
                    }
                }
            }

            // Let the task finish now.  If cancellation worked, hopefully it finishes sooner than it would have otherwise.
            // If the task builder crashed, this could have already been disposed
#if FEATURE_HANDLE_SAFEWAITHANDLE
            if (!_taskExecutionIdle.SafeWaitHandle.IsClosed)
#else
            if (!_taskExecutionIdle.GetSafeWaitHandle().IsClosed)
#endif
            {
                // Kick off a task to log the message so that we don't block the calling thread.
                Task.Run(async delegate
                {
                    await _taskExecutionIdle.ToTask(CancelFirstWarningWaitInterval);
                    if (!_taskExecutionIdle.WaitOne(0))
                    {
                        DisplayCancelWaitMessage();
                        await _taskExecutionIdle.ToTask(CancelWarningWaitInterval);
                        while (!_taskExecutionIdle.WaitOne(0))
                        {
                            DisplayCancelWaitMessage();
                            await _taskExecutionIdle.ToTask(CancelWarningWaitInterval);
                        }
                    }
                });
            }
        }

        #region Local Methods
        /// <summary>
        /// Called on the local side.
        /// </summary>
        private bool SetTaskItemParameter(TaskPropertyInfo parameter, ITaskItem item)
        {
            return InternalSetTaskParameter(parameter, item);
        }

        /// <summary>
        /// Called on the local side.
        /// </summary>
        private bool SetValueParameter(TaskPropertyInfo parameter, Type parameterType, string expandedParameterValue)
        {
            if (parameterType == typeof(bool))
            {
                // Convert the string to the appropriate datatype, and set the task's parameter.
                return InternalSetTaskParameter(parameter, ConversionUtilities.ConvertStringToBool(expandedParameterValue));
            }
            else if (parameterType == typeof(string))
            {
                return InternalSetTaskParameter(parameter, expandedParameterValue);
            }
            else
            {
                return InternalSetTaskParameter(parameter, Convert.ChangeType(expandedParameterValue, parameterType, CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// Called on the local side.
        /// </summary>
        private bool SetParameterArray(TaskPropertyInfo parameter, Type parameterType, IList<TaskItem> taskItems, ElementLocation parameterLocation)
        {
            TaskItem currentItem = null;

            try
            {
                // Loop through all the TaskItems in our arraylist, and convert them.
                ArrayList finalTaskInputs = new ArrayList(taskItems.Count);

                if (parameterType != typeof(ITaskItem[]))
                {
                    foreach (TaskItem item in taskItems)
                    {
                        currentItem = item;
                        if (parameterType == typeof(string[]))
                        {
                            finalTaskInputs.Add(item.ItemSpec);
                        }
                        else if (parameterType == typeof(bool[]))
                        {
                            finalTaskInputs.Add(ConversionUtilities.ConvertStringToBool(item.ItemSpec));
                        }
                        else
                        {
                            finalTaskInputs.Add(Convert.ChangeType(item.ItemSpec, parameterType.GetElementType(), CultureInfo.InvariantCulture));
                        }
                    }
                }
                else
                {
                    foreach (TaskItem item in taskItems)
                    {
                        // if we've been asked to remote these items then
                        // remember them so we can disconnect them from remoting later
                        RecordItemForDisconnectIfNecessary(item);

                        finalTaskInputs.Add(item);
                    }
                }

                return InternalSetTaskParameter(parameter, finalTaskInputs.ToArray(parameterType.GetElementType()));
            }
            catch (Exception ex)
            {
                if (ex is InvalidCastException || // invalid type
                    ex is ArgumentException || // can't convert to bool
                    ex is FormatException || // bad string representation of a type
                    ex is OverflowException) // overflow when converting string representation of a numerical type
                {
                    ProjectErrorUtilities.ThrowInvalidProject
                    (
                        parameterLocation,
                        "InvalidTaskParameterValueError",
                        currentItem.ItemSpec,
                        parameter.Name,
                        parameterType.FullName,
                        _taskName
                    );
                }

                throw;
            }
        }

        /// <summary>
        /// Remember this TaskItem so that we can disconnect it when this Task has finished executing
        /// Only if we're passing TaskItems to another AppDomain is this necessary. This call
        /// Will make that determination for you.
        /// </summary>
        private void RecordItemForDisconnectIfNecessary(TaskItem item)
        {
            // remember that we need to disconnect this item
            _remotedTaskItems?.Add(item);
        }

        /// <summary>
        /// Gets the outputs (as an array of ITaskItem) from the specified output parameter.
        /// </summary>
        private ITaskItem[] GetItemOutputs(TaskPropertyInfo parameter)
        {
            object outputs = _taskFactoryWrapper.GetPropertyValue(TaskInstance, parameter);

            if (!(outputs is ITaskItem[] taskItemOutputs))
            {
                taskItemOutputs = new[] { (ITaskItem)outputs };
            }

            return taskItemOutputs;
        }

        /// <summary>
        /// Gets the outputs (as an array of string values) from the specified output parameter.
        /// </summary>
        private string[] GetValueOutputs(TaskPropertyInfo parameter)
        {
            object outputs = _taskFactoryWrapper.GetPropertyValue(TaskInstance, parameter);

            Array convertibleOutputs = parameter.PropertyType.IsArray ? (Array)outputs : new[] { outputs };

            if (convertibleOutputs == null)
            {
                return null;
            }

            var stringOutputs = new string[convertibleOutputs.Length];
            for (int i = 0; i < convertibleOutputs.Length; i++)
            {
                object output = convertibleOutputs.GetValue(i);
                if (output != null)
                {
                    stringOutputs[i] = (string)Convert.ChangeType(output, typeof(string), CultureInfo.InvariantCulture);
                }
            }

            return stringOutputs;
        }

        #endregion

        /// <summary>
        /// Given the task name, this method tries to find the task. It uses the following search order:
        /// 1) checks the tasks declared by the project, searching by exact name and task identity parameters
        /// 2) checks the global task declarations (in *.TASKS in MSbuild bin dir), searching by exact name and task identity parameters
        /// 3) checks the tasks declared by the project, searching by fuzzy match (missing namespace, etc.) and task identity parameters
        /// 4) checks the global task declarations (in *.TASKS in MSbuild bin dir), searching by fuzzy match (missing namespace, etc.) and task identity parameters
        /// 5) 1-4 again in order without the task identity parameters, to gather additional information for the user (if the task identity 
        ///    parameters don't match, it is an error, but at least we can return them a more useful error in this case than just "could not 
        ///    find task")
        /// 
        /// The search ordering is meant to reduce the number of assemblies we scan, because loading assemblies can be expensive.
        /// The tasks and assemblies declared by the project are scanned first, on the assumption that if the project declared
        /// them, they are likely used.
        /// 
        /// If the set of task identity parameters are defined, only tasks that match that identity are chosen. 
        /// </summary>
        /// <returns>The Type of the task, or null if it was not found.</returns>
        private TaskFactoryWrapper FindTaskInRegistry(IDictionary<string, string> taskIdentityParameters)
        {
            if (!_intrinsicTasks.TryGetValue(_taskName, out TaskFactoryWrapper returnClass))
            {
                returnClass = _projectInstance.TaskRegistry.GetRegisteredTask(_taskName, null, taskIdentityParameters, true /* exact match */, _targetLoggingContext, _taskLocation);
                if (returnClass == null)
                {
                    returnClass = _projectInstance.TaskRegistry.GetRegisteredTask(_taskName, null, taskIdentityParameters, false /* fuzzy match */, _targetLoggingContext, _taskLocation);

                    if (returnClass == null)
                    {
                        returnClass = _projectInstance.TaskRegistry.GetRegisteredTask(_taskName, null, null, true /* exact match */, _targetLoggingContext, _taskLocation);

                        if (returnClass == null)
                        {
                            returnClass = _projectInstance.TaskRegistry.GetRegisteredTask(_taskName, null, null, false /* fuzzy match */, _targetLoggingContext, _taskLocation);

                            if (returnClass == null)
                            {
                                _targetLoggingContext.LogError
                                    (
                                        new BuildEventFileInfo(_taskLocation),
                                        "MissingTaskError",
                                        _taskName,
                                        _projectInstance.TaskRegistry.Toolset.ToolsPath
                                    );

                                return null;
                            }
                        }

                        string usingTaskRuntime = null;
                        string usingTaskArchitecture = null;

                        if (returnClass.FactoryIdentityParameters != null)
                        {
                            returnClass.FactoryIdentityParameters.TryGetValue(XMakeAttributes.runtime, out usingTaskRuntime);
                            returnClass.FactoryIdentityParameters.TryGetValue(XMakeAttributes.architecture, out usingTaskArchitecture);
                        }

                        taskIdentityParameters.TryGetValue(XMakeAttributes.runtime, out string taskRuntime);
                        taskIdentityParameters.TryGetValue(XMakeAttributes.architecture, out string taskArchitecture);

                        _targetLoggingContext.LogError
                            (
                                new BuildEventFileInfo(_taskLocation),
                                "TaskExistsButHasMismatchedIdentityError",
                                _taskName,
                                usingTaskRuntime ?? XMakeAttributes.MSBuildRuntimeValues.any,
                                usingTaskArchitecture ?? XMakeAttributes.MSBuildArchitectureValues.any,
                                taskRuntime ?? XMakeAttributes.MSBuildRuntimeValues.any,
                                taskArchitecture ?? XMakeAttributes.MSBuildArchitectureValues.any
                            );

                        // if we've logged this error, even though we've found something, we want to act like we didn't.  
                        return null;
                    }
                }

                // Map to an intrinsic task, if necessary.
                if (String.Equals(returnClass.TaskFactory.TaskType.FullName, "Microsoft.Build.Tasks.MSBuild", StringComparison.OrdinalIgnoreCase))
                {
                    returnClass = new TaskFactoryWrapper(new IntrinsicTaskFactory(typeof(MSBuild)), new LoadedType(typeof(MSBuild), AssemblyLoadInfo.Create(typeof(TaskExecutionHost).GetTypeInfo().Assembly.FullName, null)), _taskName, null);
                    _intrinsicTasks[_taskName] = returnClass;
                }
                else if (String.Equals(returnClass.TaskFactory.TaskType.FullName, "Microsoft.Build.Tasks.CallTarget", StringComparison.OrdinalIgnoreCase))
                {
                    returnClass = new TaskFactoryWrapper(new IntrinsicTaskFactory(typeof(CallTarget)), new LoadedType(typeof(CallTarget), AssemblyLoadInfo.Create(typeof(TaskExecutionHost).GetTypeInfo().Assembly.FullName, null)), _taskName, null);
                    _intrinsicTasks[_taskName] = returnClass;
                }
            }

            return returnClass;
        }

        /// <summary>
        /// Instantiates the task.
        /// </summary>
        private ITask InstantiateTask(IDictionary<string, string> taskIdentityParameters)
        {
            ITask task = null;

            try
            {
                if (_taskFactoryWrapper.TaskFactory is AssemblyTaskFactory assemblyTaskFactory)
                {
                    task = assemblyTaskFactory.CreateTaskInstance(_taskLocation, _taskLoggingContext, _buildComponentHost, taskIdentityParameters,
#if FEATURE_APPDOMAIN
                        AppDomainSetup,
#endif
                        IsOutOfProc);
                }
                else
                {
                    TaskFactoryLoggingHost loggingHost = new TaskFactoryLoggingHost(_buildEngine.IsRunningMultipleNodes, _taskLocation, _taskLoggingContext);
                    ITaskFactory2 taskFactory2 = _taskFactoryWrapper.TaskFactory as ITaskFactory2;
                    try
                    {
                        if (taskFactory2 == null)
                        {
                            task = _taskFactoryWrapper.TaskFactory.CreateTask(loggingHost);
                        }
                        else
                        {
                            task = taskFactory2.CreateTask(loggingHost, taskIdentityParameters);
                        }
                    }
                    finally
                    {
#if FEATURE_APPDOMAIN
                        loggingHost.MarkAsInactive();
#endif
                    }
                }
            }
            catch (InvalidCastException e)
            {
                _taskLoggingContext.LogError
                (
                    new BuildEventFileInfo(_taskLocation),
                    "TaskInstantiationFailureErrorInvalidCast",
                    _taskName,
                    _taskFactoryWrapper.TaskFactory.FactoryName,
                    e.Message
                );
            }
            catch (TargetInvocationException e)
            {
                // Exception thrown by the called code itself
                // Log the stack, so the task vendor can fix their code
                _taskLoggingContext.LogError
                (
                    new BuildEventFileInfo(_taskLocation),
                    "TaskInstantiationFailureError",
                    _taskName,
                    _taskFactoryWrapper.TaskFactory.FactoryName,
                    Environment.NewLine + e.InnerException
                );
            }
            catch (Exception e) // Catching Exception, but rethrowing unless it's a well-known exception.
            {
                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }

                // Reflection related exception
                _taskLoggingContext.LogError
                (
                    new BuildEventFileInfo(_taskLocation),
                    "TaskInstantiationFailureError",
                    _taskName,
                    _taskFactoryWrapper.TaskFactory.FactoryName,
                    e.Message
                );
            }

            return task;
        }

        /// <summary>
        /// Set the specified parameter based on its type.
        /// </summary>
        private bool SetTaskParameter
        (
            string parameterName,
            string parameterValue,
            ElementLocation parameterLocation,
            bool isRequired,
            out bool parameterSet
        )
        {
            bool success = false;
            parameterSet = false;

            try
            {
                // check if the task has a .NET property corresponding to the parameter
                TaskPropertyInfo parameter = _taskFactoryWrapper.GetProperty(parameterName);

                if (parameter != null)
                {
                    Type parameterType = parameter.PropertyType;

                    EnsureParameterInitialized(parameter, _batchBucket.Lookup);

                    // try to set the parameter
                    if (TaskParameterTypeVerifier.IsValidScalarInputParameter(parameterType))
                    {
                        success = InitializeTaskScalarParameter
                            (
                            parameter,
                            parameterType,
                            parameterValue,
                            parameterLocation,
                            out parameterSet
                            );
                    }
                    else if (TaskParameterTypeVerifier.IsValidVectorInputParameter(parameterType))
                    {
                        success = InitializeTaskVectorParameter
                            (
                            parameter,
                            parameterType,
                            parameterValue,
                            parameterLocation,
                            isRequired,
                            out parameterSet
                            );
                    }
                    else
                    {
                        _taskLoggingContext.LogError
                            (
                            new BuildEventFileInfo(parameterLocation),
                            "UnsupportedTaskParameterTypeError",
                            parameterType.FullName,
                            parameter.Name,
                            _taskName
                            );
                    }

                    if (!success)
                    {
                        // flag an error if the parameter could not be set
                        _taskLoggingContext.LogError
                            (
                            new BuildEventFileInfo(parameterLocation),
                            "InvalidTaskAttributeError",
                            parameterName,
                            parameterValue,
                            _taskName
                            );
                    }
                }
                else
                {
                    // flag an error if we find a parameter that has no .NET property equivalent
                    if (_taskFactoryWrapper.TaskFactoryLoadedType.LoadedAssembly is null)
                    {
                        _taskLoggingContext.LogError
                            (
                            new BuildEventFileInfo( parameterLocation ),
                            "UnexpectedTaskAttribute",
                            parameterName,
                            _taskName,
                            _taskFactoryWrapper.TaskFactoryLoadedType.Type.Assembly.FullName,
                            _taskFactoryWrapper.TaskFactoryLoadedType.Type.Assembly.Location
                            );
                    }
                    else
                    {
                        _taskLoggingContext.LogError
                            (
                            new BuildEventFileInfo( parameterLocation ),
                            "UnexpectedTaskAttribute",
                            parameterName,
                            _taskName,
                            _taskFactoryWrapper.TaskFactoryLoadedType.LoadedAssembly.FullName,
                            _taskFactoryWrapper.TaskFactoryLoadedType.LoadedAssembly.Location
                            );
                    }
                }
            }
            catch (AmbiguousMatchException)
            {
                _taskLoggingContext.LogError
                    (
                    new BuildEventFileInfo(parameterLocation),
                    "AmbiguousTaskParameterError",
                    _taskName,
                    parameterName
                    );
            }
            catch (ArgumentException)
            {
                ProjectErrorUtilities.ThrowInvalidProject
                    (
                    parameterLocation,
                    "SetAccessorNotAvailableOnTaskParameter",
                    parameterName,
                    _taskName
                    );
            }

            return success;
        }

        /// <summary>
        /// Given an instantiated task, this helper method sets the specified scalar parameter based on its type.
        /// </summary>
        private bool InitializeTaskScalarParameter
        (
            TaskPropertyInfo parameter,
            Type parameterType,
            string parameterValue,
            ElementLocation parameterLocation,
            out bool taskParameterSet
        )
        {
            taskParameterSet = false;

            bool success;

            try
            {
                if (parameterType == typeof(ITaskItem))
                {
                    // We don't know how many items we're going to end up with, but we'll
                    // keep adding them to this arraylist as we find them.
                    IList<TaskItem> finalTaskItems = _batchBucket.Expander.ExpandIntoTaskItemsLeaveEscaped(parameterValue, ExpanderOptions.ExpandAll, parameterLocation);

                    if (finalTaskItems.Count == 0)
                    {
                        success = true;
                    }
                    else
                    {
                        if (finalTaskItems.Count != 1)
                        {
                            // We only allow a single item to be passed into a parameter of ITaskItem.

                            // Some of the computation (expansion) here is expensive, so don't make the above
                            // "if" statement directly part of the first param to VerifyThrowInvalidProject.
                            ProjectErrorUtilities.VerifyThrowInvalidProject
                                (
                                false,
                                parameterLocation,
                                "CannotPassMultipleItemsIntoScalarParameter",
                                _batchBucket.Expander.ExpandIntoStringAndUnescape(parameterValue, ExpanderOptions.ExpandAll, parameterLocation),
                                parameter.Name,
                                parameterType.FullName,
                                _taskName
                                );
                        }

                        RecordItemForDisconnectIfNecessary(finalTaskItems[0]);

                        success = SetTaskItemParameter(parameter, finalTaskItems[0]);

                        taskParameterSet = true;
                    }
                }
                else
                {
                    // Expand out all the metadata, properties, and item vectors in the string.
                    string expandedParameterValue = _batchBucket.Expander.ExpandIntoStringAndUnescape(parameterValue, ExpanderOptions.ExpandAll, parameterLocation);

                    if (expandedParameterValue.Length == 0)
                    {
                        success = true;
                    }
                    else
                    {
                        success = SetValueParameter(parameter, parameterType, expandedParameterValue);
                        taskParameterSet = true;
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is InvalidCastException || // invalid type
                    ex is ArgumentException || // can't convert to bool
                    ex is FormatException || // bad string representation of a type
                    ex is OverflowException) // overflow when converting string representation of a numerical type
                {
                    ProjectErrorUtilities.ThrowInvalidProject
                    (
                        parameterLocation,
                        "InvalidTaskParameterValueError",
                        _batchBucket.Expander.ExpandIntoStringAndUnescape(parameterValue, ExpanderOptions.ExpandAll, parameterLocation),
                        parameter.Name,
                        parameterType.FullName,
                        _taskName
                    );
                }

                throw;
            }

            return success;
        }

        private void EnsureParameterInitialized(TaskPropertyInfo parameter, Lookup lookup)
        {
            if (parameter.Initialized)
            {
                return;
            }

            parameter.Initialized = true;

            string taskAndParameterName = _taskName + "_" + parameter.Name;
            string key = "DisableLogTaskParameter_" + taskAndParameterName;
            string metadataKey = "DisableLogTaskParameterItemMetadata_" + taskAndParameterName;

            if (string.Equals(lookup.GetProperty(key)?.EvaluatedValue, "true", StringComparison.OrdinalIgnoreCase))
            {
                parameter.Log = false;
            }
            else if (string.Equals(lookup.GetProperty(metadataKey)?.EvaluatedValue, "true", StringComparison.OrdinalIgnoreCase))
            {
                parameter.LogItemMetadata = false;
            }
        }

        /// <summary>
        /// Given an instantiated task, this helper method sets the specified vector parameter. Vector parameters can be composed
        /// of multiple item vectors. The semicolon is the only separator allowed, and white space around the semicolon is
        /// ignored. Any item separator strings are not allowed, and embedded item vectors are not allowed.
        /// </summary>
        /// <remarks>This method is marked "internal" for unit-testing purposes only -- it should be "private" ideally.</remarks>
        /// <example>
        /// If @(CPPFiles) is a vector for the files a.cpp and b.cpp, and @(IDLFiles) is a vector for the files a.idl and b.idl:
        ///
        ///     "@(CPPFiles)"                               converts to     { a.cpp, b.cpp }
        ///
        ///     "@(CPPFiles); c.cpp; @(IDLFiles); c.idl"    converts to     { a.cpp, b.cpp, c.cpp, a.idl, b.idl, c.idl }
        ///
        ///     "@(CPPFiles,';')"                           converts to     &lt;error&gt;
        ///
        ///     "xxx@(CPPFiles)xxx"                         converts to     &lt;error&gt;
        /// </example>
        private bool InitializeTaskVectorParameter
        (
            TaskPropertyInfo parameter,
            Type parameterType,
            string parameterValue,
            ElementLocation parameterLocation,
            bool isRequired,
            out bool taskParameterSet
        )
        {
            ErrorUtilities.VerifyThrow(parameterValue != null, "Didn't expect null parameterValue in InitializeTaskVectorParameter");

            taskParameterSet = false;
            bool success;
            IList<TaskItem> finalTaskItems = _batchBucket.Expander.ExpandIntoTaskItemsLeaveEscaped(parameterValue, ExpanderOptions.ExpandAll, parameterLocation);

            // If there were no items, don't change the parameter's value.  EXCEPT if it's marked as a required 
            // parameter, in which case we made an explicit decision to pass in an empty array.  This is 
            // to avoid project authors having to add Conditions on all their tasks to avoid calling them
            // when a particular item list is empty.  This way, we just call the task with an empty list,
            // the task will loop over an empty list, and return quickly.
            if ((finalTaskItems.Count > 0) || isRequired)
            {
                // If the task parameter is not a ITaskItem[], then we need to convert
                // all the TaskItem's in our arraylist to the appropriate datatype.
                success = SetParameterArray(parameter, parameterType, finalTaskItems, parameterLocation);
                taskParameterSet = true;
            }
            else
            {
                success = true;
            }

            return success;
        }

        /// <summary>
        /// Variation to handle arrays, to help with logging the parameters.
        /// </summary>
        /// <remarks>
        /// Logging currently enabled only by an env var.
        /// </remarks>
        private bool InternalSetTaskParameter(TaskPropertyInfo parameter, IList parameterValue)
        {
            if (LogTaskInputs &&
                !_taskLoggingContext.LoggingService.OnlyLogCriticalEvents &&
                parameterValue.Count > 0 &&
                parameter.Log)
            {
                string parameterText = ItemGroupLoggingHelper.GetParameterText(
                    ItemGroupLoggingHelper.TaskParameterPrefix,
                    parameter.Name,
                    parameterValue,
                    parameter.LogItemMetadata);
                _taskLoggingContext.LogCommentFromText(MessageImportance.Low, parameterText);
            }

            return InternalSetTaskParameter(parameter, (object)parameterValue);
        }

        /// <summary>
        /// Given an instantiated task, this helper method sets the specified parameter
        /// </summary>
        private bool InternalSetTaskParameter
        (
            TaskPropertyInfo parameter,
            object parameterValue
        )
        {
            bool success = false;

            // Logging currently enabled only by an env var.
            if (LogTaskInputs && !_taskLoggingContext.LoggingService.OnlyLogCriticalEvents)
            {
                // If the type is a list, we already logged the parameters
                if (!(parameterValue is IList))
                {
                    _taskLoggingContext.LogCommentFromText(
                        MessageImportance.Low,
                        ItemGroupLoggingHelper.TaskParameterPrefix + parameter.Name + "=" + ItemGroupLoggingHelper.GetStringFromParameterValue(parameterValue));
                }
            }

            try
            {
                _taskFactoryWrapper.SetPropertyValue(TaskInstance, parameter, parameterValue);
                success = true;
            }
            catch (LoggerException)
            {
                // if a logger has failed, abort immediately
                // Polite logger failure
                throw;
            }
            catch (InternalLoggerException)
            {
                // Logger threw arbitrary exception
                throw;
            }
            catch (TargetInvocationException e)
            {
                // handle any exception thrown by the task's setter itself
                // At this point, the interesting stack is the internal exception.
                // Log the task line number, whatever the value of ContinueOnError;
                // because this will be a hard error anyway.

                // Exception thrown by the called code itself
                // Log the stack, so the task vendor can fix their code
                _taskLoggingContext.LogFatalTaskError
                (
                    e.InnerException,
                    new BuildEventFileInfo(_taskLocation),
                    _taskName);
            }
            catch (Exception e)
            {
                // Catching Exception, but rethrowing unless it's a well-known exception.
                if (ExceptionHandling.NotExpectedReflectionException(e))
                {
                    throw;
                }

                _taskLoggingContext.LogFatalTaskError
                (
                    e,
                    new BuildEventFileInfo(_taskLocation),
                    _taskName);
            }

            return success;
        }

        /// <summary>
        /// Gets task item outputs
        /// </summary>
        private void GatherTaskItemOutputs(bool outputTargetIsItem, string outputTargetName, ITaskItem[] outputs, ElementLocation parameterLocation, TaskPropertyInfo parameter)
        {
            // if the task has generated outputs (if it didn't, don't do anything)
            if (outputs != null)
            {
                if (outputTargetIsItem)
                {
                    foreach (ITaskItem output in outputs)
                    {
                        // if individual items in the array are null, ignore them
                        if (output != null)
                        {
                            ProjectItemInstance newItem;

                            TaskItem outputAsProjectItem = output as TaskItem;
                            string parameterLocationEscaped = EscapingUtilities.EscapeWithCaching(parameterLocation.File);

                            if (outputAsProjectItem != null)
                            {
                                // The common case -- all items involved are Microsoft.Build.Execution.ProjectItemInstance.TaskItems.  
                                // Furthermore, because that is true, we know by definition that they also implement ITaskItem2.
                                newItem = new ProjectItemInstance(_projectInstance, outputTargetName, outputAsProjectItem.IncludeEscaped, parameterLocationEscaped);

                                newItem.SetMetadata(outputAsProjectItem.MetadataCollection); // copy-on-write!
                            }
                            else
                            {
                                if (output is ITaskItem2 outputAsITaskItem2)
                                {
                                    // Probably a Microsoft.Build.Utilities.TaskItem.  Not quite as good, but we can still preserve escaping. 
                                    newItem = new ProjectItemInstance(_projectInstance, outputTargetName, outputAsITaskItem2.EvaluatedIncludeEscaped, parameterLocationEscaped);

                                    // It would be nice to be copy-on-write here, but Utilities.TaskItem doesn't know about CopyOnWritePropertyDictionary. 
                                    foreach (DictionaryEntry entry in outputAsITaskItem2.CloneCustomMetadataEscaped())
                                    {
                                        newItem.SetMetadataOnTaskOutput((string)entry.Key, (string)entry.Value);
                                    }
                                }
                                else
                                {
                                    // Not a ProjectItemInstance.TaskItem or even a ITaskItem2, so we have to fake it.  
                                    // Setting an item spec expects the escaped value, as does setting metadata. 
                                    newItem = new ProjectItemInstance(_projectInstance, outputTargetName, EscapingUtilities.Escape(output.ItemSpec), parameterLocationEscaped);

                                    foreach (DictionaryEntry entry in output.CloneCustomMetadata())
                                    {
                                        newItem.SetMetadataOnTaskOutput((string)entry.Key, EscapingUtilities.Escape((string)entry.Value));
                                    }
                                }
                            }

                            _batchBucket.Lookup.AddNewItem(newItem);
                        }
                    }

                    if (LogTaskInputs && !_taskLoggingContext.LoggingService.OnlyLogCriticalEvents && outputs.Length > 0 && parameter.Log)
                    {
                        string parameterText = ItemGroupLoggingHelper.GetParameterText(
                            ItemGroupLoggingHelper.OutputItemParameterMessagePrefix,
                            outputTargetName,
                            outputs,
                            parameter.LogItemMetadata);

                        _taskLoggingContext.LogCommentFromText(MessageImportance.Low, parameterText);
                    }
                }
                else
                {
                    // to store an ITaskItem array in a property, join all the item-specs with semi-colons to make the
                    // property value, and ignore/discard the attributes on the ITaskItems.
                    //
                    // An empty ITaskItem[] should create a blank value property, for compatibility.                 
                    StringBuilder joinedOutputs = (outputs.Length == 0) ? new StringBuilder() : null;

                    foreach (ITaskItem output in outputs)
                    {
                        // if individual items in the array are null, ignore them
                        if (output != null)
                        {
                            joinedOutputs ??= new StringBuilder();

                            if (joinedOutputs.Length > 0)
                            {
                                joinedOutputs.Append(';');
                            }

                            if (output is ITaskItem2 outputAsITaskItem2)
                            {
                                joinedOutputs.Append(outputAsITaskItem2.EvaluatedIncludeEscaped);
                            }
                            else
                            {
                                joinedOutputs.Append(EscapingUtilities.Escape(output.ItemSpec));
                            }
                        }
                    }

                    if (joinedOutputs != null)
                    {
                        var outputString = joinedOutputs.ToString();
                        if (LogTaskInputs && !_taskLoggingContext.LoggingService.OnlyLogCriticalEvents)
                        {
                            _taskLoggingContext.LogComment(MessageImportance.Low, "OutputPropertyLogMessage", outputTargetName, outputString);
                        }

                        _batchBucket.Lookup.SetProperty(ProjectPropertyInstance.Create(outputTargetName, outputString, parameterLocation, _projectInstance.IsImmutable));
                    }
                }
            }
        }

        /// <summary>
        /// Gather task outputs in array form
        /// </summary>
        private void GatherArrayStringAndValueOutputs(bool outputTargetIsItem, string outputTargetName, string[] outputs, ElementLocation parameterLocation, TaskPropertyInfo parameter)
        {
            // if the task has generated outputs (if it didn't, don't do anything)            
            if (outputs != null)
            {
                if (outputTargetIsItem)
                {
                    // to store the outputs as items, use the string representations of the outputs as item-specs
                    foreach (string output in outputs)
                    {
                        // if individual outputs in the array are null, ignore them
                        // attempting to put an empty string into an item is a no-op.
                        if (output?.Length > 0)
                        {
                            _batchBucket.Lookup.AddNewItem(new ProjectItemInstance(_projectInstance, outputTargetName, EscapingUtilities.Escape(output), EscapingUtilities.Escape(parameterLocation.File)));
                        }
                    }

                    if (LogTaskInputs && !_taskLoggingContext.LoggingService.OnlyLogCriticalEvents && outputs.Length > 0 && parameter.Log)
                    {
                        string parameterText = ItemGroupLoggingHelper.GetParameterText(
                            ItemGroupLoggingHelper.OutputItemParameterMessagePrefix,
                            outputTargetName,
                            outputs,
                            parameter.LogItemMetadata);
                        _taskLoggingContext.LogCommentFromText(MessageImportance.Low, parameterText);
                    }
                }
                else
                {
                    // to store an object array in a property, join all the string representations of the objects with
                    // semi-colons to make the property value
                    //
                    // An empty ITaskItem[] should create a blank value property, for compatibility.                 
                    StringBuilder joinedOutputs = (outputs.Length == 0) ? new StringBuilder() : null;

                    foreach (string output in outputs)
                    {
                        // if individual outputs in the array are null, ignore them
                        if (output != null)
                        {
                            joinedOutputs ??= new StringBuilder();

                            if (joinedOutputs.Length > 0)
                            {
                                joinedOutputs.Append(';');
                            }

                            joinedOutputs.Append(EscapingUtilities.Escape(output));
                        }
                    }

                    if (joinedOutputs != null)
                    {
                        var outputString = joinedOutputs.ToString();
                        if (LogTaskInputs && !_taskLoggingContext.LoggingService.OnlyLogCriticalEvents)
                        {
                            _taskLoggingContext.LogComment(MessageImportance.Low, "OutputPropertyLogMessage", outputTargetName, outputString);
                        }

                        _batchBucket.Lookup.SetProperty(ProjectPropertyInstance.Create(outputTargetName, outputString, parameterLocation, _projectInstance.IsImmutable));
                    }
                }
            }
        }

        /// <summary>
        /// Finds all the task properties that are required.
        /// Returns them as keys in a dictionary.
        /// </summary>
        /// <returns>Gets a list of properties which are required.</returns>
        private IDictionary<string, string> GetNamesOfPropertiesWithRequiredAttribute()
        {
            ErrorUtilities.VerifyThrow(_taskFactoryWrapper != null, "Expected taskFactoryWrapper to not be null");
            IDictionary<string, string> requiredParameters = null;

            try
            {
                requiredParameters = _taskFactoryWrapper.GetNamesOfPropertiesWithRequiredAttribute;
            }
            catch (Exception e) // Catching Exception, but rethrowing unless it's a well-known exception.
            {
                if (ExceptionHandling.NotExpectedReflectionException(e))
                {
                    throw;
                }

                // Reflection related exception
                _targetLoggingContext.LogError(new BuildEventFileInfo(_taskLocation), "AttributeTypeLoadError", _taskName, e.Message);

                ProjectErrorUtilities.VerifyThrowInvalidProject(false, _taskLocation, "TaskDeclarationOrUsageError", _taskName);
            }

            return requiredParameters;
        }

        /// <summary>
        /// Show a message that cancel has not yet finished.
        /// </summary>
        private void DisplayCancelWaitMessage()
        {
            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out string warningCode, out string helpKeyword, "UnableToCancelTask", _taskName);
            try
            {
                _taskLoggingContext.LogWarningFromText(null, warningCode, helpKeyword, new BuildEventFileInfo(_taskLocation), message);
            }
            catch (InternalErrorException) // BUGBUG, should never catch this
            {
                // We can get an exception from this when we encounter a race between a task finishing and a cancel occurring.  In this situation
                // if the task logging context is no longer valid, we choose to eat the exception because we can't log the message anyway.
                if (_taskLoggingContext.IsValid)
                {
                    throw;
                }
            }
        }
    }
}
