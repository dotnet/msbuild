// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
#if NET
using System.Runtime.CompilerServices;
#endif
#if FEATURE_APPDOMAIN
using System.Runtime.Remoting;
#endif
using System.Text;
using System.Threading;

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using Task = System.Threading.Tasks.Task;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Flags returned by TaskExecutionHost.FindTask().
    /// </summary>
    [Flags]
    internal enum TaskRequirements
    {
        /// <summary>
        /// The task was not found.
        /// </summary>
        None = 0,

        /// <summary>
        /// The task must be executed on an STA thread.
        /// </summary>
        RequireSTAThread = 0x01,

        /// <summary>
        /// The task must be executed in a separate AppDomain.
        /// </summary>
        RequireSeparateAppDomain = 0x02
    }

    /// <summary>
    /// The TaskExecutionHost is responsible for instantiating tasks, setting their parameters and gathering outputs using
    /// reflection, and executing the task in the appropriate context.The TaskExecutionHost does not deal with any part of the task declaration or
    /// XML.
    /// </summary>
    internal class TaskExecutionHost : IDisposable
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
        /// The project file path that runs the task.
        /// </summary>
        private string _projectFile;

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
        /// When the task was resolved from <see cref="TaskClassRegistry"/> (a host-registered task), the
        /// factory that constructs it with no assembly loading or reflection. Non-null only for registered
        /// tasks, which run even when reflective task execution is disabled (trimmed/AOT host).
        /// </summary>
        private RegisteredTaskFactory _registeredTaskFactory;

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

        private readonly PropertyTrackingSetting _propertyTrackingSettings;

        /// <summary>
        /// The task environment to be used by IMultiThreadableTask instances.
        /// </summary>
        internal TaskEnvironment TaskEnvironment { get; set; }

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

            _propertyTrackingSettings = (PropertyTrackingSetting)Traits.Instance.LogPropertyTracking;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskExecutionHost"/> class
        /// for unit testing only.
        /// </summary>
        internal TaskExecutionHost()
        {
            // do nothing
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="TaskExecutionHost"/> class.
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
        public ProjectInstance ProjectInstance => _projectInstance;

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

        private HostServices _hostServices;

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
        public void InitializeForTask(
            IBuildEngine2 buildEngine,
            TargetLoggingContext loggingContext,
            ProjectInstance projectInstance,
            string taskName,
            ElementLocation taskLocation,
            ITaskHost taskHost,
            bool continueOnError,
            string projectFile,
#if FEATURE_APPDOMAIN
            AppDomainSetup appDomainSetup,
#endif
            HostServices hostServices,
            bool isOutOfProc,
            CancellationToken cancellationToken,
            TaskEnvironment taskEnvironment)
        {
            _buildEngine = buildEngine;
            _projectInstance = projectInstance;
            _targetLoggingContext = loggingContext;
            _taskName = taskName;
            _projectFile = projectFile;
            _taskLocation = taskLocation;
            _cancellationTokenRegistration = cancellationToken.Register(Cancel);
            _taskHost = taskHost;
            _taskExecutionIdle.Set();
#if FEATURE_APPDOMAIN
            AppDomainSetup = appDomainSetup;
#endif
            _hostServices = hostServices;
            IsOutOfProc = isOutOfProc;
            TaskEnvironment = taskEnvironment;
        }

        /// <summary>
        /// Ask the task host to find its task in the registry and get it ready for initializing the batch
        /// </summary>
        /// <returns>The task requirements and task factory wrapper if the task is found, (null, null) otherwise.</returns>
        public (TaskRequirements? requirements, TaskFactoryWrapper taskFactoryWrapper) FindTask(in TaskHostParameters taskIdentityParameters)
        {
            if (_taskFactoryWrapper is null)
            {
                // A fresh task resolution: clear any registered-task factory left over from a previous task on
                // this reused host, so an unregistered (e.g. intrinsic) task is not mistaken for the prior
                // registered one when constructing its instance.
                _registeredTaskFactory = null;

                // A host-registered task (TaskClassRegistry) resolves with no assembly loading or by-name
                // type resolution, so it runs even when reflective task execution is disabled - the path a
                // trimmed/AOT host takes. Consult the registry first.
                if (TryCreateRegisteredTaskFactory(out TaskFactoryWrapper registeredTaskFactoryWrapper))
                {
                    _taskFactoryWrapper = registeredTaskFactoryWrapper;
                }
                else if (!FeatureSwitches.EnableReflectiveTaskExecution)
                {
                    // The intrinsic MSBuild and CallTarget tasks are engine-internal types resolved without
                    // reflecting over a runtime-discovered assembly, so they stay available when reflective task
                    // execution is disabled (the trimmed/AOT path) - virtually every real build uses them.
                    if (TryCreateIntrinsicTaskFactory(out TaskFactoryWrapper intrinsicTaskFactoryWrapper))
                    {
                        _taskFactoryWrapper = intrinsicTaskFactoryWrapper;
                    }
                    else
                    {
                        // Loading a task factory/type reflects over an assembly discovered at run time, which a
                        // trimmed/AOT host cannot do. Fail observably with a reported build error (rather than
                        // crashing in reflection) so the host can fall back to a JIT MSBuild. This is the leaf gate
                        // that frees the whole build-execution chain above from carrying [RequiresUnreferencedCode],
                        // and it lets the trimmer remove the reflective task-loading path from the image.
                        ProjectErrorUtilities.ThrowInvalidProject(_taskLocation, "ReflectiveTaskExecutionNotSupported", _taskName);
                        return (null, null);
                    }
                }
                else
                {
                    _taskFactoryWrapper = FindTaskInRegistry(taskIdentityParameters);
                }
            }

            if (_taskFactoryWrapper is null)
            {
                return (null, null);
            }

            TaskRequirements requirements = TaskRequirements.None;

            // HasSTAThreadAttribute / HasLoadInSeparateAppDomainAttribute come from custom attributes
            // ([RunInSTA] / [LoadInSeparateAppDomain]) on the task type. A registered task's LoadedType is
            // rooted for trimming with PublicParameterlessConstructor | PublicProperties only, so under Native
            // AOT those attributes are not preserved and read as false - a registered task declaring them would
            // not get STA / separate-AppDomain treatment. That is acceptable for the in-process registered-task
            // path (separate AppDomains do not exist on .NET Core regardless); the reflective JIT path, which
            // loads the full type, observes the attributes exactly as before.
            if (_taskFactoryWrapper.TaskFactoryLoadedType.HasSTAThreadAttribute)
            {
                requirements |= TaskRequirements.RequireSTAThread;
            }

            if (_taskFactoryWrapper.TaskFactoryLoadedType.HasLoadInSeparateAppDomainAttribute)
            {
                requirements |= TaskRequirements.RequireSeparateAppDomain;

                // we're going to be remoting across the appdomain boundary, so
                // create the list that we'll use to disconnect the taskitems once we're done
                _remotedTaskItems = new List<TaskItem>();
            }

            return (requirements, _taskFactoryWrapper);
        }

        /// <summary>
        /// Attempts to resolve the current task from the host task registry (<see cref="TaskClassRegistry"/>).
        /// A registered task is constructed with no assembly loading or by-name type resolution, so it can run
        /// even in a trimmed/AOT host where reflective task execution is disabled.
        /// </summary>
        /// <param name="taskFactoryWrapper">The wrapper for the registered task, or <see langword="null"/> if the task is not registered.</param>
        /// <returns><see langword="true"/> if the task was found in the registry.</returns>
        private bool TryCreateRegisteredTaskFactory(out TaskFactoryWrapper taskFactoryWrapper)
        {
            if (TaskClassRegistry.TryGetRegistration(_taskName, out TaskClassRegistration registration))
            {
                LoadedType loadedType = registration.GetLoadedType();
                _registeredTaskFactory = new RegisteredTaskFactory(registration, loadedType);
                taskFactoryWrapper = new TaskFactoryWrapper(_registeredTaskFactory, loadedType, _taskName, TaskHostParameters.Empty);
                return true;
            }

            taskFactoryWrapper = null;
            return false;
        }

        /// <summary>
        /// Attempts to resolve the current task as an intrinsic engine task (<c>MSBuild</c> or <c>CallTarget</c>).
        /// These map to engine-internal types via <see cref="IntrinsicTaskFactory"/> with no reflection over a
        /// runtime-discovered assembly, so they remain usable when reflective task execution is disabled (the
        /// trimmed/Native AOT path). The reflective path resolves them by type in <see cref="FindTaskInRegistry"/>.
        /// </summary>
        /// <param name="taskFactoryWrapper">The wrapper for the intrinsic task, or <see langword="null"/> if the task is not intrinsic.</param>
        /// <returns><see langword="true"/> if the task is an intrinsic engine task.</returns>
        private bool TryCreateIntrinsicTaskFactory(out TaskFactoryWrapper taskFactoryWrapper)
        {
            if (string.Equals(_taskName, "MSBuild", StringComparison.OrdinalIgnoreCase))
            {
                taskFactoryWrapper = CreateIntrinsicTaskFactoryWrapper(typeof(MSBuild));
                return true;
            }

            if (string.Equals(_taskName, "CallTarget", StringComparison.OrdinalIgnoreCase))
            {
                taskFactoryWrapper = CreateIntrinsicTaskFactoryWrapper(typeof(CallTarget));
                return true;
            }

            taskFactoryWrapper = null;
            return false;
        }

        /// <summary>
        /// Builds a <see cref="TaskFactoryWrapper"/> for an intrinsic engine task (<c>MSBuild</c> or
        /// <c>CallTarget</c>) by direct type reference - no assembly probing or by-name resolution. Shared by
        /// the reflective resolution path (<see cref="FindTaskInRegistry"/>) and the reflection-free path
        /// (<see cref="TryCreateIntrinsicTaskFactory"/>) so the two constructions cannot drift.
        /// </summary>
        private TaskFactoryWrapper CreateIntrinsicTaskFactoryWrapper(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.PublicProperties)] Type intrinsicTaskType)
        {
            Assembly taskExecutionHostAssembly = typeof(TaskExecutionHost).Assembly;
            return new TaskFactoryWrapper(
                new IntrinsicTaskFactory(intrinsicTaskType),
                new LoadedType(intrinsicTaskType, AssemblyLoadInfo.Create(taskExecutionHostAssembly.FullName, null), taskExecutionHostAssembly, typeof(ITaskItem)),
                _taskName,
                TaskHostParameters.Empty);
        }

        /// <summary>
        /// Initialize to run a specific batch of the current task.
        /// </summary>
        public bool InitializeForBatch(TaskLoggingContext loggingContext, ItemBucket batchBucket, in TaskHostParameters taskIdentityParameters, int scheduledNodeId)
        {
            ArgumentNullException.ThrowIfNull(loggingContext);

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
            // A registered task's assembly is already loaded (the host referenced it statically), so it
            // needs no assembly-resolve handler.
            if (_registeredTaskFactory is null && _resolver == null)
            {
                _resolver = new TaskEngineAssemblyResolver();
                _resolver.Initialize(_taskFactoryWrapper.TaskFactoryLoadedType.Assembly.AssemblyFile);
                _resolver.InstallHandler();
            }
#endif

            // We instantiate a new task object for each batch.
            if (_registeredTaskFactory is not null)
            {
                // Reflection-free construction via the host-supplied factory. This deliberately avoids the
                // [RequiresUnreferencedCode] ITaskFactory.CreateTask interface member, so the registered-task
                // path carries no trim warning and runs under Native AOT.
                TaskInstance = _registeredTaskFactory.CreateRegisteredTask();
            }
            else if (!FeatureSwitches.EnableReflectiveTaskExecution && _taskFactoryWrapper.TaskFactory is IntrinsicTaskFactory intrinsicTaskFactory)
            {
                // Reflective-OFF (trimmed/AOT) path only: construct the intrinsic MSBuild/CallTarget task by
                // direct `new` (no reflection), because the reflective InstantiateTask below is gated off and
                // dead-stripped under trimming. Under the JIT default (switch on) intrinsic tasks deliberately
                // fall through to InstantiateTask exactly as before, so they keep their TaskFactoryEngineContext
                // lifecycle and ProjectTelemetry accounting - this branch must not alter the JIT path.
                TaskInstance = intrinsicTaskFactory.CreateIntrinsicTask();
            }
            else if (!FeatureSwitches.EnableReflectiveTaskExecution)
            {
                // See FindTask: instantiating an unregistered task reflects over a runtime-discovered type, so
                // a trimmed/AOT host fails observably here. Normally unreachable - FindTask already failed -
                // but it keeps the reflective InstantiateTask below behind the feature guard.
                ProjectErrorUtilities.ThrowInvalidProject(_taskLocation, "ReflectiveTaskExecutionNotSupported", _taskName);
                return false;
            }
            else
            {
                TaskInstance = InstantiateTask(scheduledNodeId, taskIdentityParameters);
            }

            if (TaskInstance == null)
            {
                return false;
            }

            // The task-assembly location-mismatch diagnostic reads Assembly.Location, which is empty (and
            // meaningless) in a single-file/Native AOT host - and a registered task is already the loaded
            // type. On .NET, guard the read on dynamic-code support so ILC dead-strips it (and its IL3000)
            // under Native AOT while the JIT keeps the diagnostic; .NET Framework (no AOT) always runs it.
#if NET
            if (RuntimeFeature.IsDynamicCodeSupported)
#endif
            {
                // When MSBuild loads a task assembly, it uses Assembly.LoadFrom() with a specific path, but
                // .NET then loads based on the assembly identity with that path only as a hint. This can
                // result in the assembly being loaded from a different location than expected (for example
                // from the GAC, or because something already loaded the same identity from another path),
                // which can cause confusing task behavior. This validation logs a message when the loaded
                // assembly location does not match the path we resolved the task from.
                string realTaskAssemblyLocation = TaskInstance.GetType().Assembly.Location;
                if (!string.IsNullOrWhiteSpace(realTaskAssemblyLocation) && realTaskAssemblyLocation != _taskFactoryWrapper.TaskFactoryLoadedType.Path)
                {
                    if (!IsTaskAssemblyMatchFactoryType())
                    {
                        _taskLoggingContext.LogComment(MessageImportance.Normal, "TaskAssemblyLocationMismatch", realTaskAssemblyLocation, _taskFactoryWrapper.TaskFactoryLoadedType.Path);
                    }
                }
            }

            TaskInstance.BuildEngine = _buildEngine;
            TaskInstance.HostObject = _taskHost;

            if (TaskInstance is IMultiThreadableTask multiThreadableTask)
            {
                multiThreadableTask.TaskEnvironment = TaskEnvironment;
            }

            return true;

            // Function to validate that if this is a TaskHostTask, the assembly it loaded is the same one we found in the registry.
            bool IsTaskAssemblyMatchFactoryType() => TaskInstance is not TaskHostTask tht
                || tht.LoadedTaskAssemblyInfo.AssemblyLocation == _taskFactoryWrapper.TaskFactoryLoadedType.Path;
        }

        /// <summary>
        /// Sets all of the specified parameters on the task.
        /// </summary>
        /// <param name="parameters">The name/value pairs for the parameters.</param>
        /// <returns>True if the parameters were set correctly, false otherwise.</returns>
        public bool SetTaskParameters(IDictionary<string, (string, ElementLocation)> parameters)
        {
            if (_registeredTaskFactory is null && _taskFactoryWrapper.TaskFactory is not IntrinsicTaskFactory && !FeatureSwitches.EnableReflectiveTaskExecution)
            {
                // Binding task parameters reflects over the task type. A registered task's type is trim-rooted
                // (so binding stays trim-safe) and is exempt, as is an intrinsic MSBuild/CallTarget task (an
                // engine-internal type); for any other task in a trimmed/AOT host this fails observably. See
                // FindTask: normally unreachable (FindTask fails first).
                ProjectErrorUtilities.ThrowInvalidProject(_taskLocation, "ReflectiveTaskExecutionNotSupported", _taskName);
                return false;
            }

            ArgumentNullException.ThrowIfNull(parameters);

            bool taskInitialized = true;

            // Get the properties that exist on this task.  We need to gather all of the ones that are marked
            // "required" so that we can keep track of whether or not they all get set.
            var setParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyDictionary<string, string> requiredParameters = GetNamesOfPropertiesWithRequiredAttribute();

            // look through all the attributes of the task element
            foreach (KeyValuePair<string, (string, ElementLocation)> parameter in parameters)
            {
                bool taskParameterSet = false;  // Did we actually call the setter on this task parameter?
                bool success;

                try
                {
                    success = SetTaskParameter(parameter.Key, parameter.Value.Item1, parameter.Value.Item2, requiredParameters.ContainsKey(parameter.Key), out taskParameterSet);
                }
                catch (Exception e) when (!ExceptionHandling.NotExpectedReflectionException(e))
                {
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

            if (TaskInstance is IIncrementalTask incrementalTask)
            {
                incrementalTask.FailIfNotIncremental = _buildComponentHost.BuildParameters.Question;
            }

            if (taskInitialized)
            {
                // See if any required properties were not set
                foreach (KeyValuePair<string, string> requiredParameter in requiredParameters)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(
                        setParameters.ContainsKey(requiredParameter.Key),
                        _taskLocation,
                        "RequiredPropertyNotSetError",
                        _taskName,
                        requiredParameter.Key);
                }
            }

            return taskInitialized;
        }

        /// <summary>
        /// Retrieve the outputs from the task.
        /// </summary>
        /// <returns>True of the outputs were gathered successfully, false otherwise.</returns>
        public bool GatherTaskOutputs(string parameterName, ElementLocation parameterLocation, bool outputTargetIsItem, string outputTargetName)
        {
            Assumed.NotNull(_taskFactoryWrapper, "Need a taskFactoryWrapper to retrieve outputs from.");

            bool gatheredGeneratedOutputsSuccessfully = true;

            try
            {
                TaskPropertyInfo parameter = _taskFactoryWrapper.GetProperty(parameterName);
                foreach (TaskPropertyInfo prop in _taskFactoryWrapper.TaskFactoryLoadedType.Properties)
                {
                    if (prop.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase))
                    {
                        parameter = prop;
                        break;
                    }
                }

                // flag an error if we find a parameter that has no .NET property equivalent
                ProjectErrorUtilities.VerifyThrowInvalidProject(
                    parameter != null,
                    parameterLocation,
                    "UnexpectedTaskOutputAttribute",
                    parameterName,
                    _taskName);

                // output parameters must have their corresponding .NET properties marked with the Output attribute
                ProjectErrorUtilities.VerifyThrowInvalidProject(
                    _taskFactoryWrapper.GetNamesOfPropertiesWithOutputAttribute.ContainsKey(parameterName),
                    parameterLocation,
                    "UnmarkedOutputTaskParameter",
                    parameter.Name,
                    _taskName);

                EnsureParameterInitialized(parameter, _batchBucket.Lookup);

                if (parameter.IsAssignableToITask)
                {
                    ITaskItem[] outputs = GetItemOutputs(parameter);
                    GatherTaskItemOutputs(outputTargetIsItem, outputTargetName, outputs, parameterLocation, parameter);
                }
                else if (parameter.IsValueTypeOutputParameter)
                {
                    string[] outputs = GetValueOutputs(parameter);
                    GatherArrayStringAndValueOutputs(outputTargetIsItem, outputTargetName, outputs, parameterLocation, parameter);
                }
                else
                {
                    ProjectErrorUtilities.ThrowInvalidProject(
                        parameterLocation,
                        "UnsupportedTaskParameterTypeError",
                        parameter.PropertyType.FullName,
                        parameter.Name,
                        _taskName);
                }
            }
            catch (InvalidOperationException e)
            {
                // handle invalid TaskItems in task outputs
                _targetLoggingContext.LogError(
                    new BuildEventFileInfo(parameterLocation),
                    "InvalidTaskItemsInTaskOutputs",
                    _taskName,
                    parameterName,
                    e.Message);

                gatheredGeneratedOutputsSuccessfully = false;
            }
            catch (TargetInvocationException e)
            {
                // handle any exception thrown by the task's getter
                // Exception thrown by the called code itself
                // Log the stack, so the task vendor can fix their code
                // Log the task line number, whatever the value of ContinueOnError;
                // because this will be a hard error anyway.
                _targetLoggingContext.LogFatalTaskError(
                    e.InnerException,
                    new BuildEventFileInfo(parameterLocation),
                    _taskName);

                // We do not recover from a task exception while getting outputs,
                // so do not merely set gatheredGeneratedOutputsSuccessfully = false; here
                ProjectErrorUtilities.ThrowInvalidProject(
                    parameterLocation,
                    "FailedToRetrieveTaskOutputs",
                    _taskName,
                    parameterName,
                    e.InnerException?.Message);
            }
            catch (Exception e) when (!ExceptionHandling.NotExpectedReflectionException(e))
            {
                ProjectErrorUtilities.ThrowInvalidProject(
                    parameterLocation,
                    "FailedToRetrieveTaskOutputs",
                    _taskName,
                    parameterName,
                    e.Message);
            }

            return gatheredGeneratedOutputsSuccessfully;
        }

        /// <summary>
        /// Cleans up after running a batch.
        /// </summary>
        public void CleanupForBatch()
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
        public void CleanupForTask()
        {
#if FEATURE_APPDOMAIN
            if (_resolver != null)
            {
                _resolver.RemoveHandler();
                _resolver = null;
            }
#endif

            _taskFactoryWrapper = null;

            // Clear the registered-task factory too, so it cannot leak into the next task that reuses this host.
            _registeredTaskFactory = null;

            // We must null this out because it could be a COM object (or any other ref-counted object) which needs to
            // be released.
            _taskHost = null;
            CleanupCancellationToken();

            Assumed.Null(TaskInstance, "Task Instance should be null");
        }

        /// <summary>
        /// Executes the task.
        /// </summary>
        public bool Execute()
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
                Debug.Assert(TaskInstance is not IMultiThreadableTask multiThreadableTask || multiThreadableTask.TaskEnvironment != null, "task environment missing for multi-threadable task");
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
                catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                {
                    try
                    {
                        _taskLoggingContext.LogFatalTaskError(e, new BuildEventFileInfo(_taskLocation), ((ProjectTaskInstance)_taskLoggingContext.Task).Name);
                    }

                    // If this fails it could be due to the task logging context no longer being valid due to a race condition where the task completes while we
                    // are in this method.  In that case we simply ignore the exception and carry on since we can't log anything anyhow.
                    catch (InternalErrorException) when (!_taskLoggingContext.IsValid)
                    {
                    }
                }
            }

            // Let the task finish now.  If cancellation worked, hopefully it finishes sooner than it would have otherwise.
            // If the task builder crashed, this could have already been disposed
            if (!_taskExecutionIdle.SafeWaitHandle.IsClosed)
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
        /// Checks if a type is TaskItem&lt;T&gt; or ITaskItem&lt;T&gt; where T is path-like.
        /// </summary>
        private static bool IsPathLikeTaskItemOrITaskItemOfT(Type parameterType)
            => TaskParameterTypeVerifier.IsPathLikeITaskItemOfT(parameterType)
                || TaskParameterTypeVerifier.IsPathLikeTaskItemOfT(parameterType, typeof(TaskItem<>).FullName);

        /// <summary>
        /// Cache of compiled constructor delegates that wrap an <see cref="ITaskItem"/> into a
        /// <see cref="TaskItem{T}"/>, keyed by the generic argument T. Building the closed generic type and
        /// resolving the constructor via reflection on every parameter binding is expensive, so the delegate
        /// is compiled once per T and reused.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Func<ITaskItem, ITaskItem>> s_taskItemOfTFactories = new();

        /// <summary>
        /// Wraps the given <paramref name="item"/> into a <c>TaskItem&lt;T&gt;</c> where T is
        /// <paramref name="genericArgument"/>, using a cached compiled constructor delegate.
        /// </summary>
        private static ITaskItem CreateTaskItemOfT(Type genericArgument, ITaskItem item)
        {
            Func<ITaskItem, ITaskItem> factory = s_taskItemOfTFactories.GetOrAdd(genericArgument, static t =>
            {
#if NET
                if (!RuntimeFeature.IsDynamicCodeSupported)
                {
                    // Wrapping an ITaskItem into a closed-generic TaskItem<T> requires Type.MakeGenericType
                    // plus an expression-tree Compile(), both of which need runtime code generation. Fail
                    // observably under trimming / Native AOT rather than silently mis-binding the typed task
                    // parameter. (See documentation/aot/follow-up-work.md - the typed TaskItem<T> parameter
                    // feature still needs a proper AOT-safe binding strategy.)
                    throw new NotSupportedException(
                        "Task parameters typed as TaskItem<T> or ITaskItem<T> require runtime code generation " +
                        "(Type.MakeGenericType and expression compilation) and are not supported when MSBuild " +
                        "runs trimmed or with Native AOT.");
                }
#endif
                ConstructorInfo constructor = typeof(TaskItem<>).MakeGenericType(t).GetConstructor([typeof(ITaskItem)]);
                ParameterExpression itemParameter = Expression.Parameter(typeof(ITaskItem), "item");
                return Expression.Lambda<Func<ITaskItem, ITaskItem>>(
                    Expression.Convert(Expression.New(constructor, itemParameter), typeof(ITaskItem)),
                    itemParameter).Compile();
            });

            return factory(item);
        }


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
            return InternalSetTaskParameter(parameter, ConvertStringToParameterValue(expandedParameterValue, parameterType));
        }

        /// <summary>
        /// Converts a single string value to an instance of <paramref name="targetType"/>, applying the same
        /// conversions for both scalar parameters and the elements of array parameters.
        /// </summary>
        private object ConvertStringToParameterValue(string value, Type targetType)
        {
            // Path-like types are resolved through TaskEnvironment so the path is rooted consistently
            // with the rest of the build before being handed to the task.
            if (targetType == typeof(AbsolutePath))
            {
                return TaskEnvironment.GetAbsolutePath(value);
            }

            if (targetType == typeof(FileInfo))
            {
                return new FileInfo(TaskEnvironment.GetAbsolutePath(value).Value);
            }

            if (targetType == typeof(DirectoryInfo))
            {
                return new DirectoryInfo(TaskEnvironment.GetAbsolutePath(value).Value);
            }

            return ValueTypeParser.Parse(value, targetType);
        }

        /// <summary>
        /// Called on the local side.
        /// </summary>
        private bool SetParameterArray(TaskPropertyInfo parameter, Type parameterType, IList<TaskItem> taskItems, ElementLocation parameterLocation)
        {
            TaskItem currentItem = null;

            try
            {
                Type elementType = parameterType.GetElementType();

                if (parameterType == typeof(ITaskItem[]))
                {
                    ITaskItem[] finalInputs = new ITaskItem[taskItems.Count];
                    for (int i = 0; i < taskItems.Count; i++)
                    {
                        TaskItem item = taskItems[i];
                        currentItem = item;

                        // if we've been asked to remote these items then
                        // remember them so we can disconnect them from remoting later
                        RecordItemForDisconnectIfNecessary(item);
                        finalInputs[i] = item;
                    }

                    return InternalSetTaskParameter(parameter, finalInputs);
                }
                else if (IsPathLikeTaskItemOrITaskItemOfT(elementType))
                {
                    // TaskItem<T> / ITaskItem<T> arrays: wrap each item into the closed generic TaskItem<T>.
#if NET
                    // AOT friendly
                    Array finalInputs = Array.CreateInstanceFromArrayType(parameterType, taskItems.Count);
#else
                    Array finalInputs = Array.CreateInstance(elementType, taskItems.Count);
#endif
                    for (int i = 0; i < taskItems.Count; i++)
                    {
                        TaskItem item = taskItems[i];
                        currentItem = item;
                        RecordItemForDisconnectIfNecessary(item);
                        ITaskItem taskItemOfT = CreateTaskItemOfT(elementType.GetGenericArguments()[0], item);
                        finalInputs.SetValue(taskItemOfT, i);
                    }

                    return InternalSetTaskParameter(parameter, finalInputs);
                }
                else if (parameterType == typeof(string[]))
                {
                    string[] finalInputs = new string[taskItems.Count];
                    for (int i = 0; i < taskItems.Count; i++)
                    {
                        currentItem = taskItems[i];
                        finalInputs[i] = currentItem.ItemSpec;
                    }

                    return InternalSetTaskParameter(parameter, finalInputs);
                }
                else if (parameterType == typeof(bool[]))
                {
                    bool[] finalInputs = new bool[taskItems.Count];
                    for (int i = 0; i < taskItems.Count; i++)
                    {
                        currentItem = taskItems[i];
                        finalInputs[i] = ConversionUtilities.ConvertStringToBool(currentItem.ItemSpec);
                    }

                    return InternalSetTaskParameter(parameter, finalInputs);
                }
                else
                {
                    // Fallback for custom value types and path-like scalar types (AbsolutePath/FileInfo/DirectoryInfo).
#if NET
                    // AOT friendly
                    Array finalTaskInputs = Array.CreateInstanceFromArrayType(parameterType, taskItems.Count);
#else
                    Array finalTaskInputs = Array.CreateInstance(elementType, taskItems.Count);
#endif
                    for (int i = 0; i < taskItems.Count; i++)
                    {
                        TaskItem item = taskItems[i];
                        currentItem = item;
                        finalTaskInputs.SetValue(ConvertStringToParameterValue(item.ItemSpec, elementType), i);
                    }

                    return InternalSetTaskParameter(parameter, finalTaskInputs);
                }
            }
            catch (Exception ex)
            {
                if (ex is InvalidCastException || // invalid type
                    ex is ArgumentException || // can't convert to bool
                    ex is FormatException || // bad string representation of a type
                    ex is OverflowException) // overflow when converting string representation of a numerical type
                {
                    ProjectErrorUtilities.ThrowInvalidProject(
                        parameterLocation,
                        "InvalidTaskParameterValueError",
                        currentItem.ItemSpec,
                        parameter.Name,
                        parameterType.FullName,
                        _taskName);
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

            if (outputs is ITaskItem[] taskItemOutputs)
            {
                return taskItemOutputs;
            }

            if (outputs == null)
            {
                return null;
            }

            Type outputType = outputs.GetType();
            if (outputType.IsArray && IsPathLikeTaskItemOrITaskItemOfT(outputType.GetElementType()))
            {
                Array taskItemArray = (Array)outputs;
                ITaskItem[] result = new ITaskItem[taskItemArray.Length];
                for (int i = 0; i < taskItemArray.Length; i++)
                {
                    result[i] = (ITaskItem)taskItemArray.GetValue(i);
                }

                return result;
            }

            return [(ITaskItem)outputs];
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
                    stringOutputs[i] = ValueTypeParser.ToString(output);
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
        [RequiresUnreferencedCode("Creates and loads a task factory by reflecting over an assembly discovered at runtime, which is incompatible with trimming.")]
        private TaskFactoryWrapper FindTaskInRegistry(in TaskHostParameters taskIdentityParameters)
        {
            if (!_intrinsicTasks.TryGetValue(_taskName, out TaskFactoryWrapper returnClass))
            {
                returnClass = _projectInstance.TaskRegistry.GetRegisteredTask(_taskName, null, taskIdentityParameters, true /* exact match */, _targetLoggingContext, _taskLocation, _buildComponentHost?.BuildParameters?.MultiThreaded ?? false);
                if (returnClass == null)
                {
                    returnClass = _projectInstance.TaskRegistry.GetRegisteredTask(_taskName, null, taskIdentityParameters, false /* fuzzy match */, _targetLoggingContext, _taskLocation, _buildComponentHost?.BuildParameters?.MultiThreaded ?? false);

                    if (returnClass == null)
                    {
                        returnClass = _projectInstance.TaskRegistry.GetRegisteredTask(_taskName, null, TaskHostParameters.Empty, true /* exact match */, _targetLoggingContext, _taskLocation, _buildComponentHost?.BuildParameters?.MultiThreaded ?? false);

                        if (returnClass == null)
                        {
                            returnClass = _projectInstance.TaskRegistry.GetRegisteredTask(_taskName, null, TaskHostParameters.Empty, false /* fuzzy match */, _targetLoggingContext, _taskLocation, _buildComponentHost?.BuildParameters?.MultiThreaded ?? false);

                            if (returnClass == null)
                            {
                                _targetLoggingContext.LogError(
                                        new BuildEventFileInfo(_taskLocation),
                                        "MissingTaskError",
                                        _taskName,
                                        _projectInstance.TaskRegistry.Toolset.ToolsPath);

                                return null;
                            }
                        }

                        _targetLoggingContext.LogError(
                                new BuildEventFileInfo(_taskLocation),
                                "TaskExistsButHasMismatchedIdentityError",
                                _taskName,
                                returnClass.FactoryIdentityParameters.Runtime ?? XMakeAttributes.MSBuildRuntimeValues.any,
                                returnClass.FactoryIdentityParameters.Architecture ?? XMakeAttributes.MSBuildArchitectureValues.any,
                                taskIdentityParameters.Runtime ?? XMakeAttributes.MSBuildRuntimeValues.any,
                                taskIdentityParameters.Architecture ?? XMakeAttributes.MSBuildArchitectureValues.any);

                        // if we've logged this error, even though we've found something, we want to act like we didn't.
                        return null;
                    }
                }

                // Map to an intrinsic task, if necessary.
                if (String.Equals(returnClass.TaskFactory.TaskType.FullName, "Microsoft.Build.Tasks.MSBuild", StringComparison.OrdinalIgnoreCase))
                {
                    returnClass = CreateIntrinsicTaskFactoryWrapper(typeof(MSBuild));
                    _intrinsicTasks[_taskName] = returnClass;
                }
                else if (String.Equals(returnClass.TaskFactory.TaskType.FullName, "Microsoft.Build.Tasks.CallTarget", StringComparison.OrdinalIgnoreCase))
                {
                    returnClass = CreateIntrinsicTaskFactoryWrapper(typeof(CallTarget));
                    _intrinsicTasks[_taskName] = returnClass;
                }
            }

            return returnClass;
        }

        /// <summary>
        /// Instantiates the task.
        /// </summary>
        [RequiresUnreferencedCode("Instantiates a task by reflecting over a task type discovered at runtime, which is incompatible with trimming.")]
        private ITask InstantiateTask(int scheduledNodeId, in TaskHostParameters taskIdentityParameters)
        {
            ITask task = null;

            try
            {
                if (_taskFactoryWrapper.TaskFactory is AssemblyTaskFactory assemblyTaskFactory)
                {
                    task = assemblyTaskFactory.CreateTaskInstance(
                        _taskLocation,
                        _taskLoggingContext,
                        _buildComponentHost,
                        taskIdentityParameters,
                        _projectFile,
                        _hostServices,
#if FEATURE_APPDOMAIN
                        AppDomainSetup,
#endif
                        IsOutOfProc,
                        scheduledNodeId,
                        ProjectInstance.GetProperty,
                        TaskEnvironment);
                }
                else
                {
                    TaskFactoryEngineContext taskFactoryEngineContext = new TaskFactoryEngineContext(_buildEngine.IsRunningMultipleNodes, _taskLocation, _taskLoggingContext, _buildComponentHost?.BuildParameters?.MultiThreaded ?? false, Traits.Instance.ForceTaskFactoryOutOfProc);
                    bool isTaskHost = false;
                    try
                    {
                        // Check if we should force out-of-process execution for non-AssemblyTaskFactory instances
                        // This happens when: 1) Environment variable is set, OR 2) MultiThreaded build is enabled
                        // IntrinsicTaskFactory tasks run in proc always
                        bool shouldRunOutOfProc = TaskFactoryUtilities.ShouldCompileForOutOfProcess(taskFactoryEngineContext)
                                                  && _taskFactoryWrapper.TaskFactory is not IntrinsicTaskFactory;

                        if (shouldRunOutOfProc)
                        {
                            // Custom Task factories are not supported, internal TaskFactories implement this marker interface
                            if (_taskFactoryWrapper.TaskFactory is not IOutOfProcTaskFactory outOfProcTaskFactory)
                            {
                                _taskLoggingContext.LogError(
                                    new BuildEventFileInfo(_taskLocation),
                                    "CustomTaskFactoryOutOfProcNotSupported",
                                    _taskFactoryWrapper.TaskFactory.FactoryName,
                                    _taskName);
                                return null;
                            }

                            task = CreateTaskHostTaskForOutOfProcFactory(taskIdentityParameters, taskFactoryEngineContext, outOfProcTaskFactory, scheduledNodeId);
                            isTaskHost = true;
                        }

                        // Normal in-process execution for custom task factories
                        else
                        {
                            // ITaskFactory2 is here for compat reasons
                            if (_taskFactoryWrapper.TaskFactory is ITaskFactory2 taskFactory2)
                            {
                                task = taskFactory2.CreateTask(taskFactoryEngineContext, taskIdentityParameters.ToDictionary());
                            }
                            else
                            {
                                task = _taskFactoryWrapper.TaskFactory is ITaskFactory3 taskFactory3
                                    ? taskFactory3.CreateTask(taskFactoryEngineContext, taskIdentityParameters)
                                    : _taskFactoryWrapper.TaskFactory.CreateTask(taskFactoryEngineContext);
                            }
                        }

                        // Track telemetry for non-AssemblyTaskFactory task factories
                        _taskLoggingContext?.TargetLoggingContext?.ProjectLoggingContext?.ProjectTelemetry?.AddTaskExecution(_taskFactoryWrapper.TaskFactory.GetType().FullName, isTaskHost);
                    }
                    finally
                    {
#if FEATURE_APPDOMAIN
                        taskFactoryEngineContext.MarkAsInactive();
#endif
                    }
                }
            }
            catch (InvalidCastException e)
            {
                _taskLoggingContext.LogError(
                    new BuildEventFileInfo(_taskLocation),
                    "TaskInstantiationFailureErrorInvalidCast",
                    _taskName,
                    _taskFactoryWrapper.TaskFactory.FactoryName,
                    e.Message);
            }
            catch (TargetInvocationException e)
            {
                // Exception thrown by the called code itself
                // Log the stack, so the task vendor can fix their code
                _taskLoggingContext.LogError(
                    new BuildEventFileInfo(_taskLocation),
                    "TaskInstantiationFailureError",
                    _taskName,
                    _taskFactoryWrapper.TaskFactory.FactoryName,
                    Environment.NewLine + e.InnerException);
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                // Reflection related exception
                _taskLoggingContext.LogError(
                    new BuildEventFileInfo(_taskLocation),
                    "TaskInstantiationFailureError",
                    _taskName,
                    _taskFactoryWrapper.TaskFactory.FactoryName,
                    e.Message);
            }

            return task;
        }

        /// <summary>
        /// Set the specified parameter based on its type.
        /// </summary>
        private bool SetTaskParameter(
            string parameterName,
            string parameterValue,
            ElementLocation parameterLocation,
            bool isRequired,
            out bool parameterSet)
        {
            bool success = false;
            parameterSet = false;

            try
            {
                // check if the task has a .NET property corresponding to the parameter
                LoadedType loadedType = _taskFactoryWrapper.TaskFactoryLoadedType;
                int indexOfParameter = -1;
                for (int i = 0; i < loadedType.Properties.Length; i++)
                {
                    if (loadedType.Properties[i].Name.Equals(parameterName))
                    {
                        indexOfParameter = i;
                        break;
                    }
                }

                // For most tasks, finding the parameter in our list of known properties is equivalent to
                // saying the task was properly invoked, as far as this parameter is concerned. However,
                // that is not true for CodeTaskFactories like RoslynCodeTaskFactory. In that case, they
                // will often have a list of parameters under the UsingTask declaration. Fortunately, if
                // your TaskFactory is RoslynCodeTaskFactory, it isn't TaskHostFactory, which means the
                // types are fully loaded at this stage, and we can access them as we had in the past.
                TaskPropertyInfo parameter = null;
                Type parameterType = null;
                if (indexOfParameter != -1)
                {
                    parameter = loadedType.Properties[indexOfParameter];
                    parameterType = ResolveTaskParameterType(loadedType, parameter, indexOfParameter);
                }
                else
                {
                    parameter = _taskFactoryWrapper.GetProperty(parameterName);
                    if (parameter != null)
                    {
                        parameterType = ResolveTaskParameterType(loadedType, parameter, indexOfParameter: -1);
                    }
                }

                if (parameter != null)
                {
                    EnsureParameterInitialized(parameter, _batchBucket.Lookup);

                    // try to set the parameter
                    if (TaskParameterTypeVerifier.IsValidScalarInputParameter(parameterType))
                    {
                        success = InitializeTaskScalarParameter(
                            parameter,
                            parameterType,
                            parameterValue,
                            parameterLocation,
                            out parameterSet);
                    }
                    else if (TaskParameterTypeVerifier.IsValidVectorInputParameter(parameterType))
                    {
                        success = InitializeTaskVectorParameter(
                            parameter,
                            parameterType,
                            parameterValue,
                            parameterLocation,
                            isRequired,
                            out parameterSet);
                    }
                    else
                    {
                        _taskLoggingContext.LogError(
                            new BuildEventFileInfo(parameterLocation),
                            "UnsupportedTaskParameterTypeError",
                            parameterType.FullName,
                            parameter.Name,
                            _taskName);
                    }

                    if (!success)
                    {
                        // flag an error if the parameter could not be set
                        _taskLoggingContext.LogError(
                            new BuildEventFileInfo(parameterLocation),
                            "InvalidTaskAttributeError",
                            parameterName,
                            parameterValue,
                            _taskName);
                    }
                }
                else
                {
                    // flag an error if we find a parameter that has no .NET property equivalent
                    _taskLoggingContext.LogError(
                        new BuildEventFileInfo(parameterLocation),
                        "UnexpectedTaskAttribute",
                        parameterName,
                        _taskName,
                        _taskFactoryWrapper.TaskFactoryLoadedType.LoadedAssemblyName.FullName,
                        _taskFactoryWrapper.TaskFactoryLoadedType.Path);
                }
            }
            catch (AmbiguousMatchException)
            {
                _taskLoggingContext.LogError(
                    new BuildEventFileInfo(parameterLocation),
                    "AmbiguousTaskParameterError",
                    _taskName,
                    parameterName);
            }
            catch (ArgumentException)
            {
                ProjectErrorUtilities.ThrowInvalidProject(
                    parameterLocation,
                    "SetAccessorNotAvailableOnTaskParameter",
                    parameterName,
                    _taskName);
            }

            return success;
        }

        /// <summary>
        /// Resolves the .NET <see cref="Type"/> of a task parameter for binding.
        /// </summary>
        /// <remarks>
        /// For an in-proc task the property's <see cref="TaskPropertyInfo.PropertyType"/> is already the live,
        /// usable type, so no reflection is required - this is the path a registered task and every in-proc
        /// task take. Only a type loaded via <c>MetadataLoadContext</c> (the out-of-proc task host) is
        /// reflection-only and must be re-resolved by assembly-qualified name; that path is reflective and is
        /// gated behind <see cref="FeatureSwitches.EnableReflectiveTaskExecution"/>, so a trimmed/AOT image
        /// (which never loads task types via <c>MetadataLoadContext</c>) drops it.
        /// </remarks>
        private static Type ResolveTaskParameterType(LoadedType loadedType, TaskPropertyInfo parameter, int indexOfParameter)
        {
            if (!loadedType.LoadedViaMetadataLoadContext)
            {
                return parameter.PropertyType;
            }

            if (FeatureSwitches.EnableReflectiveTaskExecution)
            {
                return ResolveTaskParameterTypeByName(loadedType, parameter, indexOfParameter);
            }

            return null;
        }

        /// <summary>
        /// Re-resolves a <c>MetadataLoadContext</c>-loaded parameter type into the live runtime by its
        /// assembly-qualified name.
        /// </summary>
        [RequiresUnreferencedCode("Resolves the task parameter type from its assembly-qualified name by reflection, which is incompatible with trimming.")]
        private static Type ResolveTaskParameterTypeByName(LoadedType loadedType, TaskPropertyInfo parameter, int indexOfParameter)
        {
            string assemblyQualifiedName =
                (indexOfParameter != -1 ? loadedType.PropertyAssemblyQualifiedNames?[indexOfParameter] : null)
                ?? parameter.PropertyType.AssemblyQualifiedName;
            return Type.GetType(assemblyQualifiedName);
        }

        /// <summary>
        /// Given an instantiated task, this helper method sets the specified scalar parameter based on its type.
        /// </summary>
        private bool InitializeTaskScalarParameter(
            TaskPropertyInfo parameter,
            Type parameterType,
            string parameterValue,
            ElementLocation parameterLocation,
            out bool taskParameterSet)
        {
            taskParameterSet = false;

            bool success;

            try
            {
                if (parameterType == typeof(ITaskItem) || IsPathLikeTaskItemOrITaskItemOfT(parameterType))
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
                            // We only allow a single item to be passed into a parameter of ITaskItem or TaskItem<T>.

                            // Some of the computation (expansion) here is expensive, so don't switch to VerifyThrowInvalidProject.
                            ProjectErrorUtilities.ThrowInvalidProject(
                                parameterLocation,
                                "CannotPassMultipleItemsIntoScalarParameter",
                                _batchBucket.Expander.ExpandIntoStringAndUnescape(parameterValue, ExpanderOptions.ExpandAll, parameterLocation),
                                parameter.Name,
                                parameterType.FullName,
                                _taskName);
                        }

                        RecordItemForDisconnectIfNecessary(finalTaskItems[0]);

                        if (IsPathLikeTaskItemOrITaskItemOfT(parameterType))
                        {
                            ITaskItem taskItemOfT = CreateTaskItemOfT(parameterType.GetGenericArguments()[0], finalTaskItems[0]);
                            success = InternalSetTaskParameter(parameter, taskItemOfT);
                        }
                        else
                        {
                            success = SetTaskItemParameter(parameter, finalTaskItems[0]);
                        }

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
                    ProjectErrorUtilities.ThrowInvalidProject(
                        parameterLocation,
                        "InvalidTaskParameterValueError",
                        _batchBucket.Expander.ExpandIntoStringAndUnescape(parameterValue, ExpanderOptions.ExpandAll, parameterLocation),
                        parameter.Name,
                        parameterType.FullName,
                        _taskName);
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

            // PERF: Be careful to avoid unnecessary string allocations. Appending '_taskName + "_" + parameter.Name' happens in both paths,
            // but we don't want to allocate the string if we don't need to.
            string key = "DisableLogTaskParameter_" + _taskName + "_" + parameter.Name;

            if (string.Equals(lookup.GetProperty(key)?.EvaluatedValue, "true", StringComparison.OrdinalIgnoreCase))
            {
                parameter.Log = false;
            }
            else
            {
                string metadataKey = "DisableLogTaskParameterItemMetadata_" + _taskName + "_" + parameter.Name;
                if (string.Equals(lookup.GetProperty(metadataKey)?.EvaluatedValue, "true", StringComparison.OrdinalIgnoreCase))
                {
                    parameter.LogItemMetadata = false;
                }
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
        private bool InitializeTaskVectorParameter(
            TaskPropertyInfo parameter,
            Type parameterType,
            string parameterValue,
            ElementLocation parameterLocation,
            bool isRequired,
            out bool taskParameterSet)
        {
            Assumed.NotNull(parameterValue, "Didn't expect null parameterValue in InitializeTaskVectorParameter");

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

        private static readonly string TaskParameterFormatString = ItemGroupLoggingHelper.TaskParameterPrefix + "{0}={1}";

        /// <summary>
        /// Given an instantiated task, this helper method sets the specified parameter
        /// </summary>
        private bool InternalSetTaskParameter(
            TaskPropertyInfo parameter,
            object parameterValue)
        {
            bool success = false;

            if (LogTaskInputs && !_taskLoggingContext.LoggingService.OnlyLogCriticalEvents)
            {
                IList parameterValueAsList = parameterValue as IList;
                bool legacyBehavior = !ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_12);

                // Legacy textual logging for parameters that are not lists.
                if (legacyBehavior && parameterValueAsList == null)
                {
                    _taskLoggingContext.LogCommentFromText(
                       MessageImportance.Low,
                       TaskParameterFormatString,
                       parameter.Name,
                       ItemGroupLoggingHelper.GetStringFromParameterValue(parameterValue));
                }

                if (parameter.Log)
                {
                    // Structured logging for all parameters that have logging enabled and are not empty lists.
                    if (parameterValueAsList?.Count > 0 || (parameterValueAsList == null && !legacyBehavior))
                    {
                        // Note: We're setting TaskParameterEventArgs.ItemType to parameter name for backward compatibility with
                        // older loggers and binlog viewers.
                        ItemGroupLoggingHelper.LogTaskParameter(
                            _taskLoggingContext,
                            TaskParameterMessageKind.TaskInput,
                            parameterName: parameter.Name,
                            propertyName: null,
                            itemType: parameter.Name,
                            parameterValueAsList ?? (object[])[parameterValue],
                            parameter.LogItemMetadata);
                    }
                }
            }

            try
            {
                _taskFactoryWrapper.SetPropertyValue(TaskInstance, parameter, parameterValue);
                success = true;
            }
            catch (TargetInvocationException e)
            {
                // handle any exception thrown by the task's setter itself
                // At this point, the interesting stack is the internal exception.
                // Log the task line number, whatever the value of ContinueOnError;
                // because this will be a hard error anyway.

                // Exception thrown by the called code itself
                // Log the stack, so the task vendor can fix their code
                _taskLoggingContext.LogFatalTaskError(
                    e.InnerException,
                    new BuildEventFileInfo(_taskLocation),
                    _taskName);
            }
            // If a logger has failed, abort immediately. This is the polite LoggerException.
            // InternalLoggerException is an arbitrary logger exception.
            catch (Exception e) when (e is not LoggerException && e is not InternalLoggerException && !ExceptionHandling.NotExpectedReflectionException(e))
            {
                _taskLoggingContext.LogFatalTaskError(
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
                    // Only count non-null elements. We sometimes have a single-element array where the element is null
                    bool hasElements = false;

                    foreach (ITaskItem output in outputs)
                    {
                        // if individual items in the array are null, ignore them
                        if (output != null)
                        {
                            hasElements = true;

                            ProjectItemInstance newItem;

                            TaskItem outputAsProjectItem = output as TaskItem;
                            string parameterLocationEscaped = EscapingUtilities.Escape(parameterLocation.File, cache: true);

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

                                    // If found, directly pass the backing copy-on-write dictionary.
                                    // Otherwise, retrieve a cloned dictionary from the task item.
                                    IMetadataContainer outputAsMetadataContainer = output as IMetadataContainer;
                                    SerializableMetadata backingMetadata = outputAsMetadataContainer?.BackingMetadata ?? default;

                                    if (backingMetadata.HasValue)
                                    {
                                        newItem.SetMetadataOnTaskOutput(backingMetadata.Dictionary);
                                    }
                                    else
                                    {
                                        newItem.SetMetadataOnTaskOutput(outputAsITaskItem2.CloneCustomMetadataEscaped().Cast<KeyValuePair<string, string>>());
                                    }
                                }
                                else
                                {
                                    // Not a ProjectItemInstance.TaskItem or even a ITaskItem2, so we have to fake it.
                                    // Setting an item spec expects the escaped value, as does setting metadata.
                                    newItem = new ProjectItemInstance(_projectInstance, outputTargetName, EscapingUtilities.Escape(output.ItemSpec), parameterLocationEscaped);

                                    newItem.SetMetadataOnTaskOutput(EnumerateMetadata(output.CloneCustomMetadata()));

                                    static IEnumerable<KeyValuePair<string, string>> EnumerateMetadata(IDictionary customMetadata)
                                    {
                                        if (customMetadata is CopyOnWriteDictionary<string> copyOnWriteDictionary)
                                        {
                                            foreach (KeyValuePair<string, string> kvp in copyOnWriteDictionary)
                                            {
                                                yield return new KeyValuePair<string, string>(kvp.Key, EscapingUtilities.Escape(kvp.Value));
                                            }
                                        }
                                        else if (customMetadata is Dictionary<string, string> dictionary)
                                        {
                                            foreach (KeyValuePair<string, string> kvp in dictionary)
                                            {
                                                yield return new KeyValuePair<string, string>(kvp.Key, EscapingUtilities.Escape(kvp.Value));
                                            }
                                        }
                                        else
                                        {
                                            foreach (DictionaryEntry de in customMetadata)
                                            {
                                                yield return new KeyValuePair<string, string>((string)de.Key, EscapingUtilities.Escape((string)de.Value));
                                            }
                                        }
                                    }
                                }
                            }

                            _batchBucket.Lookup.AddNewItem(newItem);
                        }
                    }

                    if (hasElements && LogTaskInputs && !_taskLoggingContext.LoggingService.OnlyLogCriticalEvents && parameter.Log)
                    {
                        ItemGroupLoggingHelper.LogTaskParameter(
                            _taskLoggingContext,
                            TaskParameterMessageKind.TaskOutput,
                            parameterName: parameter.Name,
                            propertyName: null,
                            itemType: outputTargetName,
                            outputs,
                            parameter.LogItemMetadata);
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
                            if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_12))
                            {
                                // Note: We're setting TaskParameterEventArgs.ItemType to property name for backward compatibility with
                                // older loggers and binlog viewers.
                                ItemGroupLoggingHelper.LogTaskParameter(
                                    _taskLoggingContext,
                                    TaskParameterMessageKind.TaskOutput,
                                    parameterName: parameter.Name,
                                    propertyName: outputTargetName,
                                    itemType: outputTargetName,
                                    (object[])[outputString],
                                    parameter.LogItemMetadata);
                            }
                            else
                            {
                                _taskLoggingContext.LogComment(MessageImportance.Low, "OutputPropertyLogMessage", outputTargetName, outputString);
                            }
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
                        ItemGroupLoggingHelper.LogTaskParameter(
                            _taskLoggingContext,
                            TaskParameterMessageKind.TaskOutput,
                            parameterName: parameter.Name,
                            propertyName: null,
                            itemType: outputTargetName,
                            outputs,
                            parameter.LogItemMetadata);
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
                            if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_12))
                            {
                                // Note: We're setting TaskParameterEventArgs.ItemType to property name for backward compatibility with
                                // older loggers and binlog viewers.
                                ItemGroupLoggingHelper.LogTaskParameter(
                                    _taskLoggingContext,
                                    TaskParameterMessageKind.TaskOutput,
                                    parameterName: parameter.Name,
                                    propertyName: outputTargetName,
                                    itemType: outputTargetName,
                                    (object[])[outputString],
                                    parameter.LogItemMetadata);
                            }
                            else
                            {
                                _taskLoggingContext.LogComment(MessageImportance.Low, "OutputPropertyLogMessage", outputTargetName, outputString);
                            }
                        }

                        PropertyTrackingUtils.LogPropertyAssignment(
                            _propertyTrackingSettings,
                            outputTargetName,
                            outputString,
                            parameterLocation,
                            _projectInstance.GetProperty(outputTargetName)?.EvaluatedValue ?? null,
                            _taskLoggingContext);

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
        private IReadOnlyDictionary<string, string> GetNamesOfPropertiesWithRequiredAttribute()
        {
            Assumed.NotNull(_taskFactoryWrapper, "Expected taskFactoryWrapper to not be null");
            IReadOnlyDictionary<string, string> requiredParameters = null;

            try
            {
                requiredParameters = _taskFactoryWrapper.GetNamesOfPropertiesWithRequiredAttribute;
            }
            catch (Exception e) when (!ExceptionHandling.NotExpectedReflectionException(e))
            {
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
            catch (InternalErrorException) when (!_taskLoggingContext.IsValid)
            {
                // We can get an exception from this when we encounter a race between a task finishing and a cancel occurring.  In this situation
                // if the task logging context is no longer valid, we choose to eat the exception because we can't log the message anyway.
            }
        }

        /// <summary>
        /// Creates a <see cref="TaskHostTask"/> wrapper to run a non-AssemblyTaskFactory task out of process.
        /// This is used when Traits.Instance.ForceTaskFactoryOutOfProc=1 is true or the multi-threaded mode is active to ensure
        /// non-AssemblyTaskFactory tasks run in isolation.
        /// </summary>
        /// <param name="taskIdentityParameters">Task identity parameters.</param>
        /// <param name="taskFactoryEngineContext">The engine context to use for the task.</param>
        /// <param name="outOfProcTaskFactory">The out-of-process task factory instance.</param>
        /// <param name="scheduledNodeId">Node for which the task host should be called</param>
        /// <returns>A TaskHostTask that will execute the inner task out of process, or <code>null</code> if task creation fails.</returns>
        [RequiresUnreferencedCode("Instantiates a task by reflecting over a task type discovered at runtime, which is incompatible with trimming.")]
        private ITask CreateTaskHostTaskForOutOfProcFactory(
            in TaskHostParameters taskIdentityParameters,
            TaskFactoryEngineContext taskFactoryEngineContext,
            IOutOfProcTaskFactory outOfProcTaskFactory,
            int scheduledNodeId)
        {
            ITask innerTask;

            // ITaskFactory2 is used for compatibility reasons
            if (_taskFactoryWrapper.TaskFactory is ITaskFactory2 taskFactory2)
            {
                innerTask = taskFactory2.CreateTask(taskFactoryEngineContext, taskIdentityParameters.ToDictionary());
            }
            else
            {
                innerTask = _taskFactoryWrapper.TaskFactory is ITaskFactory3 taskFactory3
                    ? taskFactory3.CreateTask(taskFactoryEngineContext, taskIdentityParameters)
                    : _taskFactoryWrapper.TaskFactory.CreateTask(taskFactoryEngineContext);
            }

            if (innerTask == null)
            {
                return null;
            }

            // Create a LoadedType for the actual task type so we can wrap it in TaskHostTask
            Type taskType = innerTask.GetType();

            // For out-of-process inline tasks, get the assembly path from the factory
            // (Assembly.Location is typically empty for inline tasks loaded from bytes)
            string resolvedAssemblyLocation = outOfProcTaskFactory.GetAssemblyPath();

            // This should never happen - if the factory can create a task, it should know where the assembly is
            Assumed.NotNullOrEmpty(resolvedAssemblyLocation, $"IOutOfProcTaskFactory {_taskFactoryWrapper.TaskFactory.FactoryName} created a task but returned null/empty assembly path");

            LoadedType taskLoadedType = new LoadedType(
                taskType,
                AssemblyLoadInfo.Create(null, resolvedAssemblyLocation),
                taskType.Assembly,
                typeof(ITaskItem));

            // Default task host parameters for out-of-process execution for inline tasks
            TaskHostParameters taskHostParameters = new(XMakeAttributes.GetCurrentMSBuildRuntime(), XMakeAttributes.GetCurrentMSBuildArchitecture());

            // Merge with any existing task identity parameters
            if (!taskIdentityParameters.IsEmpty)
            {
                taskHostParameters = TaskHostParameters.MergeTaskHostParameters(taskHostParameters, taskIdentityParameters);
            }

            // Clean up the original task since we're going to wrap it
            _taskFactoryWrapper.TaskFactory.CleanupTask(innerTask);

            return new TaskHostTask(
                _taskLocation,
                _taskLoggingContext,
                _buildComponentHost,
                taskHostParameters,
                taskLoadedType,
                useSidecarTaskHost: true,
                _projectFile,
#if FEATURE_APPDOMAIN
                AppDomainSetup,
#endif
                _hostServices,
                scheduledNodeId,
                TaskEnvironment);
        }
    }
}
