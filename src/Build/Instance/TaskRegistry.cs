// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.NET.StringTools;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using ProjectXmlUtilities = Microsoft.Build.Internal.ProjectXmlUtilities;
using TargetLoggingContext = Microsoft.Build.BackEnd.Logging.TargetLoggingContext;
using TaskEngineAssemblyResolver = Microsoft.Build.BackEnd.Logging.TaskEngineAssemblyResolver;

#nullable disable

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// This class is used to track tasks used by a project. Tasks are declared in project files with the &lt;UsingTask&gt; tag.
    /// Task and assembly names must be specified per .NET guidelines, however, the names do not need to be fully qualified if
    /// they provide enough information to locate the tasks they refer to. Assemblies can also be referred to using file paths --
    /// this is useful when it is not possible/desirable to place task assemblies in the GAC, or in the same directory as MSBuild.
    /// </summary>
    /// <remarks>
    /// 1) specifying a task assembly using BOTH its assembly name (strong or weak) AND its file path is not allowed
    /// 2) when specifying the assembly name, the file extension (usually ".dll") must NOT be specified
    /// 3) when specifying the assembly file, the file extension MUST be specified
    /// </remarks>
    /// <example>
    /// &lt;UsingTask TaskName="Microsoft.Build.Tasks.Csc"                     ==> look for the "Csc" task in the
    ///            AssemblyName="Microsoft.Build.Tasks"/&gt;                       weakly-named "Microsoft.Build.Tasks" assembly
    ///
    /// &lt;UsingTask TaskName="t1"                                            ==> look for the "t1" task in the
    ///            AssemblyName="mytasks, Culture=en, Version=1.0.0.0"/&gt;        strongly-named "mytasks" assembly
    ///
    /// &lt;UsingTask TaskName="foo"                                           ==> look for the "foo" task in the
    ///            AssemblyFile="$(MyDownloadedTasks)\utiltasks.dll"/&gt;          "utiltasks" assembly file
    ///
    /// &lt;UsingTask TaskName="UtilTasks.Bar"                                 ==> invalid task declaration
    ///            AssemblyName="utiltasks.dll"
    ///            AssemblyFile="$(MyDownloadedTasks)\"/&gt;
    /// </example>
    internal sealed class TaskRegistry : ITranslatable
    {
        /// <summary>
        /// The fallback task registry
        /// </summary>
        private Toolset _toolset;

        /// <summary>
        /// Simple name for the MSBuild tasks (v4), used for shimming in loading
        /// task factory UsingTasks
        /// </summary>
        private const string s_tasksV4SimpleName = "Microsoft.Build.Tasks.v4.0";

        /// <summary>
        /// Filename for the MSBuild tasks (v4), used for shimming in loading
        /// task factory UsingTasks
        /// </summary>
        private const string s_tasksV4Filename = $"{s_tasksV4SimpleName}.dll";

        /// <summary>
        /// Expected location that MSBuild tasks (v4) is picked up from if the user
        /// references it with just a simple name, used for shimming in loading
        /// task factory UsingTasks
        /// </summary>
        private static readonly string s_potentialTasksV4Location = Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, s_tasksV4Filename);

        /// <summary>
        /// Simple name for the MSBuild tasks (v12), used for shimming in loading
        /// task factory UsingTasks
        /// </summary>
        private const string s_tasksV12SimpleName = "Microsoft.Build.Tasks.v12.0";

        /// <summary>
        /// Filename for the MSBuild tasks (v12), used for shimming in loading
        /// task factory UsingTasks
        /// </summary>
        private const string s_tasksV12Filename = $"{s_tasksV12SimpleName}.dll";

        /// <summary>
        /// Expected location that MSBuild tasks (v12) is picked up from if the user
        /// references it with just a simple name, used for shimming in loading
        /// task factory UsingTasks
        /// </summary>
        private static readonly string s_potentialTasksV12Location = Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, s_tasksV12Filename);

        /// <summary>
        /// Simple name for the MSBuild tasks (v14+), used for shimming in loading
        /// task factory UsingTasks
        /// </summary>
        private const string s_tasksCoreSimpleName = "Microsoft.Build.Tasks.Core";

        /// <summary>
        /// Filename for the MSBuild tasks (v14+), used for shimming in loading
        /// task factory UsingTasks
        /// </summary>
        private const string s_tasksCoreFilename = $"{s_tasksCoreSimpleName}.dll";

        /// <summary>
        /// Expected location that MSBuild tasks (v14+) is picked up from if the user
        /// references it with just a simple name, used for shimming in loading
        /// task factory UsingTasks
        /// </summary>
        private static readonly string s_potentialTasksCoreLocation = Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, s_tasksCoreFilename);

        /// <summary>
        /// Monotonically increasing counter for registered tasks.
        /// </summary>
        private int _nextRegistrationOrderId = 0;

        /// <summary>
        /// Cache of tasks already found using exact matching,
        /// keyed by the task identity requested.
        /// </summary>
        private readonly ConcurrentDictionary<RegisteredTaskIdentity, RegisteredTaskRecord> _cachedTaskRecordsWithExactMatch =
            new(RegisteredTaskIdentity.RegisteredTaskIdentityComparer.Exact);

        /// <summary>
        /// Cache of tasks already found using fuzzy matching,
        /// keyed by the task name requested.
        /// Value is a dictionary of all possible matches for that
        /// task name, by unique identity.
        /// </summary>
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<RegisteredTaskIdentity, RegisteredTaskRecord>> _cachedTaskRecordsWithFuzzyMatch = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Cache of task declarations i.e. the &lt;UsingTask&gt; tags fed to this registry,
        /// keyed by the task name declared.
        /// Task name may be qualified or not.
        /// This field may be null.
        /// This is expected to be modified only during initialization via a single call, and all reads will occur only after the initialization is done - so no need for a concurrent dictionary.
        /// </summary>
        private Dictionary<RegisteredTaskIdentity, List<RegisteredTaskRecord>> _taskRegistrations;

        /// <summary>
        /// Create another set containing architecture-specific task entries.
        ///  Then when we look for them, check if the name exists in that.
        /// This is expected to be modified only during initialization via a single call, and all reads will occur only after the initialization is done - so no need for a concurrent dictionary.
        /// </summary>
        private readonly Dictionary<string, List<RegisteredTaskRecord>> _overriddenTasks = new Dictionary<string, List<RegisteredTaskRecord>>();

#if DEBUG
        /// <summary>
        /// Indicates whether the task registry has been initialized.
        /// Task registry cannot be used until it is initialized. And it cannot be initialized more than once.
        /// This will help to guarantee logical immutability of TaskRegistry.
        /// </summary>
        private bool _isInitialized;
#endif

        /// <summary>
        /// The cache to load the *.tasks files into
        /// </summary>
        internal ProjectRootElementCacheBase RootElementCache { get; set; }

        /// <summary>
        /// Creates a task registry that does not fall back to any other task registry.
        /// Default constructor does no work because the tables are initialized lazily when a task is registered
        /// </summary>
        internal TaskRegistry(ProjectRootElementCacheBase projectRootElementCache)
        {
            ErrorUtilities.VerifyThrowInternalNull(projectRootElementCache);

            RootElementCache = projectRootElementCache;
        }

        private TaskRegistry()
        {
        }

        /// <summary>
        /// Creates a task registry that defers to the specified toolset's registry for those tasks it cannot resolve.
        /// UNDONE: (Logging.) We can't pass the base task registry from the Toolset because we can't call GetTaskRegistry
        /// without logging context information.  When the Project load code is altered to contain logging service
        /// references, we can load the toolset task registry at the time this registry is created and pass it to
        /// this constructor instead of the toolset state.
        /// </summary>
        /// <param name="toolset">The Toolset containing the toolser task registry</param>
        /// <param name="projectRootElementCache">The <see cref="ProjectRootElementCache"/> to use.</param>
        internal TaskRegistry(Toolset toolset, ProjectRootElementCacheBase projectRootElementCache)
        {
            ErrorUtilities.VerifyThrowInternalNull(projectRootElementCache);
            ErrorUtilities.VerifyThrowInternalNull(toolset);

            RootElementCache = projectRootElementCache;
            _toolset = toolset;
        }

        /// <summary>
        /// Returns the toolset state used to initialize this registry, if any.
        /// </summary>
        internal Toolset Toolset
        {
            [DebuggerStepThrough]
            get
            { return _toolset; }
        }

        /// <summary>
        /// Access the next registration sequence id.
        /// FOR UNIT TESTING ONLY.
        /// </summary>
        internal int NextRegistrationOrderId => _nextRegistrationOrderId;

        /// <summary>
        /// Access list of task registrations.
        /// FOR UNIT TESTING ONLY.
        /// </summary>
        internal IDictionary<RegisteredTaskIdentity, List<RegisteredTaskRecord>> TaskRegistrations
        {
            get
            {
                if (_taskRegistrations == null)
                {
                    _taskRegistrations = CreateRegisteredTaskDictionary();
                }

                return _taskRegistrations;
            }
        }

        internal bool IsLoaded => RootElementCache != null;

        /// <summary>
        /// Evaluate the usingtask and add the result into the data passed in
        /// </summary>
        /// <typeparam name="P">A type derived from IProperty</typeparam>
        /// <typeparam name="I">A type derived from IItem</typeparam>
        internal static void InitializeTaskRegistryFromUsingTaskElements<P, I>(
            LoggingContext loggingContext,
            IEnumerable<(ProjectUsingTaskElement projectUsingTaskXml, string directoryOfImportingFile)> registrations,
            TaskRegistry taskRegistry,
            Expander<P, I> expander,
            ExpanderOptions expanderOptions,
            IFileSystem fileSystem)
            where P : class, IProperty
            where I : class, IItem
        {
            foreach ((ProjectUsingTaskElement projectUsingTaskXml, string directoryOfImportingFile) registration in registrations)
            {
                RegisterTasksFromUsingTaskElement(
                    loggingContext,
                    registration.directoryOfImportingFile,
                    registration.projectUsingTaskXml,
                    taskRegistry,
                    expander,
                    expanderOptions,
                    fileSystem);
            }
#if DEBUG
            taskRegistry._isInitialized = true;
            taskRegistry._taskRegistrations ??= TaskRegistry.CreateRegisteredTaskDictionary();
#endif
        }

        /// <summary>
        /// Evaluate the usingtask and add the result into the data passed in
        /// </summary>
        /// <typeparam name="P">A type derived from IProperty</typeparam>
        /// <typeparam name="I">A type derived from IItem</typeparam>
        private static void RegisterTasksFromUsingTaskElement
            <P, I>(
            LoggingContext loggingContext,
            string directoryOfImportingFile,
            ProjectUsingTaskElement projectUsingTaskXml,
            TaskRegistry taskRegistry,
            Expander<P, I> expander,
            ExpanderOptions expanderOptions,
            IFileSystem fileSystem)
            where P : class, IProperty
            where I : class, IItem
        {
            ErrorUtilities.VerifyThrowInternalNull(directoryOfImportingFile);
#if DEBUG
            ErrorUtilities.VerifyThrowInternalError(!taskRegistry._isInitialized, "Attempt to modify TaskRegistry after it was initialized.");
#endif

            if (!ConditionEvaluator.EvaluateCondition(
                    projectUsingTaskXml.Condition,
                    ParserOptions.AllowPropertiesAndItemLists,
                    expander,
                    expanderOptions,
                    projectUsingTaskXml.ContainingProject.DirectoryPath,
                    projectUsingTaskXml.ConditionLocation,
                    fileSystem,
                    loggingContext))
            {
                return;
            }

            string assemblyFile = null;
            string assemblyName = null;

            string taskName = expander.ExpandIntoStringLeaveEscaped(projectUsingTaskXml.TaskName, expanderOptions, projectUsingTaskXml.TaskNameLocation);

            ProjectErrorUtilities.VerifyThrowInvalidProject(
                taskName.Length > 0,
                projectUsingTaskXml.TaskNameLocation,
                "InvalidEvaluatedAttributeValue",
                taskName,
                projectUsingTaskXml.TaskName,
                XMakeAttributes.name,
                XMakeElements.usingTask);

            string taskFactory = expander.ExpandIntoStringLeaveEscaped(projectUsingTaskXml.TaskFactory, expanderOptions, projectUsingTaskXml.TaskFactoryLocation);

            if (String.IsNullOrEmpty(taskFactory) || taskFactory.Equals(RegisteredTaskRecord.AssemblyTaskFactory, StringComparison.OrdinalIgnoreCase) || taskFactory.Equals(RegisteredTaskRecord.TaskHostFactory, StringComparison.OrdinalIgnoreCase))
            {
                ProjectXmlUtilities.VerifyThrowProjectNoChildElements(projectUsingTaskXml.XmlElement);
            }

            if (projectUsingTaskXml.AssemblyFile.Length > 0)
            {
                assemblyFile = expander.ExpandIntoStringLeaveEscaped(projectUsingTaskXml.AssemblyFile, expanderOptions, projectUsingTaskXml.AssemblyFileLocation);
            }
            else
            {
                assemblyName = expander.ExpandIntoStringLeaveEscaped(projectUsingTaskXml.AssemblyName, expanderOptions, projectUsingTaskXml.AssemblyNameLocation);
            }

            ProjectErrorUtilities.VerifyThrowInvalidProject(
                assemblyFile == null || assemblyFile.Length > 0,
                projectUsingTaskXml.AssemblyFileLocation,
                "InvalidEvaluatedAttributeValue",
                assemblyFile,
                projectUsingTaskXml.AssemblyFile,
                XMakeAttributes.assemblyFile,
                XMakeElements.usingTask);

            ProjectErrorUtilities.VerifyThrowInvalidProject(
                assemblyName == null || assemblyName.Length > 0,
                projectUsingTaskXml.AssemblyNameLocation,
                "InvalidEvaluatedAttributeValue",
                assemblyName,
                projectUsingTaskXml.AssemblyName,
                XMakeAttributes.assemblyName,
                XMakeElements.usingTask);

            // Ensure the assembly file/path is relative to the project in which this <UsingTask> node was defined -- we
            // don't want paths from imported projects being interpreted relative to the main project file.
            try
            {
                assemblyFile = FileUtilities.FixFilePath(assemblyFile);

                if (assemblyFile != null && !Path.IsPathRooted(assemblyFile))
                {
                    assemblyFile = Strings.WeakIntern(Path.Combine(directoryOfImportingFile, assemblyFile));
                }

                if (String.Equals(taskFactory, RegisteredTaskRecord.CodeTaskFactory, StringComparison.OrdinalIgnoreCase) || String.Equals(taskFactory, RegisteredTaskRecord.XamlTaskFactory, StringComparison.OrdinalIgnoreCase))
                {
                    // SHIM: One common pattern for people using CodeTaskFactory or XamlTaskFactory from M.B.T.v4.0.dll is to
                    // specify it using $(MSBuildToolsPath) -- which now no longer contains M.B.T.v4.0.dll.  This same pattern
                    // may also occur if someone is using CodeTaskFactory or XamlTaskFactory from M.B.T.v12.0.dll.  So if we have a
                    // situation where the path being used doesn't contain the v4 or v12 tasks but DOES contain the v14+ tasks, just
                    // secretly substitute it here.
                    if (
                            assemblyFile != null &&
                            (assemblyFile.EndsWith(s_tasksV4Filename, StringComparison.OrdinalIgnoreCase) || assemblyFile.EndsWith(s_tasksV12Filename, StringComparison.OrdinalIgnoreCase)) &&
                            !FileUtilities.FileExistsNoThrow(assemblyFile, fileSystem))
                    {
                        string replacedAssemblyFile = Path.Combine(Path.GetDirectoryName(assemblyFile), s_tasksCoreFilename);

                        if (FileUtilities.FileExistsNoThrow(replacedAssemblyFile, fileSystem))
                        {
                            assemblyFile = replacedAssemblyFile;
                        }
                    }
                    else if (assemblyName != null)
                    {
                        // SHIM: Another common pattern for people using CodeTaskFactory or XamlTaskFactory from
                        // M.B.T.v4.0.dll is to specify it using AssemblyName with a simple name -- which works only if that
                        // that assembly is in the current directory.  Much like with the above case, if we detect that
                        // situation, secretly substitute it here so that the majority of task factory users aren't broken.
                        if
                            (
                                assemblyName.Equals(s_tasksV4SimpleName, StringComparison.OrdinalIgnoreCase) &&
                                !FileUtilities.FileExistsNoThrow(s_potentialTasksV4Location, fileSystem) &&
                                FileUtilities.FileExistsNoThrow(s_potentialTasksCoreLocation, fileSystem))
                        {
                            assemblyName = s_tasksCoreSimpleName;
                        }
                        else if
                            (
                                assemblyName.Equals(s_tasksV12SimpleName, StringComparison.OrdinalIgnoreCase) &&
                                !FileUtilities.FileExistsNoThrow(s_potentialTasksV12Location, fileSystem) &&
                                FileUtilities.FileExistsNoThrow(s_potentialTasksCoreLocation, fileSystem))
                        {
                            assemblyName = s_tasksCoreSimpleName;
                        }
                    }
                }
            }
            catch (ArgumentException ex)
            {
                // Invalid chars in AssemblyFile path
                ProjectErrorUtilities.ThrowInvalidProject(projectUsingTaskXml.Location, "InvalidAttributeValueWithException", assemblyFile, XMakeAttributes.assemblyFile, XMakeElements.usingTask, ex.Message);
            }

            RegisteredTaskRecord.ParameterGroupAndTaskElementRecord parameterGroupAndTaskElementRecord = null;

            if (projectUsingTaskXml.Count > 0)
            {
                parameterGroupAndTaskElementRecord = new RegisteredTaskRecord.ParameterGroupAndTaskElementRecord();
                parameterGroupAndTaskElementRecord.ExpandUsingTask<P, I>(projectUsingTaskXml, expander, expanderOptions);
            }

            TaskHostParameters taskFactoryParameters = TaskHostParameters.Empty;
            string runtime = expander.ExpandIntoStringLeaveEscaped(projectUsingTaskXml.Runtime, expanderOptions, projectUsingTaskXml.RuntimeLocation);
            string architecture = expander.ExpandIntoStringLeaveEscaped(projectUsingTaskXml.Architecture, expanderOptions, projectUsingTaskXml.ArchitectureLocation);
            string overrideUsingTask = expander.ExpandIntoStringLeaveEscaped(projectUsingTaskXml.Override, expanderOptions, projectUsingTaskXml.OverrideLocation);

            if ((runtime != string.Empty) || (architecture != string.Empty))
            {
                taskFactoryParameters = new TaskHostParameters(
                    runtime == string.Empty ? XMakeAttributes.MSBuildRuntimeValues.any : runtime,
                    architecture == string.Empty ? XMakeAttributes.MSBuildArchitectureValues.any : architecture);
            }

            taskRegistry.RegisterTask(
                taskName,
                AssemblyLoadInfo.Create(assemblyName, assemblyFile),
                taskFactory,
                taskFactoryParameters,
                parameterGroupAndTaskElementRecord,
                loggingContext,
                projectUsingTaskXml,
                ConversionUtilities.ValidBooleanTrue(overrideUsingTask));
        }

        /// <summary>
        /// Given a task name, this method retrieves the task class. If the task has been requested before, it will be found in
        /// the class cache; otherwise, &lt;UsingTask&gt; declarations will be used to search the appropriate assemblies.
        /// </summary>
        internal TaskFactoryWrapper GetRegisteredTask(
            string taskName,
            string taskProjectFile,
            in TaskHostParameters taskIdentityParameters,
            bool exactMatchRequired,
            TargetLoggingContext targetLoggingContext,
            ElementLocation elementLocation,
            bool isMultiThreadedBuild)
        {
#if DEBUG
            ErrorUtilities.VerifyThrowInternalError(_isInitialized, "Attempt to read from TaskRegistry before its initialization was finished.");
#endif
            TaskFactoryWrapper taskFactory = null;

            // If there are no usingtask tags in the project don't bother caching or looking for tasks locally
            RegisteredTaskRecord record = GetTaskRegistrationRecord(taskName, taskProjectFile, taskIdentityParameters, exactMatchRequired, targetLoggingContext, elementLocation, out bool retrievedFromCache, isMultiThreadedBuild);

            if (record != null)
            {
                // if the given task name is longer than the registered task name
                // we will use the longer name to help disambiguate between multiple matches
                string mostSpecificTaskName = (taskName.Length > record.RegisteredName.Length) ? taskName : record.RegisteredName;
                taskFactory = record.GetTaskFactoryFromRegistrationRecord(mostSpecificTaskName, taskProjectFile, taskIdentityParameters, targetLoggingContext, elementLocation, isMultiThreadedBuild);

                if (taskFactory != null && !retrievedFromCache)
                {
                    if (record.TaskFactoryAttributeName.Equals(RegisteredTaskRecord.AssemblyTaskFactory) || record.TaskFactoryAttributeName.Equals(RegisteredTaskRecord.TaskHostFactory))
                    {
                        targetLoggingContext.LogComment(MessageImportance.Low, "TaskFound", taskName, taskFactory.Name);
                    }
                    else
                    {
                        targetLoggingContext.LogComment(MessageImportance.Low, "TaskFoundFromFactory", taskName, taskFactory.Name);
                    }

                    if (taskFactory.TaskFactoryLoadedType.HasSTAThreadAttribute)
                    {
                        targetLoggingContext.LogComment(MessageImportance.Low, "TaskNeedsSTA", taskName);
                    }
                }
            }

            return taskFactory;
        }

        /// <summary>
        /// Retrieves the task registration record for the specified task.
        /// </summary>
        /// <param name="taskName">The name of the task to retrieve.</param>
        /// <param name="taskProjectFile">The task's project file.</param>
        /// <param name="taskIdentityParameters">The set of task identity parameters to be used to identify the
        /// correct task record match.</param>
        /// <param name="exactMatchRequired">True if an exact name match is required.</param>
        /// <param name="targetLoggingContext">The logging context.</param>
        /// <param name="elementLocation">The location of the task element in the project file.</param>
        /// <param name="retrievedFromCache">True if the record was retrieved from the cache.</param>
        /// <param name="isMultiThreadedBuild">Whether the build is running in multi-threaded mode.</param>
        /// <returns>The task registration record, or null if none was found.</returns>
        internal RegisteredTaskRecord GetTaskRegistrationRecord(
            string taskName,
            string taskProjectFile,
            in TaskHostParameters taskIdentityParameters,
            bool exactMatchRequired,
            TargetLoggingContext targetLoggingContext,
            ElementLocation elementLocation,
            out bool retrievedFromCache,
            bool isMultiThreadedBuild)
        {
            RegisteredTaskRecord taskRecord = null;
            retrievedFromCache = false;
            RegisteredTaskIdentity taskIdentity = new(taskName, taskIdentityParameters);

            // Project-level override tasks are keyed by task name (unqualified).
            // Because Foo.Bar and Baz.Bar are both valid, they are stored
            // in a dictionary keyed as `Bar` because most tasks are called unqualified
            if (_overriddenTasks.TryGetValue(taskName, out List<RegisteredTaskRecord> recs))
            {
                // When we determine this task was overridden, search all task records
                // to find the most correct registration. Search with the fully qualified name (if applicable)
                // Behavior is intended to be "first one wins"
                foreach (RegisteredTaskRecord rec in recs)
                {
                    if (RegisteredTaskIdentity.RegisteredTaskIdentityComparer.IsPartialMatch(taskIdentity, rec.TaskIdentity))
                    {
                        return rec;
                    }
                }
            }

            // Try the override task registry first
            if (_toolset != null)
            {
                TaskRegistry toolsetRegistry = _toolset.GetOverrideTaskRegistry(targetLoggingContext, RootElementCache);
                taskRecord = toolsetRegistry.GetTaskRegistrationRecord(taskName, taskProjectFile, taskIdentityParameters, exactMatchRequired, targetLoggingContext, elementLocation, out retrievedFromCache, isMultiThreadedBuild);
            }

            // Try the current task registry
            if (taskRecord == null && _taskRegistrations?.Count > 0)
            {
                if (exactMatchRequired)
                {
                    if (_cachedTaskRecordsWithExactMatch.TryGetValue(taskIdentity, out taskRecord))
                    {
                        retrievedFromCache = true;
                        return taskRecord;
                    }
                }
                else
                {
                    if (_cachedTaskRecordsWithFuzzyMatch.TryGetValue(taskIdentity.Name, out ConcurrentDictionary<RegisteredTaskIdentity, RegisteredTaskRecord> taskRecords))
                    {
                        // if we've looked up this exact one before, just grab it and return
                        if (taskRecords.TryGetValue(taskIdentity, out taskRecord))
                        {
                            retrievedFromCache = true;
                            return taskRecord;
                        }
                        else
                        {
                            // otherwise, check the "short list" of everything else included here to see if one of them matches
                            foreach (RegisteredTaskRecord record in taskRecords.Values)
                            {
                                // Just return the first one that actually matches.  There may be nulls in here as well, if we've previously attempted to
                                // find a variation on this task record and failed.  In that case, since it wasn't an exact match (otherwise it would have
                                // been picked up by the check above) just ignore it, the way we ignore task records that don't work with this set of
                                // parameters.
                                if (record != null)
                                {
                                    if (record.CanTaskBeCreatedByFactory(taskName, taskProjectFile, taskIdentityParameters, targetLoggingContext, elementLocation, isMultiThreadedBuild))
                                    {
                                        retrievedFromCache = true;
                                        return record;
                                    }
                                }
                            }
                        }

                        // otherwise, nothing fit, so act like we never hit the cache at all.
                    }
                }

                IEnumerable<RegisteredTaskRecord> registrations = GetRelevantOrderedRegistrations(taskIdentity, exactMatchRequired);

                // look for the given task name in the registry; if not found, gather all registered task names that partially
                // match the given name
                taskRecord = GetMatchingRegistration(taskName, registrations, taskProjectFile, taskIdentityParameters, targetLoggingContext, elementLocation, isMultiThreadedBuild);
            }

            // If we didn't find the task but we have a fallback registry in the toolset state, try that one.
            if (taskRecord == null && _toolset != null)
            {
                TaskRegistry toolsetRegistry = _toolset.GetTaskRegistry(targetLoggingContext, RootElementCache);
                taskRecord = toolsetRegistry.GetTaskRegistrationRecord(taskName, taskProjectFile, taskIdentityParameters, exactMatchRequired, targetLoggingContext, elementLocation, out retrievedFromCache, isMultiThreadedBuild);
            }

            // Cache the result, even if it is null.  We should never again do the work we just did, for this task name.
            if (exactMatchRequired)
            {
                _cachedTaskRecordsWithExactMatch[taskIdentity] = taskRecord;
            }
            else
            {
                // Since this is a fuzzy match, we could conceivably have several sets of task identity parameters that match
                // each other ... but might be mutually exclusive themselves.  E.g. CLR4|x86 and CLR2|x64 both match *|*.
                //
                // To prevent us inadvertently leaking something incompatible, in this case, we need to store not just the
                // record that we got this time, but ALL of the records that have previously matched this key.
                //
                // Furthermore, the first level key needs to be the name of the task, not its identity -- otherwise we might
                // end up with multiple entries containing subsets of the same fuzzy-matchable tasks.  E.g. with the following
                // set of steps:
                // 1. Look up Foo | bar
                // 2. Look up Foo | * (goes into Foo | bar cache entry)
                // 3. Look up Foo | baz (gets its own entry because it doesn't match Foo | bar)
                // 4. Look up Foo | * (should get the Foo | * under Foo | bar, but depending on what the dictionary looks up
                //    first, might get Foo | baz, which also matches, instead)
                ConcurrentDictionary<RegisteredTaskIdentity, RegisteredTaskRecord> taskRecords
                    = _cachedTaskRecordsWithFuzzyMatch.GetOrAdd(taskIdentity.Name,
                        _ => new(RegisteredTaskIdentity.RegisteredTaskIdentityComparer.Exact));

                taskRecords[taskIdentity] = taskRecord;
                _cachedTaskRecordsWithFuzzyMatch[taskIdentity.Name] = taskRecords;
            }

            return taskRecord;
        }

        /// <summary>
        /// Is the class being loaded a task factory class
        /// </summary>
        private static bool IsTaskFactoryClass(Type type, object unused)
        {
            return type.GetTypeInfo().IsClass &&
                !type.GetTypeInfo().IsAbstract &&
                typeof(Microsoft.Build.Framework.ITaskFactory).IsAssignableFrom(type);
        }

        /// <summary>
        /// Searches all task declarations for the given task name.
        /// If no exact match is found, looks for partial matches.
        /// A task name that is not fully qualified may produce several partial matches.
        /// </summary>
        private IEnumerable<RegisteredTaskRecord> GetRelevantOrderedRegistrations(RegisteredTaskIdentity taskIdentity, bool exactMatchRequired)
        {
            if (_taskRegistrations.TryGetValue(taskIdentity, out List<RegisteredTaskRecord> taskAssemblies))
            {
                // (records for single key should be ordered by order of registrations - as they are inserted into the list)
                return taskAssemblies;
            }

            if (exactMatchRequired)
            {
                return [];
            }

            // look through all task declarations for partial matches
            return _taskRegistrations
                .Where(tp => RegisteredTaskIdentity.RegisteredTaskIdentityComparer.IsPartialMatch(taskIdentity, tp.Key))
                .SelectMany(tp => tp.Value)
                .OrderBy(r => r.RegistrationOrderId);
        }

        /// <summary>
        /// Registers an evaluated using task tag for future
        /// consultation
        /// </summary>
        private void RegisterTask(
            string taskName,
            AssemblyLoadInfo assemblyLoadInfo,
            string taskFactory,
            in TaskHostParameters taskFactoryParameters,
            RegisteredTaskRecord.ParameterGroupAndTaskElementRecord inlineTaskRecord,
            LoggingContext loggingContext,
            ProjectUsingTaskElement projectUsingTaskInXml,
            bool overrideTask)
        {
            ErrorUtilities.VerifyThrowInternalLength(taskName, nameof(taskName));
            ErrorUtilities.VerifyThrowInternalNull(assemblyLoadInfo);

            // Lazily allocate the hashtable
            if (_taskRegistrations == null)
            {
                _taskRegistrations = CreateRegisteredTaskDictionary();
            }

            // since more than one task can have the same name, we want to keep track of all assemblies that are declared to
            // contain tasks with a given name...
            List<RegisteredTaskRecord> registeredTaskEntries;
            RegisteredTaskIdentity taskIdentity = new RegisteredTaskIdentity(taskName, taskFactoryParameters);
            if (!_taskRegistrations.TryGetValue(taskIdentity, out registeredTaskEntries))
            {
                registeredTaskEntries = new List<RegisteredTaskRecord>();
                _taskRegistrations[taskIdentity] = registeredTaskEntries;
            }

            RegisteredTaskRecord newRecord = new RegisteredTaskRecord(
                taskName,
                assemblyLoadInfo,
                taskFactory,
                taskFactoryParameters,
                inlineTaskRecord,
                Interlocked.Increment(ref _nextRegistrationOrderId),
                projectUsingTaskInXml.ContainingProject.FullPath);

            if (overrideTask)
            {
                // Key the dictionary based on Unqualified task names
                // This is to support partial matches on tasks like Foo.Bar and Baz.Bar
                string[] nameComponents = taskName.Split('.');
                string unqualifiedTaskName = nameComponents[nameComponents.Length - 1];

                // Is the task already registered?
                if (_overriddenTasks.TryGetValue(unqualifiedTaskName, out List<RegisteredTaskRecord> recs))
                {
                    foreach (RegisteredTaskRecord rec in recs)
                    {
                        if (rec.RegisteredName.Equals(taskIdentity.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            loggingContext.LogError(new BuildEventFileInfo(projectUsingTaskInXml.OverrideLocation), "DuplicateOverrideUsingTaskElement", taskName);
                            break;
                        }
                    }
                    recs.Add(newRecord);
                }
                else
                {
                    // New record's name may be fully qualified. Use it anyway to account for partial matches.
                    List<RegisteredTaskRecord> unqualifiedTaskNameMatches = new();
                    unqualifiedTaskNameMatches.Add(newRecord);
                    _overriddenTasks.Add(unqualifiedTaskName, unqualifiedTaskNameMatches);
                    loggingContext.LogComment(MessageImportance.Low, "OverrideUsingTaskElementCreated", taskName, projectUsingTaskInXml.OverrideLocation);
                }
            }

            registeredTaskEntries.Add(newRecord);
        }

        private static Dictionary<RegisteredTaskIdentity, List<RegisteredTaskRecord>> CreateRegisteredTaskDictionary(int? capacity = null)
        {
            return capacity != null
                ? new Dictionary<RegisteredTaskIdentity, List<RegisteredTaskRecord>>(capacity.Value, RegisteredTaskIdentity.RegisteredTaskIdentityComparer.Exact)
                : new Dictionary<RegisteredTaskIdentity, List<RegisteredTaskRecord>>(RegisteredTaskIdentity.RegisteredTaskIdentityComparer.Exact);
        }

        /// <summary>
        /// Given a task name and a list of records which may contain the task, this helper method will ask the records to see if the task name
        /// can be created by the factories which are wrapped by the records. (this is done by instantiating the task factory and asking it).
        /// </summary>
        private RegisteredTaskRecord GetMatchingRegistration(
            string taskName,
            IEnumerable<RegisteredTaskRecord> taskRecords,
            string taskProjectFile,
            TaskHostParameters taskIdentityParameters,
            TargetLoggingContext targetLoggingContext,
            ElementLocation elementLocation,
            bool isMultiThreadedBuild)
            =>
                taskRecords.FirstOrDefault(r =>
                    r.CanTaskBeCreatedByFactory(
                        // if the given task name is longer than the registered task name
                        // we will use the longer name to help disambiguate between multiple matches
                        (taskName.Length > r.TaskIdentity.Name.Length) ? taskName : r.TaskIdentity.Name,
                        taskProjectFile,
                        taskIdentityParameters,
                        targetLoggingContext,
                        elementLocation,
                        isMultiThreadedBuild));

        /// <summary>
        /// An object representing the identity of a task -- not just task name, but also
        /// the set of identity parameters
        /// </summary>
        [DebuggerDisplay("{Name} ParameterCount = {TaskIdentityParameters.Count}")]
        internal class RegisteredTaskIdentity : ITranslatable
        {
            private string _name;
            private TaskHostParameters _taskIdentityParameters;

            /// <summary>
            /// Constructor.
            /// </summary>
            internal RegisteredTaskIdentity(string name, in TaskHostParameters taskIdentityParameters)
            {
                _name = name;
                _taskIdentityParameters = taskIdentityParameters.IsEmpty ? TaskHostParameters.Empty : taskIdentityParameters;
            }

            public RegisteredTaskIdentity()
            {
            }

            /// <summary>
            /// The name of the task
            /// </summary>
            public string Name
            {
                get { return _name; }
            }

            /// <summary>
            /// The identity parameters.
            /// </summary>
            public TaskHostParameters TaskIdentityParameters => _taskIdentityParameters;

            /// <summary>
            /// Comparer used to figure out whether two RegisteredTaskIdentities are equal or not.
            /// </summary>
            internal class RegisteredTaskIdentityComparer : IEqualityComparer<RegisteredTaskIdentity>
            {
                /// <summary>
                /// The singleton comparer to use when an exact match is desired
                /// </summary>
                private static readonly RegisteredTaskIdentityComparer s_exact = new RegisteredTaskIdentityComparer(true /* exact match */);

                /// <summary>
                /// The singleton comparer to use when a fuzzy match is desired.  Note that this still does an exact match on the
                /// name, but does a fuzzy match on the task identity parameters.
                /// </summary>
                private static readonly RegisteredTaskIdentityComparer s_fuzzy = new RegisteredTaskIdentityComparer(false /* fuzzy match */);

                /// <summary>
                /// Keeps track of whether we're doing exact or fuzzy equivalency
                /// </summary>
                private bool _exactMatchRequired;

                /// <summary>
                /// Constructor
                /// </summary>
                private RegisteredTaskIdentityComparer(bool exactMatchRequired)
                {
                    _exactMatchRequired = exactMatchRequired;
                }

                /// <summary>
                /// The singleton comparer to use for when an exact match is desired
                /// </summary>
                public static RegisteredTaskIdentityComparer Exact
                {
                    get { return s_exact; }
                }

                /// <summary>
                /// The singleton comparer to use for when a fuzzy match is desired
                /// </summary>
                public static RegisteredTaskIdentityComparer Fuzzy
                {
                    get { return s_fuzzy; }
                }

                /// <summary>
                /// Returns true if these two identities match "fuzzily" -- if the names pass a partial type name
                /// match and the task identity parameters would constitute a valid merge (e.g. "don't care" and
                /// something explicit).  Otherwise returns false.
                /// </summary>
                public static bool IsPartialMatch(RegisteredTaskIdentity x, RegisteredTaskIdentity y)
                {
                    return TypeLoader.IsPartialTypeNameMatch(x.Name, y.Name)
                        ? IdentityParametersMatch(x.TaskIdentityParameters, y.TaskIdentityParameters, false /* fuzzy match */)
                        : false;
                }

                /// <summary>
                /// Returns true if the two task identities are equal; false otherwise.
                /// </summary>
                public bool Equals(RegisteredTaskIdentity x, RegisteredTaskIdentity y)
                {
                    if (x == null && y == null)
                    {
                        return true;
                    }

                    if (x == null || y == null)
                    {
                        return false;
                    }

                    // have to have the same name
                    if (String.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return IdentityParametersMatch(x.TaskIdentityParameters, y.TaskIdentityParameters, _exactMatchRequired);
                    }
                    else
                    {
                        return false;
                    }
                }

                /// <summary>
                /// Returns a hash code for the given task identity
                /// </summary>
                public int GetHashCode(RegisteredTaskIdentity obj)
                {
                    if (obj == null)
                    {
                        return 0;
                    }

                    int nameHash = String.IsNullOrEmpty(obj.Name) ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name);

                    // Since equality for the exact comparer depends on the exact values of the parameters,
                    // we need our hash code to depend on them as well. However, for fuzzy matches, we just
                    // need the ultimate meaning of the parameters to be the same.
                    int paramHash;
                    if (_exactMatchRequired)
                    {
                        int runtimeHash = obj.TaskIdentityParameters.Runtime == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TaskIdentityParameters.Runtime);
                        int architectureHash = obj.TaskIdentityParameters.Architecture == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TaskIdentityParameters.Architecture);

                        paramHash = runtimeHash ^ architectureHash;
                    }
                    else
                    {
                        // Ideally, we'd like a hash code that returns the same thing for any runtime or
                        // architecture that is counted as a match in Runtime/ArchitectureValuesMatch.
                        // But since we can't really know that without having someone to compare against,
                        // in this case just give up and don't try to factor the runtime / architecture
                        // in, and take the minor hit of having more matching hash codes than we would
                        // have otherwise.
                        paramHash = 0;
                    }

                    return nameHash ^ paramHash;
                }

                /// <summary>
                /// Returns true if the two TaskHostParameters match; false otherwise.
                /// Internal so that RegisteredTaskRecord can use this function in its determination of whether the task factory
                /// supports a certain task identity.
                /// </summary>
                private static bool IdentityParametersMatch(in TaskHostParameters x, in TaskHostParameters y, bool exactMatchRequired)
                {
                    // Both empty - match
                    if (x.IsEmpty && y.IsEmpty)
                    {
                        return true;
                    }

                    if (exactMatchRequired)
                    {
                        // For exact match, one empty means no match
                        if (x.IsEmpty || y.IsEmpty)
                        {
                            return false;
                        }

                        // For exact match, all properties must be equal (case-insensitive)
                        return string.Equals(x.Runtime, y.Runtime, StringComparison.OrdinalIgnoreCase) &&
                               string.Equals(x.Architecture, y.Architecture, StringComparison.OrdinalIgnoreCase) &&
                               string.Equals(x.DotnetHostPath, y.DotnetHostPath, StringComparison.OrdinalIgnoreCase) &&
                               string.Equals(x.MSBuildAssemblyPath, y.MSBuildAssemblyPath, StringComparison.OrdinalIgnoreCase) &&
                               x.TaskHostFactoryExplicitlyRequested == y.TaskHostFactoryExplicitlyRequested;
                    }
                    else
                    {
                        // Fuzzy match: null is treated as "don't care"
                        // Only check runtime and architecture for fuzzy matching

                        string runtimeX = x.Runtime;
                        string runtimeY = y.Runtime;
                        string architectureX = x.Architecture;
                        string architectureY = y.Architecture;

                        // null is OK -- it's treated as a "don't care"
                        if (!XMakeAttributes.RuntimeValuesMatch(runtimeX, runtimeY))
                        {
                            return false;
                        }

                        if (!XMakeAttributes.ArchitectureValuesMatch(architectureX, architectureY))
                        {
                            return false;
                        }
                    }

                    // if we didn't return before now, all parameters matched
                    return true;
                }
            }

            public void Translate(ITranslator translator)
            {
                translator.Translate(ref _name);
                translator.Translate(ref _taskIdentityParameters);
            }
        }

        /// <summary>
        /// A record for a task registration which also contains the factory which matches the record
        /// </summary>
        internal class RegisteredTaskRecord : ITranslatable
        {
            /// <summary>
            /// Default task factory to use if one is not specified
            /// </summary>
            internal const string AssemblyTaskFactory = "AssemblyTaskFactory";

            /// <summary>
            /// Default task factory to use if one is not specified and runtime or architecture is specified
            /// </summary>
            internal const string TaskHostFactory = "TaskHostFactory";

            /// <summary>
            /// Task factory used to create CodeDom-based inline tasks.  Special-cased as one of two officially
            /// supported task factories in Microsoft.Build.Tasks.vX.Y.dll to deal with versioning issue.
            /// </summary>
            internal const string CodeTaskFactory = "CodeTaskFactory";

            /// <summary>
            /// Task factory used to create CodeDom-based inline tasks.  Special-cased as one of two officially
            /// supported task factories in Microsoft.Build.Tasks.vX.Y.dll to deal with versioning issue.
            /// </summary>
            internal const string XamlTaskFactory = "XamlTaskFactory";

            /// <summary>
            /// Lock for the taskFactoryTypeLoader
            /// </summary>
            private static readonly LockType s_taskFactoryTypeLoaderLock = new();

#if DEBUG
            /// <summary>
            /// Inform users that this is a problem from a task factory, a bug should be opened against the factory user
            /// </summary>
            private const string UnhandledFactoryError = "\nThis is an unhandled exception from a task factory-- PLEASE OPEN A BUG AGAINST THE TASK FACTORY OWNER. ";
#endif

            /// <summary>
            /// Type filter to make sure we only look for taskFactoryClasses
            /// </summary>
            private static readonly Func<Type, object, bool> s_taskFactoryTypeFilter = IsTaskFactoryClass;

            /// <summary>
            /// Lock object to ensure that only one thread can access the task factory type loader at a time.
            /// </summary>
            private readonly LockType _lockObject = new();

            /// <summary>
            /// Identity of this task.
            /// </summary>
            private RegisteredTaskIdentity _taskIdentity;

            /// <summary>
            /// Typeloader for taskFactories
            /// </summary>
            private static TypeLoader s_taskFactoryTypeLoader;

            /// <summary>
            /// The task name this record was registered with from the using task element
            /// </summary>
            private string _registeredName;

            /// <summary>
            /// The assembly information about the task factory to be instantiated. For
            /// AssemblyTaskFactories this is the task assembly which should be loaded
            /// </summary>
            private AssemblyLoadInfo _taskFactoryAssemblyLoadInfo;

            /// <summary>
            /// The task factory class name which will be used to lookup the task factory from the assembly specified in the assemblyName or assemblyFile.
            /// </summary>
            private string _taskFactory;

            /// <summary>
            /// A task factory wrapper which caches and combines information related to the parameters of the task.
            /// </summary>
            private TaskFactoryWrapper _taskFactoryWrapperInstance;

            /// <summary>
            /// Cache of task names which can be created by the factory.
            /// When ever a taskName is checked against the factory we cache the result so we do not have to
            /// make possibly expensive calls over and over again. We intentionally do not use a ConcurrentDictionary here
            /// for performance reasons, since a concurrent dictionary is much larger than a regular dictionary. The usage
            /// scope is limited, so we can just lock on a regular dictionary.
            /// </summary>
            private Dictionary<RegisteredTaskIdentity, object> _taskNamesCreatableByFactory;

            /// <summary>
            /// Parameters that can be used by the task factory specifically.
            /// </summary>
            private TaskHostParameters _taskFactoryParameters;

            /// <summary>
            /// Encapsulates the parameters and the body of the task element for the inline task.
            /// </summary>
            private ParameterGroupAndTaskElementRecord _parameterGroupAndTaskBody;

            /// <summary>
            /// The registration order id for this task.  This is used to determine the order in which tasks are registered.
            /// </summary>
            private int _registrationOrderId;

            /// <summary>
            /// Full path to the file that contains definition of this task.
            /// </summary>
            private string _definingFileFullPath;

            /// <summary>
            /// Execution statistics for the tasks.
            /// Not translatable - the statistics are anyway expected to be reset after each project request.
            /// </summary>
            internal Stats Statistics { get; private init; } = new Stats();

            internal class Stats()
            {
                public short ExecutedCount { get; private set; } = 0;
                public long TotalMemoryConsumption { get; private set; } = 0;
                private readonly Stopwatch _executedSw = new Stopwatch();
                private long _memoryConsumptionOnStart;

                public TimeSpan ExecutedTime => _executedSw.Elapsed;

                public void ExecutionStarted()
                {
                    _memoryConsumptionOnStart = GetMemoryAllocated();
                    _executedSw.Start();
                    ExecutedCount++;
                }

                public void ExecutionStopped()
                {
                    _executedSw.Stop();
                    TotalMemoryConsumption += GetMemoryAllocated() - _memoryConsumptionOnStart;
                }

                private static long GetMemoryAllocated()
                {
#if NET
                    return GC.GetTotalAllocatedBytes(false);
#else
                    return GC.GetTotalMemory(false);
#endif
                }

                public void Reset()
                {
                    ExecutedCount = 0;
                    _executedSw.Reset();
                    TotalMemoryConsumption = 0;
                }
            }

            /// <summary>
            /// Constructor
            /// </summary>
            internal RegisteredTaskRecord(
                string registeredName,
                AssemblyLoadInfo assemblyLoadInfo,
                string taskFactory,
                in TaskHostParameters taskFactoryParameters,
                ParameterGroupAndTaskElementRecord inlineTask,
                int registrationOrderId,
                string containingFileFullPath)
            {
                ErrorUtilities.VerifyThrowArgumentNull(assemblyLoadInfo, "AssemblyLoadInfo");
                _registeredName = registeredName;
                _taskFactoryAssemblyLoadInfo = assemblyLoadInfo;
                _taskFactoryParameters = taskFactoryParameters;
                _taskIdentity = new RegisteredTaskIdentity(registeredName, taskFactoryParameters);
                _parameterGroupAndTaskBody = inlineTask;
                _registrationOrderId = registrationOrderId;

                if (string.IsNullOrEmpty(taskFactory))
                {
                    if (!taskFactoryParameters.IsEmpty)
                    {
                        ErrorUtilities.VerifyThrow(
                            taskFactoryParameters.Runtime != null && taskFactoryParameters.Architecture != null,
                            "if the parameters are non-null, it should contain both Runtime and Architecture when we get here!");
                    }

                    _taskFactory = AssemblyTaskFactory;
                }
                else
                {
                    _taskFactory = taskFactory;
                }

                if (inlineTask == null)
                {
                    _parameterGroupAndTaskBody = new ParameterGroupAndTaskElementRecord();
                }

                _definingFileFullPath = containingFileFullPath;
            }

            private RegisteredTaskRecord()
            {
            }

            /// <summary>
            /// Evaluates whether the current task is assumed to be defined within the user code - as opposed
            ///  to being a built-in task, or task authored by Microsoft brought to build via sdk, common targets or nuget.
            /// </summary>
            public bool ComputeIfCustom()
            {
                return
                    (
                        // There are occurrences of inline tasks within common targets (Microsoft.CodeAnalysis.Targets - SetEnvironmentVariable),
                        //  so we need to check file as well (the very last condition).
                        !string.IsNullOrEmpty(_parameterGroupAndTaskBody?.InlineTaskXmlBody) ||
                        (!string.IsNullOrEmpty(_taskFactoryAssemblyLoadInfo.AssemblyName) &&
                         !FileClassifier.IsMicrosoftAssembly(_taskFactoryAssemblyLoadInfo.AssemblyName)) ||
                        (!string.IsNullOrEmpty(_taskFactoryAssemblyLoadInfo.AssemblyFile) &&
                         // This condition will as well capture Microsoft tasks pulled from NuGet cache - since we decide based on assembly name.
                         // Hence we do not have to add the 'IsMicrosoftPackageInNugetCache' call anywhere here
                         !FileClassifier.IsMicrosoftAssembly(Path.GetFileName(_taskFactoryAssemblyLoadInfo.AssemblyFile)) &&
                         !FileClassifier.Shared.IsBuiltInLogic(_taskFactoryAssemblyLoadInfo.AssemblyFile)))
                    // and let's consider all tasks imported by common targets as non custom logic.
                    && !FileClassifier.Shared.IsBuiltInLogic(_definingFileFullPath);
            }

            public bool IsFromNugetCache
                => FileClassifier.Shared.IsInNugetCache(_taskFactoryAssemblyLoadInfo.AssemblyFile) ||
                   FileClassifier.Shared.IsInNugetCache(_definingFileFullPath);

            /// <summary>
            /// Gets the task name this record was registered with.
            /// </summary>
            internal string RegisteredName
            {
                [DebuggerStepThrough]
                get
                { return _registeredName; }
            }

            /// <summary>
            /// Gets the assembly load information.
            /// </summary>
            internal AssemblyLoadInfo TaskFactoryAssemblyLoadInfo
            {
                [DebuggerStepThrough]
                get
                { return _taskFactoryAssemblyLoadInfo; }
            }

            /// <summary>
            /// Gets the task factory attribute value.
            /// </summary>
            internal string TaskFactoryAttributeName
            {
                [DebuggerStepThrough]
                get
                { return _taskFactory; }
            }

            /// <summary>
            /// Gets the set of parameters for the task factory.
            /// </summary>
            internal TaskHostParameters TaskFactoryParameters
            {
                [DebuggerStepThrough]
                get => _taskFactoryParameters;
            }

            /// <summary>
            /// Gets the inline task record
            /// </summary>
            internal ParameterGroupAndTaskElementRecord ParameterGroupAndTaskBody
            {
                [DebuggerStepThrough]
                get
                { return _parameterGroupAndTaskBody; }
            }

            /// <summary>
            /// Identity of this task.
            /// </summary>
            internal RegisteredTaskIdentity TaskIdentity => _taskIdentity;

            /// <summary>
            /// The registration order id for this task.  This is used to determine the order in which tasks are registered.
            /// </summary>
            internal int RegistrationOrderId => _registrationOrderId;

            /// <summary>
            /// Ask the question, whether or not the task name can be created by the task factory.
            /// To answer this question we need to instantiate and initialize the task factory and ask it if it can create the given task name.
            /// This question is useful for assembly tasks where the task may or may not be in an assembly, this can also be useful if the task factory
            /// loads an external file and uses that to generate the tasks.
            /// </summary>
            /// <returns>true if the task can be created by the factory, false if it cannot be created</returns>
            internal bool CanTaskBeCreatedByFactory(string taskName, string taskProjectFile, TaskHostParameters taskIdentityParameters, TargetLoggingContext targetLoggingContext, ElementLocation elementLocation, bool isMultiThreadedBuild)
            {
                // First check (fast path - no locking)
                if (_taskNamesCreatableByFactory == null)
                {
                    lock (_lockObject)
                    {
                        // Second check (inside lock - ensure only one thread initializes)

                        // Initialize the cache dictionary only when first needed.
                        // This approach ensures the dictionary is available regardless of how the RegisteredTaskRecord
                        // instance was created (constructor, deserialization, factory methods, etc.).
                        _taskNamesCreatableByFactory ??= new Dictionary<RegisteredTaskIdentity, object>(
                                RegisteredTaskIdentity.RegisteredTaskIdentityComparer.Exact);
                    }
                }

                RegisteredTaskIdentity taskIdentity = new RegisteredTaskIdentity(taskName, taskIdentityParameters);

                // See if the task name as already been checked against the factory, return the value if it has
                object creatableByFactory = null;
                lock (_lockObject)
                {
                    // If we already have a value for this task identity, return it
                    if (_taskNamesCreatableByFactory.TryGetValue(taskIdentity, out creatableByFactory))
                    {
                        return creatableByFactory != null;
                    }
                }

                try
                {
                    bool haveTaskFactory = GetTaskFactory(targetLoggingContext, elementLocation, taskProjectFile, isMultiThreadedBuild);

                    // Create task Factory will only actually create a factory once.
                    if (haveTaskFactory)
                    {
                        // If we are an AssemblyTaskFactory we can use the fact we are internal to the engine assembly to do some logging / exception throwing that regular factories cannot do,
                        // this is requried to remain compatible with orcas in terms of exceptions thrown / messages logged when a task cannot be found in an assembly.
                        if (TaskFactoryAttributeName == AssemblyTaskFactory || TaskFactoryAttributeName == TaskHostFactory)
                        {
                            // Also we only need to check to see if the task name can be created by the factory if the taskName does not equal the Registered name
                            // and the identity parameters don't match the factory's declared parameters.
                            // This is because when the task factory is instantiated we try and load the Registered name from the task factory and fail it it cannot be loaded
                            // therefore the fact that we have a factory means the Registered type and parameters can be created by the factory.
                            if (RegisteredTaskIdentity.RegisteredTaskIdentityComparer.Fuzzy.Equals(this.TaskIdentity, taskIdentity))
                            {
                                creatableByFactory = this;
                            }
                            else
                            {
                                // The method will handle exceptions related to asking if a task can be created and will throw an Invalid project file exception if there is a problem
                                bool createable = ((AssemblyTaskFactory)_taskFactoryWrapperInstance.TaskFactory).TaskNameCreatableByFactory(taskName, taskIdentityParameters, taskProjectFile, targetLoggingContext, elementLocation);

                                if (createable)
                                {
                                    creatableByFactory = this;
                                }
                                else
                                {
                                    creatableByFactory = null;
                                }
                            }
                        }
                        else
                        {
                            // Wrap arbitrary task factory calls because we do not know what kind of error handling they are doing.
                            try
                            {
                                bool createable = _taskFactoryWrapperInstance.IsCreatableByFactory(taskName);

                                if (createable)
                                {
                                    creatableByFactory = this;
                                }
                                else
                                {
                                    creatableByFactory = null;
                                }
                            }
                            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                            {
                                // Log e.ToString to give as much information about the failure of a "third party" call as possible.
                                string message =
#if DEBUG
                                UnhandledFactoryError +
#endif
                                e.ToString();
                                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "TaskLoadFailure", taskName, _taskFactoryWrapperInstance.Name, message);
                            }
                        }
                    }
                }
                finally
                {
                    lock (_lockObject)
                    {
                        _taskNamesCreatableByFactory[taskIdentity] = creatableByFactory;
                    }
                }

                return creatableByFactory != null;
            }

            /// <summary>
            /// Given a Registered task record and a task name. Check create an instance of the task factory using the record.
            /// If the factory is a assembly task factory see if the assemblyFile has the correct task inside of it.
            /// </summary>
            internal TaskFactoryWrapper GetTaskFactoryFromRegistrationRecord(string taskName, string taskProjectFile, in TaskHostParameters taskIdentityParameters, TargetLoggingContext targetLoggingContext, ElementLocation elementLocation, bool isMultiThreadedBuild)
            {
                if (CanTaskBeCreatedByFactory(taskName, taskProjectFile, taskIdentityParameters, targetLoggingContext, elementLocation, isMultiThreadedBuild))
                {
                    return _taskFactoryWrapperInstance;
                }

                return null;
            }

            /// <summary>
            /// Create an instance of the task factory and load it from the assembly.
            /// </summary>
            /// <exception cref="InvalidProjectFileException">If the task factory could not be properly created an InvalidProjectFileException will be thrown</exception>
            private bool GetTaskFactory(TargetLoggingContext targetLoggingContext, ElementLocation elementLocation, string taskProjectFile, bool isMultiThreadedBuild)
            {
                // see if we have already created the factory before, only create it once
                if (_taskFactoryWrapperInstance == null)
                {
                    AssemblyLoadInfo taskFactoryLoadInfo = TaskFactoryAssemblyLoadInfo;
                    ErrorUtilities.VerifyThrow(taskFactoryLoadInfo != null, "TaskFactoryLoadInfo should never be null");
                    ITaskFactory factory = null;
                    LoadedType loadedType = null;

                    bool isAssemblyTaskFactory = String.Equals(TaskFactoryAttributeName, AssemblyTaskFactory, StringComparison.OrdinalIgnoreCase);
                    bool isTaskHostFactory = String.Equals(TaskFactoryAttributeName, TaskHostFactory, StringComparison.OrdinalIgnoreCase);

                    if (isTaskHostFactory)
                    {
                        _taskFactoryParameters = _taskFactoryParameters.WithTaskHostFactoryExplicitlyRequested(true);
                    }

                    if (isAssemblyTaskFactory || isTaskHostFactory)
                    {
                        // If ForceAllTasksOutOfProc is true, we will force all tasks to run in the MSBuild task host
                        // "EXCEPT a small well-known set of tasks that are known to depend on IBuildEngine callbacks
                        // as forcing those out of proc would be just setting them up for known failure"
                        bool launchTaskHost =
                            isTaskHostFactory ||
                            (
                                Traits.Instance.ForceAllTasksOutOfProcToTaskHost &&
                                !TypeLoader.IsPartialTypeNameMatch(RegisteredName, "MSBuild") &&
                                !TypeLoader.IsPartialTypeNameMatch(RegisteredName, "CallTarget"));

                        // Create an instance of the internal assembly task factory, it has the error handling built into its methods.
                        AssemblyTaskFactory taskFactory = new AssemblyTaskFactory();
                        loadedType = taskFactory.InitializeFactory(taskFactoryLoadInfo, RegisteredName, ParameterGroupAndTaskBody.UsingTaskParameters, ParameterGroupAndTaskBody.InlineTaskXmlBody, TaskFactoryParameters, launchTaskHost, targetLoggingContext, elementLocation, taskProjectFile);
                        factory = taskFactory;
                    }
                    else
                    {
                        // We are not one of the default factories.
                        TaskEngineAssemblyResolver resolver = null;

                        try
                        {
                            // Add a resolver to allow us to resolve types from the assembly when loading into the current appdomain.
                            resolver = new TaskEngineAssemblyResolver();
                            resolver.Initialize(taskFactoryLoadInfo.AssemblyFile);
                            resolver.InstallHandler();

                            try
                            {
                                lock (s_taskFactoryTypeLoaderLock)
                                {
                                    if (s_taskFactoryTypeLoader == null)
                                    {
                                        s_taskFactoryTypeLoader = new TypeLoader(s_taskFactoryTypeFilter);
                                    }
                                }

                                // Make sure we only look for task factory classes when loading based on the name
                                loadedType = s_taskFactoryTypeLoader.Load(TaskFactoryAttributeName, taskFactoryLoadInfo);

                                if (loadedType == null)
                                {
                                    // We could not find the type (this is what null means from the Load method) but there is no reason given so we can only log the fact that
                                    // we could not find the name given in the task factory attribute in the class specified in the assembly File or assemblyName fields.
                                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CouldNotFindFactory", TaskFactoryAttributeName, taskFactoryLoadInfo.AssemblyLocation);
                                }

                                targetLoggingContext.LogComment(MessageImportance.Low, "InitializingTaskFactory", TaskFactoryAttributeName, taskFactoryLoadInfo.AssemblyLocation);
                            }
                            catch (TargetInvocationException e)
                            {
                                // Exception thrown by the called code itself
                                // Log the stack, so the task vendor can fix their code
                                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "TaskFactoryLoadFailure", TaskFactoryAttributeName, taskFactoryLoadInfo.AssemblyLocation, Environment.NewLine + e.InnerException.ToString());
                            }
                            catch (ReflectionTypeLoadException e)
                            {
                                // ReflectionTypeLoadException.LoaderExceptions may contain nulls
                                foreach (Exception exception in e.LoaderExceptions)
                                {
                                    if (exception != null)
                                    {
                                        targetLoggingContext.LogError(new BuildEventFileInfo(taskProjectFile), "TaskFactoryLoadFailure", TaskFactoryAttributeName, taskFactoryLoadInfo.AssemblyLocation, exception.Message);
                                    }
                                }

                                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "TaskFactoryLoadFailure", TaskFactoryAttributeName, taskFactoryLoadInfo.AssemblyLocation, e.Message);
                            }
                            catch (Exception e) when (!ExceptionHandling.NotExpectedReflectionException(e))
                            {
                                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "TaskFactoryLoadFailure", TaskFactoryAttributeName, taskFactoryLoadInfo.AssemblyLocation, e.Message);
                            }

                            try
                            {
                                // We have loaded the type, lets now try and construct it
                                // Any exceptions from the constructor of the task factory will be caught lower down and turned into an InvalidProjectFileExceptions
#if FEATURE_APPDOMAIN
                                factory = (ITaskFactory)AppDomain.CurrentDomain.CreateInstanceAndUnwrap(loadedType.Type.GetTypeInfo().Assembly.FullName, loadedType.Type.FullName);
#else
                                factory = (ITaskFactory)Activator.CreateInstance(loadedType.Type);
#endif
                                TaskFactoryEngineContext taskFactoryLoggingHost = new TaskFactoryEngineContext(true /*I dont have the data at this point, the safest thing to do is make sure events are serializable*/, elementLocation, targetLoggingContext, isMultiThreadedBuild, Traits.Instance.ForceTaskFactoryOutOfProc);

                                bool initialized = false;
                                try
                                {
                                    // for backward compatibility with public interface
                                    if (factory is ITaskFactory2 factory2)
                                    {
                                        var taskFactoryParams = new Dictionary<string, string>(3)
                                        {
                                            { nameof(TaskHostParameters.Runtime), TaskFactoryParameters.Runtime },
                                            { nameof(TaskHostParameters.Architecture), TaskFactoryParameters.Architecture },
                                            { nameof(TaskHostParameters.TaskHostFactoryExplicitlyRequested), TaskFactoryParameters.TaskHostFactoryExplicitlyRequested.ToString() },
                                        };

                                        initialized = factory2.Initialize(RegisteredName, taskFactoryParams, ParameterGroupAndTaskBody.UsingTaskParameters, ParameterGroupAndTaskBody.InlineTaskXmlBody, taskFactoryLoggingHost);
                                    }
                                    else if (factory is ITaskFactory3 factory3)
                                    {
                                        initialized = factory3.Initialize(RegisteredName, TaskFactoryParameters, ParameterGroupAndTaskBody.UsingTaskParameters, ParameterGroupAndTaskBody.InlineTaskXmlBody, taskFactoryLoggingHost);
                                    }
                                    else
                                    {
                                        initialized = factory.Initialize(RegisteredName, ParameterGroupAndTaskBody.UsingTaskParameters, ParameterGroupAndTaskBody.InlineTaskXmlBody, taskFactoryLoggingHost);

                                        // TaskFactoryParameters will always be null unless specifically created to have runtime and architecture parameters.
                                        // In case TaskHostFactory is explicitly requested, we will now have a parameter for that.
                                        bool containsArchOrRuntimeParam = TaskFactoryParameters.Runtime != null
                                                                          || TaskFactoryParameters.Architecture != null;

                                        if (initialized && containsArchOrRuntimeParam)
                                        {
                                            targetLoggingContext.LogWarning(
                                                null,
                                                    new BuildEventFileInfo(elementLocation),
                                                    "TaskFactoryWillIgnoreTaskFactoryParameters",
                                                    factory.FactoryName,
                                                    XMakeAttributes.runtime,
                                                    XMakeAttributes.architecture,
                                                RegisteredName);
                                        }
                                    }

                                    // Throw an error if the ITaskFactory did not set the TaskType property.  If the property is null, it can cause NullReferenceExceptions in our code
                                    if (initialized && factory.TaskType == null)
                                    {
                                        throw new InvalidOperationException(AssemblyResources.GetString("TaskFactoryTaskTypeIsNotSet"));
                                    }
                                }
                                finally
                                {
#if FEATURE_APPDOMAIN
                                    taskFactoryLoggingHost.MarkAsInactive();
#endif
                                }

                                if (!initialized)
                                {
                                    _taskFactoryWrapperInstance = null;
                                    return false;
                                }
                            }
                            catch (InvalidCastException e)
                            {
                                string message = String.Empty;
#if DEBUG
                                message += UnhandledFactoryError;
#endif
                                message += e.Message;

                                // Could get an invalid cast when Creating Instance and UnWrap due to the framework assembly not being the same.
                                targetLoggingContext.LogError(
                                    new BuildEventFileInfo(elementLocation.File, elementLocation.Line, elementLocation.Column),
                                    "TaskFactoryInstantiationFailureErrorInvalidCast",
                                    TaskFactoryAttributeName,
                                    taskFactoryLoadInfo.AssemblyLocation,
                                    message);

                                return false;
                            }
                            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                            {
                                string message =
#if DEBUG
                                UnhandledFactoryError +
#endif
                                e.Message;

                                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "TaskFactoryLoadFailure", TaskFactoryAttributeName, taskFactoryLoadInfo.AssemblyLocation, message);
                            }
                        }
                        finally
                        {
                            if (resolver != null)
                            {
                                resolver.RemoveHandler();
                                resolver = null;
                            }
                        }
                    }

                    _taskFactoryWrapperInstance = new TaskFactoryWrapper(factory, loadedType, RegisteredName, TaskFactoryParameters, Statistics);
                }

                return true;
            }

            /// <summary>
            /// Keep track of the xml which will be sent to the inline task factory and the parameters if any which will also be passed in
            /// </summary>
            internal class ParameterGroupAndTaskElementRecord : ITranslatable
            {
                /// <summary>
                /// The list of parameters found in the using task along with a corosponding UsingTaskParameterInfo which contains the specific information about it
                /// Populated lazily as it is often empty.
                /// </summary>
                private IDictionary<string, TaskPropertyInfo> _usingTaskParameters;

                /// <summary>
                /// The body of the task element which will be passed to the task factory.
                /// </summary>
                private string _inlineTaskXmlBody;

                /// <summary>
                /// Was the task body evaluated or not
                /// </summary>
                private bool _taskBodyEvaluated;

                /// <summary>
                /// Create an empty ParameterGroupAndTaskElementRecord
                /// </summary>
                public ParameterGroupAndTaskElementRecord()
                {
                }

                /// <summary>
                /// The parameters from the ParameterGroup from the using task element which will be passed to the task factory.
                /// </summary>
                internal IDictionary<string, TaskPropertyInfo> UsingTaskParameters
                {
                    get { return _usingTaskParameters ?? ReadOnlyEmptyDictionary<string, TaskPropertyInfo>.Instance; }
                }

                /// <summary>
                /// The body of the task element which will be passed to the task factory.
                /// </summary>
                internal string InlineTaskXmlBody
                {
                    get { return _inlineTaskXmlBody; }
                }

                /// <summary>
                /// Has the task body been passed to the expander to be expanded
                /// </summary>
                internal bool TaskBodyEvaluated
                {
                    get { return _taskBodyEvaluated; }
                }

                /// <summary>
                /// Keep track of the xml which will be sent to the inline task factory and the parameters if any which will also be passed in
                /// </summary>
                /// <typeparam name="P">Property type</typeparam>
                /// <typeparam name="I">Item Type</typeparam>
                internal void ExpandUsingTask<P, I>(ProjectUsingTaskElement projectUsingTaskXml, Expander<P, I> expander, ExpanderOptions expanderOptions)
                    where P : class, IProperty
                    where I : class, IItem
                {
                    ErrorUtilities.VerifyThrowArgumentNull(projectUsingTaskXml);
                    ErrorUtilities.VerifyThrowArgumentNull(expander);

                    ProjectUsingTaskBodyElement taskElement = projectUsingTaskXml.TaskBody;
                    if (taskElement != null)
                    {
                        EvaluateTaskBody<P, I>(expander, taskElement, expanderOptions);
                    }

                    UsingTaskParameterGroupElement parameterGroupElement = projectUsingTaskXml.ParameterGroup;

                    if (parameterGroupElement != null)
                    {
                        ParseUsingTaskParameterGroupElement<P, I>(parameterGroupElement, expander, expanderOptions);
                    }
                }

                /// <summary>
                /// Evaluate the task body of the using task
                /// </summary>
                /// <typeparam name="P">IProperttyTypes</typeparam>
                /// <typeparam name="I">IItems</typeparam>
                private void EvaluateTaskBody<P, I>(Expander<P, I> expander, ProjectUsingTaskBodyElement taskElement, ExpanderOptions expanderOptions)
                    where P : class, IProperty
                    where I : class, IItem
                {
                    bool evaluate;
                    string expandedType = expander.ExpandIntoStringLeaveEscaped(taskElement.Evaluate, expanderOptions, taskElement.EvaluateLocation);

                    if (!Boolean.TryParse(expandedType, out evaluate))
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(
                         taskElement.EvaluateLocation,
                         "InvalidEvaluatedAttributeValue",
                         expandedType,
                         taskElement.Evaluate,
                         XMakeAttributes.evaluate,
                         XMakeElements.usingTaskBody);
                    }

                    _taskBodyEvaluated = evaluate;

                    // If we need to evaluate then expand and evaluate the next inside of the body
                    if (evaluate)
                    {
                        _inlineTaskXmlBody = expander.ExpandIntoStringLeaveEscaped(taskElement.TaskBody, expanderOptions, taskElement.Location);
                    }
                    else
                    {
                        _inlineTaskXmlBody = taskElement.TaskBody;
                    }
                }

                /// <summary>
                /// Convert the UsingTaskParameterGroupElement into a list of parameter names and UsingTaskParameters
                /// </summary>
                /// <typeparam name="P">Property type</typeparam>
                /// <typeparam name="I">Item types</typeparam>
                private void ParseUsingTaskParameterGroupElement<P, I>(UsingTaskParameterGroupElement usingTaskParameterGroup, Expander<P, I> expander, ExpanderOptions expanderOptions)
                    where P : class, IProperty
                    where I : class, IItem
                {
                    _usingTaskParameters ??= new Dictionary<string, TaskPropertyInfo>(StringComparer.OrdinalIgnoreCase);

                    // Go through each of the parameters and create new ParameterInfo objects from them
                    foreach (ProjectUsingTaskParameterElement parameter in usingTaskParameterGroup.Parameters)
                    {
                        // Expand the type value before parsing it because it could be a property or item which needs to be expanded before it make sense
                        string expandedType = expander.ExpandIntoStringLeaveEscaped(parameter.ParameterType, expanderOptions, parameter.ParameterTypeLocation);

                        // Cannot have a null or empty name for the type after expansion.
                        ProjectErrorUtilities.VerifyThrowInvalidProject(
                            !String.IsNullOrEmpty(expandedType),
                            parameter.ParameterTypeLocation,
                            "InvalidEvaluatedAttributeValue",
                            expandedType,
                            parameter.ParameterType,
                            XMakeAttributes.parameterType,
                            XMakeElements.usingTaskParameter);

                        Type paramType;
                        if (expandedType.StartsWith("Microsoft.Build.Framework.", StringComparison.OrdinalIgnoreCase) && !expandedType.Contains(","))
                        {
                            // This is workaround for internal bug https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1448821
                            // Visual Studio can load different version of Microsoft.Build.Framework.dll and non fully classified type could be resolved from it
                            // which cause InvalidProjectFileException with "UnsupportedTaskParameterTypeError" message.
                            // Another way to address this is to load types from compiled assembly - that would be more robust solution but also much more complex and risky code changes.
                            paramType = Type.GetType(expandedType + "," + typeof(ITaskItem).GetTypeInfo().Assembly.FullName, false /* don't throw on error */, true /* case-insensitive */) ??
                                        Type.GetType(expandedType);
                        }
                        else
                        {
                            paramType = Type.GetType(expandedType) ??
                                        Type.GetType(expandedType + "," + typeof(ITaskItem).GetTypeInfo().Assembly.FullName, false /* don't throw on error */, true /* case-insensitive */);
                        }

                        ProjectErrorUtilities.VerifyThrowInvalidProject(
                            paramType != null,
                            parameter.ParameterTypeLocation,
                            "InvalidEvaluatedAttributeValue",
                            expandedType,
                            parameter.ParameterType,
                            XMakeAttributes.parameterType,
                            XMakeElements.usingTaskParameter);

                        bool output;
                        string expandedOutput = expander.ExpandIntoStringLeaveEscaped(parameter.Output, expanderOptions, parameter.OutputLocation);

                        if (!Boolean.TryParse(expandedOutput, out output))
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(
                                parameter.OutputLocation,
                                "InvalidEvaluatedAttributeValue",
                                expandedOutput,
                                parameter.Output,
                                XMakeAttributes.output,
                                XMakeElements.usingTaskParameter);
                        }

                        if (
                            (!output && (!TaskParameterTypeVerifier.IsValidInputParameter(paramType))) ||
                            (output && !TaskParameterTypeVerifier.IsValidOutputParameter(paramType)))
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(
                                parameter.Location,
                                "UnsupportedTaskParameterTypeError",
                                paramType.FullName,
                                parameter.ParameterType,
                                parameter.Name);
                        }

                        bool required;
                        string expandedRequired = expander.ExpandIntoStringLeaveEscaped(parameter.Required, expanderOptions, parameter.RequiredLocation);

                        if (!Boolean.TryParse(expandedRequired, out required))
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(
                                parameter.RequiredLocation,
                                "InvalidEvaluatedAttributeValue",
                                expandedRequired,
                                parameter.Required,
                                XMakeAttributes.required,
                                XMakeElements.usingTaskParameter);
                        }

                        UsingTaskParameters.Add(parameter.Name, new TaskPropertyInfo(parameter.Name, paramType, output, required));
                    }
                }

                public void Translate(ITranslator translator)
                {
                    translator.Translate(ref _inlineTaskXmlBody);
                    translator.Translate(ref _taskBodyEvaluated);

                    translator.TranslateDictionary(ref _usingTaskParameters, TranslatorForTaskParametersKey, TranslatorForTaskParameterValue, count => new Dictionary<string, TaskPropertyInfo>(StringComparer.OrdinalIgnoreCase));
                }

                // todo move to nested function after C# 7
                private static void TranslatorForTaskParametersKey(ITranslator translator, ref string key)
                {
                    translator.Translate(ref key);
                }

                // todo move to nested function after C# 7
                private static void TranslatorForTaskParameterValue(ITranslator translator, ref TaskPropertyInfo taskPropertyInfo)
                {
                    string name = null;
                    string propertyTypeName = null;
                    bool output = false;
                    bool required = false;

                    var writing = translator.Mode == TranslationDirection.WriteToStream;

                    if (writing)
                    {
                        name = taskPropertyInfo.Name;
                        propertyTypeName = taskPropertyInfo.PropertyType.AssemblyQualifiedName;
                        output = taskPropertyInfo.Output;
                        required = taskPropertyInfo.Required;
                    }

                    translator.Translate(ref name);
                    translator.Translate(ref output);
                    translator.Translate(ref required);
                    translator.Translate(ref propertyTypeName);

                    if (!writing)
                    {
                        Type propertyType = Type.GetType(propertyTypeName);
                        taskPropertyInfo = new TaskPropertyInfo(name, propertyType, output, required);
                    }
                }
            }

            public void Translate(ITranslator translator)
            {
                translator.Translate(ref _taskIdentity);
                translator.Translate(ref _registeredName);
                translator.Translate(ref _taskFactoryAssemblyLoadInfo, AssemblyLoadInfo.FactoryForTranslation);
                translator.Translate(ref _taskFactory);
                translator.Translate(ref _parameterGroupAndTaskBody);
                translator.Translate(ref _registrationOrderId);
                translator.Translate(ref _definingFileFullPath);
                translator.Translate(ref _taskFactoryParameters);
            }

            internal static RegisteredTaskRecord FactoryForDeserialization(ITranslator translator)
            {
                var instance = new RegisteredTaskRecord();
                instance.Translate(translator);

                return instance;
            }
        }

        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _toolset, Toolset.FactoryForDeserialization);
            translator.Translate(ref _nextRegistrationOrderId);
            IDictionary<RegisteredTaskIdentity, List<RegisteredTaskRecord>> copy = _taskRegistrations;
            translator.TranslateDictionary(ref copy, TranslateTaskRegistrationKey, TranslateTaskRegistrationValue, count => CreateRegisteredTaskDictionary(count));

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                _taskRegistrations = (Dictionary<RegisteredTaskIdentity, List<RegisteredTaskRecord>>)copy;
#if DEBUG
                _isInitialized = _taskRegistrations != null;
#endif
            }
        }

        // todo make nested after C# 7
        private void TranslateTaskRegistrationKey(ITranslator translator, ref RegisteredTaskIdentity taskIdentity)
        {
            translator.Translate(ref taskIdentity);
        }

        // todo make nested after C# 7
        private void TranslateTaskRegistrationValue(ITranslator translator, ref List<RegisteredTaskRecord> taskRecords)
        {
            translator.Translate(ref taskRecords, RegisteredTaskRecord.FactoryForDeserialization);
        }

        public static TaskRegistry FactoryForDeserialization(ITranslator translator)
        {
            var instance = new TaskRegistry();
            instance.Translate(translator);

            return instance;
        }
    }
}
