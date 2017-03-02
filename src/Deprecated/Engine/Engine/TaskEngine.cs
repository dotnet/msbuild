// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Globalization;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;
using System.Runtime.Remoting;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// The manner in which a task engine runs its assigned task.
    /// </summary>
    /// <remarks>
    /// This enum is public because it is passed between processes i.e. the engine
    /// and the node process.
    /// </remarks>
    /// <owner>SumedhK</owner>
    [Flags]
    internal enum TaskExecutionMode
    {
        /// <summary>
        /// This entry is necessary to use the enum with binary math. It is never used outside 
        /// intermediate calculations.
        /// </summary>
        Invalid = 0,
        /// <summary>
        /// In this mode, the task engine actually runs the task and retrieves its outputs.
        /// </summary>
        ExecuteTaskAndGatherOutputs = 1,
        /// <summary>
        /// In this mode, the task engine only infers the task's outputs from its &lt;Output&gt; tags.
        /// </summary>
        InferOutputsOnly = 2
    }

    /// <summary>
    /// This class is used by targets to execute tasks. This class encapsulates the information needed to run a single task once.
    /// </summary>
    /// <owner>SumedhK</owner>
    internal sealed class TaskEngine
    {
        #region Constructors

        /// <summary>
        /// Creates an instance of this class for the specified task.
        /// </summary>
        public TaskEngine
        (
            XmlElement taskNodeXmlElement,
            ITaskHost hostObject,
            string projectFileOfTaskNode,
            string parentProjectFullFileName,
            EngineLoggingServices loggingServices,
            int handleId,
            TaskExecutionModule parentModule,
            BuildEventContext targetBuildEventContext
        )
        {
            ErrorUtilities.VerifyThrow(taskNodeXmlElement != null, "Need to specify the task node.");
            ErrorUtilities.VerifyThrow(projectFileOfTaskNode != null, "Need to specify path of project.");
            ErrorUtilities.VerifyThrow(parentProjectFullFileName != null, "Need to specify name of project.");
            ErrorUtilities.VerifyThrow(loggingServices != null, "Need to specify the node logger.");

            this.taskNode = taskNodeXmlElement;
            this.taskClass = null;
            this.hostObject = hostObject;
            this.projectFileOfTaskNode = projectFileOfTaskNode;
            this.parentProjectFullFileName = parentProjectFullFileName;
            this.loggingServices = loggingServices;
            this.handleId = handleId;
            this.parentModule = parentModule;
            this.continueOnError = false;
            this.conditionAttribute = taskNode.Attributes[XMakeAttributes.condition];
            this.buildEventContext = targetBuildEventContext;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the string name of the task.
        /// </summary>
        private string TaskName
        {
            get { return taskNode.Name; }
        }

        /// <summary>
        /// Gets the .NET class that defines the task.
        /// </summary>
        internal LoadedType TaskClass
        {
            get { return taskClass; }
            set { taskClass = value; }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Build up a list of all parameters on the task, including those in any Output tags,
        /// in order to find batchable metadata references
        /// </summary>
        private List<string> CreateListOfParameterValues()
        {
            List<string> taskParameters = new List<string>(taskNode.Attributes.Count);

            foreach (XmlAttribute taskParameter in taskNode.Attributes)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(!XMakeAttributes.IsBadlyCasedSpecialTaskAttribute(taskParameter.Name), taskParameter,
                    "BadlyCasedSpecialTaskAttribute", taskParameter.Name, TaskName, TaskName);

                taskParameters.Add(taskParameter.Value);
            }

            // Add parameters on any output tags
            foreach (TaskOutput taskOutputSpecification in GetTaskOutputSpecifications(false /* no warnings */))
            {
                if (taskOutputSpecification.TaskParameterAttribute != null)
                {
                    taskParameters.Add(taskOutputSpecification.TaskParameterAttribute.Value);
                }

                if (taskOutputSpecification.ItemNameAttribute != null)
                {
                    taskParameters.Add(taskOutputSpecification.ItemNameAttribute.Value);
                }

                if (taskOutputSpecification.PropertyNameAttribute != null)
                {
                    taskParameters.Add(taskOutputSpecification.PropertyNameAttribute.Value);
                }

                if (taskOutputSpecification.ConditionAttribute != null)
                {
                    taskParameters.Add(taskOutputSpecification.ConditionAttribute.Value);
                }
            }

            return taskParameters;
        }

        /// <summary>
        /// Given the task XML, this method tries to find the task. It uses the following search order:
        /// 1) checks the tasks declared by the project, searching by exact name
        /// 2) checks the global task declarations (in *.TASKS in MSbuild bin dir), searching by exact name
        /// 3) checks the tasks declared by the project, searching by fuzzy match (missing namespace, etc.)
        /// 4) checks the global task declarations (in *.TASKS in MSbuild bin dir), searching by fuzzy match (missing namespace, etc.)
        /// 
        /// The search ordering is meant to reduce the number of assemblies we scan, because loading assemblies can be expensive.
        /// The tasks and assemblies declared by the project are scanned first, on the assumption that if the project declared
        /// them, they are likely used.
        /// </summary>
        /// <remarks>
        /// This is internal so that BuildTask.Type can call it.
        /// </remarks>
        /// <returns>true, if task was found</returns>
        internal bool FindTask()
        {
            // We may have been called earlier on a previous batch; if so,
            // there's no work to do.
            if (TaskClass == null)
            {
                if (!parentModule.GetProjectTasksRegistry(handleId).GetRegisteredTask(TaskName, projectFileOfTaskNode, taskNode, true /* exact match */, loggingServices, buildEventContext, out taskClass))
                {
                    if (!parentModule.GetDefaultTasksRegistry(handleId).GetRegisteredTask(TaskName, projectFileOfTaskNode, taskNode, true /* exact match */, loggingServices, buildEventContext, out taskClass))
                    {
                        if (!parentModule.GetProjectTasksRegistry(handleId).GetRegisteredTask(TaskName, projectFileOfTaskNode, taskNode, false /* fuzzy match */, loggingServices, buildEventContext, out taskClass))
                        {
                            if (!parentModule.GetDefaultTasksRegistry(handleId).GetRegisteredTask(TaskName, projectFileOfTaskNode, taskNode, false /* fuzzy match */, loggingServices, buildEventContext, out taskClass))
                            {
                                loggingServices.LogError(buildEventContext, CreateBuildEventFileInfoForTask(),
                                    "MissingTaskError", TaskName, parentModule.GetToolsPath(handleId));
                            }
                        }
                    }
                }
            }

            return (TaskClass != null);
        }

        /// <summary>
        /// Sets up an app domain for the task batch, if necessary
        /// </summary>
        private AppDomain PrepareAppDomain()
        {
            // If the task assembly is loaded into a separate AppDomain using LoadFrom, then we have a problem
            // to solve - when the task class Type is marshalled back into our AppDomain, it's not just transferred
            // here. Instead, NDP will try to Load (not LoadFrom!) the task assembly into our AppDomain, and since
            // we originally used LoadFrom, it will fail miserably not knowing where to find it.
            // We need to temporarily subscribe to the AppDomain.AssemblyResolve event to fix it.
            if (null == resolver)
            {
                resolver = new TaskEngineAssemblyResolver();
                resolver.Initialize(TaskClass.Assembly.AssemblyFile);
                resolver.InstallHandler();
            }

            bool hasLoadInSeparateAppDomainAttribute = false;
            try
            {
                hasLoadInSeparateAppDomainAttribute = TaskClass.HasLoadInSeparateAppDomainAttribute();
            }
            catch (Exception e) // Catching Exception, but rethrowing unless it's a well-known exception.
            {
                if (ExceptionHandling.NotExpectedReflectionException(e))
                    throw;

                // Reflection related exception
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, taskNode, "TaskInstantiationFailureError", TaskName, TaskClass.Assembly.ToString(), e.Message);
            }

            AppDomain taskAppDomain = null;

            if (hasLoadInSeparateAppDomainAttribute)
            {
                if (!TaskClass.Type.IsMarshalByRef)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, taskNode, "TaskNotMarshalByRef", TaskName);
                }
                else
                {
                        // Our task depend on this name to be precisely that, so if you change it make sure
                        // you also change the checks in the tasks run in separate AppDomains. Better yet, just don't change it.

                        // Make sure we copy the appdomain configuration and send it to the appdomain we create so that if the creator of the current appdomain
                        // has done the binding redirection in code, that we will get those settings as well.
                        AppDomainSetup appDomainInfo = new AppDomainSetup();

			// Get the current app domain setup settings
                        byte[] currentAppdomainBytes = AppDomain.CurrentDomain.SetupInformation.GetConfigurationBytes();

                        //Apply the appdomain settings to the new appdomain before creating it
                        appDomainInfo.SetConfigurationBytes(currentAppdomainBytes);
                        taskAppDomain = AppDomain.CreateDomain("taskAppDomain", null, appDomainInfo);
                 }
            }

            return taskAppDomain;
        }

        /// <summary>
        /// Called to execute a task within a target. This method instantiates the task, sets its parameters, and executes it. 
        /// </summary>
        /// <returns>true, if successful</returns>
        internal bool ExecuteTask(TaskExecutionMode howToExecuteTask, Lookup lookup)
        {
            ErrorUtilities.VerifyThrow(lookup != null, "Need to specify items available to task.");

            bool taskExecutedSuccessfully = false;
            EngineProxy engineProxy = null;

            ArrayList buckets = null;

            try
            {
                engineProxy = new EngineProxy(parentModule, handleId, parentProjectFullFileName, projectFileOfTaskNode, loggingServices, buildEventContext);
                List<string> taskParameterValues = CreateListOfParameterValues();
                buckets = BatchingEngine.PrepareBatchingBuckets(taskNode, taskParameterValues, lookup);

                lookupHash = null;
                // Only create a hash table if there are more than one bucket as this is the only time a property can be overridden
                if (buckets.Count > 1)
                {
                    lookupHash = Utilities.CreateTableIfNecessary((Hashtable)null);
                }
		
		// Loop through each of the batch buckets and execute them one at a time
                for (int i=0; i < buckets.Count; i++)
                {
                    // Execute the batch bucket, pass in which bucket we are executing so that we know when to get a new taskId for the bucket.
                    taskExecutedSuccessfully = ExecuteBucket(engineProxy, (ItemBucket)buckets[i], i, howToExecuteTask);
                    if (!taskExecutedSuccessfully)
                    {
                        break;
                    }
                }
            }
            finally
            {
                // Remove the AssemblyResolve handler in the default AppDomain, we are done with the task.
                if (resolver != null)
                {
                    resolver.RemoveHandler();
                }

                if (engineProxy != null)
                {
                    engineProxy.MarkAsInActive();
                }

                // Now all task batches are done, apply all item adds to the outer 
                // target batch; we do this even if the task wasn't found (in that case,
                // no items or properties will have been added to the scope)
                if (buckets != null)
                {
                    foreach (ItemBucket bucket in buckets)
                    {
                        bucket.Lookup.LeaveScope();
                    }
                }
            }

            return taskExecutedSuccessfully;
        }

        /// <summary>
        /// Execute a single bucket
        /// </summary>
        /// <returns>true if execution succeeded</returns>
        private bool ExecuteBucket(EngineProxy engineProxy, ItemBucket bucket, int bucketNumber, TaskExecutionMode howToExecuteTask)
        {
            if (
                    (this.conditionAttribute != null)
                    &&
                    !Utilities.EvaluateCondition(this.conditionAttribute.Value, this.conditionAttribute,
                        bucket.Expander, null, ParserOptions.AllowAll, loggingServices, buildEventContext)
                )
            {
                // Condition is false
                if (howToExecuteTask == TaskExecutionMode.ExecuteTaskAndGatherOutputs)
                {
                    if (!loggingServices.OnlyLogCriticalEvents)
                    {
                        // Expand the expression for the Log.
                        string expanded = bucket.Expander.ExpandAllIntoString(this.conditionAttribute);
                        // Whilst we are within the processing of the task, we haven't actually started executing it, so
                        // our skip task message needs to be in the context of the target. However any errors should be reported
                        // at the point where the task appears in the project.
                        BuildEventContext skipTaskContext = new BuildEventContext(buildEventContext.NodeId, buildEventContext.TargetId, buildEventContext.ProjectContextId, BuildEventContext.InvalidTaskId);
                        loggingServices.LogComment(skipTaskContext,
                                        "TaskSkippedFalseCondition",
                                         TaskName, this.conditionAttribute.Value, expanded);
                    }
                }

                return true;
            }

            bool taskExecutedSuccessfully = true;

            // Condition is true
            if (howToExecuteTask == TaskExecutionMode.ExecuteTaskAndGatherOutputs)
            {
                // Now that we know we will need to execute the task,
                // Ensure the TaskEngine is initialized with the task class
                // This does the work of task discovery, if it 
                // hasn't already been done.
                bool taskClassWasFound = FindTask();

                if (!taskClassWasFound)
                {
                    // Task wasn't discovered, we cannot continue
                    return false;
                }

                // Now instantiate, initialize, and execute the task
                ITask task;

                // If this is the first bucket use the task context originally given to it, for the remaining buckets get a unique id for them
                if (bucketNumber != 0)
                {
		    // Ask the parent engine the next Id which should be used for the taskId.
                    buildEventContext = new BuildEventContext(buildEventContext.NodeId, buildEventContext.TargetId, buildEventContext.ProjectContextId, parentModule.GetNextTaskId());

		    // For each batch the engineProxy needs to have the correct buildEventContext as all messages comming from a task will have the buildEventContext of the EngineProxy.
                    engineProxy.BuildEventContext = buildEventContext;
                }

                loggingServices.LogTaskStarted(buildEventContext, TaskName, parentProjectFullFileName, projectFileOfTaskNode);

                AppDomain taskAppDomain = PrepareAppDomain();

                bool taskResult = false;

                try
                {
                    task = InstantiateTask(taskAppDomain);

                    // If task cannot be instantiated, we consider its declaration/usage to be invalid.
                    ProjectErrorUtilities.VerifyThrowInvalidProject(task != null, taskNode, "TaskDeclarationOrUsageError", TaskName);
                    taskExecutedSuccessfully = ExecuteInstantiatedTask(engineProxy, bucket, howToExecuteTask, task, out taskResult);
                    if (lookupHash != null)
                    {
                        List<string> overrideMessages = bucket.Lookup.GetPropertyOverrideMessages(lookupHash);
                        if (overrideMessages != null)
                        {
                            foreach (string s in overrideMessages)
                            {
                                loggingServices.LogCommentFromText(buildEventContext, MessageImportance.Low, s);
                            }
                        }
                    }
                }
                catch (InvalidProjectFileException e)
                {
                    // Make sure the Invalid Project error gets logged *before* TaskFinished.  Otherwise,
                    // the log is confusing.
                    loggingServices.LogInvalidProjectFileError(buildEventContext, e);
                    throw;
                }
                finally
                {
                    // Flag the completion of the task.
                    loggingServices.LogTaskFinished(
                        buildEventContext,
                        TaskName,
                        parentProjectFullFileName,
                        projectFileOfTaskNode,
                        taskResult);

                    task = null;

                    if (taskAppDomain != null)
                    {
                        AppDomain.Unload(taskAppDomain);
                        taskAppDomain = null;
                    }
                }
            }
            else
            {
                Debug.Assert(howToExecuteTask == TaskExecutionMode.InferOutputsOnly);

                ErrorUtilities.VerifyThrow(GatherTaskOutputs(howToExecuteTask, null, bucket),
                    "The method GatherTaskOutputs() should never fail when inferring task outputs.");
                if (lookupHash != null)
                {
                    List<string> overrideMessages = bucket.Lookup.GetPropertyOverrideMessages(lookupHash);
                    if (overrideMessages != null)
                    {
                        foreach (string s in overrideMessages)
                        {
                            loggingServices.LogCommentFromText(buildEventContext, MessageImportance.Low, s);
                        }
                    }
                }
            }

            return taskExecutedSuccessfully;
        }

        /// <summary>
        /// Recomputes the task's "ContinueOnError" setting.
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="engineProxy"></param>
        private void UpdateContinueOnError(ItemBucket bucket, EngineProxy engineProxy)
        {
            XmlAttribute continueOnErrorAttribute = taskNode.Attributes[XMakeAttributes.continueOnError];

            try
            {
                continueOnError =
                (
                    // if attribute doesn't exist, default to "false"
                        (continueOnErrorAttribute != null)
                    &&
                    // otherwise, convert its value to a boolean
                        ConversionUtilities.ConvertStringToBool
                        (
                    // expand embedded item vectors after expanding properties and item metadata
                            bucket.Expander.ExpandAllIntoString(continueOnErrorAttribute)
                        )
                );
            }
            // handle errors in string-->bool conversion
            catch (ArgumentException e)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, continueOnErrorAttribute, "InvalidContinueOnErrorAttribute", TaskName, e.Message);
            }

            // We need to access an internal method of the EngineProxy in order to update the value
            // of continueOnError that will be returned to the task when the task queries IBuildEngine for it
            engineProxy.UpdateContinueOnError(continueOnError);
        }

        /// <summary>
        /// Tries to instantiate the task object
        /// </summary>
        /// <returns>task object, or null if it could not be instantiated</returns>
        private ITask InstantiateTask(AppDomain taskAppDomain)
        {
            ITask task = null;

            try
            {
                // instantiate the task in given domain
                if (TaskClass.Assembly.AssemblyFile != null)
                {
                    if (taskAppDomain == null)
                    {
                        taskAppDomain = AppDomain.CurrentDomain;
                    }

                    task = (ITask)taskAppDomain.CreateInstanceFromAndUnwrap(TaskClass.Assembly.AssemblyFile, TaskClass.Type.FullName);

                    Type taskType = null;
                    // this will force evaluation of the task class type and try to load the task assembly
                    taskType = task.GetType();

                    // If the types don't match, we have a problem. It means that our AppDomain was able to load
                    // a task assembly using Load, and loaded a different one. I don't see any other choice than
                    // to fail here.
                    if (taskType != TaskClass.Type)
                    {
                        loggingServices.LogError(buildEventContext, CreateBuildEventFileInfoForTask(),
                            "ConflictingTaskAssembly", TaskClass.Assembly.AssemblyFile, taskType.Assembly.Location);

                        task = null;
                    }
                }
                else
                {
                    if (taskAppDomain == null)
                    {
                        // perf improvement for the same appdomain case - we already have the type object
                        // and don't want to go through reflection to recreate it from the name.
                        task = (ITask)Activator.CreateInstance(TaskClass.Type);
                    }
                    else
                    {
                        task = (ITask)taskAppDomain.CreateInstanceAndUnwrap(TaskClass.Type.Assembly.FullName, TaskClass.Type.FullName);
                    }
                }
            }
            catch (InvalidCastException e)
            {
                loggingServices.LogError(buildEventContext, CreateBuildEventFileInfoForTask(),
                    "TaskInstantiationFailureErrorInvalidCast", TaskName, TaskClass.Assembly.ToString(), e.Message);
            }
            catch (TargetInvocationException e)
            {
                // Exception thrown by the called code itself
                // Log the stack, so the task vendor can fix their code
                loggingServices.LogError(buildEventContext, CreateBuildEventFileInfoForTask(),
                    "TaskInstantiationFailureError", TaskName, TaskClass.Assembly.ToString(), Environment.NewLine + e.InnerException.ToString());
            }
            catch (Exception e) // Catching Exception, but rethrowing unless it's a well-known exception.
            {
                if (ExceptionHandling.NotExpectedReflectionException(e))
                    throw;

                // Reflection related exception
                loggingServices.LogError(buildEventContext, CreateBuildEventFileInfoForTask(),
                    "TaskInstantiationFailureError", TaskName, TaskClass.Assembly.ToString(), e.Message);
            }

            return task;
        }

        /// <summary>
        /// Given an instantiated task, this method initializes it, and sets all the task parameters (which are defined as
        /// properties of the task class).
        /// </summary>
        /// <remarks>
        /// This method is internal for unit-testing purposes only.
        /// </remarks>
        /// <returns>true, if successful</returns>
        internal bool InitializeTask(ITask task, ItemBucket bucket, EngineProxy engineProxy)
        {
            try
            {
                task.BuildEngine = engineProxy;
                task.HostObject = hostObject;
            }
            // if a logger has failed, abort immediately
            catch (LoggerException)
            {
                // Polite logger failure
                throw;
            }
            catch (InternalLoggerException)
            {
                // Logger threw arbitrary exception
                throw;
            }
            // handle any exception thrown by the task during initialization
            catch (Exception e)
            {
                // NOTE: We catch ALL exceptions here, to attempt to completely isolate the Engine
                // from failures in the task. Probably we should try to avoid catching truly fatal exceptions,
                // e.g., StackOverflowException

                loggingServices.LogFatalTaskError(buildEventContext,
                     e,
                    // Display the task's exception stack.
                    // Log the task line number, whatever the value of ContinueOnError;
                    // because InitializeTask failure will be a hard error anyway.
                     CreateBuildEventFileInfoForTask(),
                     TaskName);

                return false;
            }

            bool taskInitialized = InitializeTaskParameters(task, bucket);

            return taskInitialized;
        }

        /// <summary>
        /// Sets all the task parameters, using the provided bucket's lookup.
        /// </summary>
        private bool InitializeTaskParameters(ITask task, ItemBucket bucket)
        {
            bool taskInitialized = true;

            // Get the properties that exist on this task.  We need to gather all of the ones that are marked
            // "required" so that we can keep track of whether or not they all get set.
            Dictionary<string, string> setParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> requiredParameters = GetNamesOfPropertiesWithRequiredAttribute();

            // look through all the attributes of the task element
            foreach (XmlAttribute taskAttribute in taskNode.Attributes)
            {
                // skip the known "special" task attributes
                if (!XMakeAttributes.IsSpecialTaskAttribute(taskAttribute.Name))
                {
                    bool taskParameterSet = false;  // Did we actually call the setter on this task parameter?

                    bool success = InitializeTaskParameter(task, taskAttribute, requiredParameters.ContainsKey(taskAttribute.Name), bucket, out taskParameterSet);

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
                        setParameters[taskAttribute.Name] = String.Empty;
                    }
                }
            }

            if (taskInitialized)
            {
                // See if any required properties were not set
                foreach (KeyValuePair<string, string> requiredParameter in requiredParameters)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(setParameters.ContainsKey(requiredParameter.Key),
                        taskNode,
                        "RequiredPropertyNotSetError",
                        TaskName, requiredParameter.Key);
                }
            }
            return taskInitialized;
        }


        /// <summary>
        /// Finds all the task properties that are required.
        /// Returns them as keys in a dictionary.
        /// </summary>
        private Dictionary<string, string> GetNamesOfPropertiesWithRequiredAttribute()
        {
            Dictionary<string, string> requiredParameters = null;

            try
            {
                requiredParameters = TaskClass.GetNamesOfPropertiesWithRequiredAttribute();
            }
            catch (Exception e) // Catching Exception, but rethrowing unless it's a well-known exception.
            {
                if (ExceptionHandling.NotExpectedReflectionException(e))
                    throw;

                // Reflection related exception
                loggingServices.LogError(buildEventContext, CreateBuildEventFileInfoForTask(), "AttributeTypeLoadError", TaskName, e.Message);

                ProjectErrorUtilities.VerifyThrowInvalidProject(false, taskNode, "TaskDeclarationOrUsageError", TaskName);
            }

            return requiredParameters;
        }

        /// <summary>
        /// Execute a task object for a given bucket.
        /// </summary>
        /// <param name="engineProxy"></param>
        /// <param name="bucket"></param>
        /// <param name="howToExecuteTask"></param>
        /// <param name="task"></param>
        /// <param name="taskResult">Whether the task returned true from Execute</param>
        /// <returns>true if task executed successfully (possibly failed but continueOnError=true)</returns>
        private bool ExecuteInstantiatedTask(EngineProxy engineProxy, ItemBucket bucket, TaskExecutionMode howToExecuteTask, ITask task, out bool taskResult)
        {
            UpdateContinueOnError(bucket, engineProxy);

            taskResult = false;
            bool taskExecutedSuccessfully = true;

            if (!InitializeTask(task, bucket, engineProxy))
            {
                // The task cannot be initialized.
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, taskNode, "TaskParametersError", TaskName, String.Empty);
            }
            else
            {
                bool taskReturned = false;

                try
                {
                    taskResult = task.Execute();
                    taskReturned = true;
                }
                // if a logger has failed, abort immediately
                catch (LoggerException)
                {
                    // Polite logger failure
                    throw;
                }
                catch (InternalLoggerException)
                {
                    // Logger threw arbitrary exception
                    throw;
                }
                // handle any exception thrown by the task during execution
                // NOTE: We catch ALL exceptions here, to attempt to completely isolate the Engine
                // from failures in the task. Probably we should try to avoid catching truly fatal exceptions,
                // e.g., StackOverflowException
                catch (Exception e)
                {
                    if (continueOnError)
                    {
                        loggingServices.LogTaskWarningFromException(buildEventContext, e,
                            // Don't try and log the line/column number for this error if
                            // ContinueOnError=true, because it's too expensive to do so, 
                            // and this error may be fairly common and expected.
                            new BuildEventFileInfo(projectFileOfTaskNode), TaskName);

                        // Log a message explaining why we converted the previous error into a warning.
                        loggingServices.LogComment(buildEventContext, MessageImportance.Normal, "ErrorConvertedIntoWarning");
                    }
                    else
                    {
                        loggingServices.LogFatalTaskError(buildEventContext, e,
                            CreateBuildEventFileInfoForTask(),
                            TaskName);
                    }
                }

                // If the task returned attempt to gather its outputs.  If gathering outputs fails set the taskResults
                // to false
                if (taskReturned)
                {
                    taskResult = GatherTaskOutputs(howToExecuteTask, task, bucket) && taskResult;
                }

                // If the taskResults are false look at ContinueOnError.  If ContinueOnError=false (default)
                // mark the taskExecutedSuccessfully=false.  Otherwise let the task succeed but log a normal
                // pri message that says this task is continuing because ContinueOnError=true
                if (!taskResult)
                {
                    if (!continueOnError)
                    {
                        taskExecutedSuccessfully = false;
                    }
                    else
                    {
                        loggingServices.LogComment(buildEventContext, MessageImportance.Normal,
                            "TaskContinuedDueToContinueOnError",
                             "ContinueOnError", TaskName, "true");
                    }
                }
            }

            return taskExecutedSuccessfully;
        }

        /// <summary>
        /// Gathers task outputs in two ways:
        /// 1) Given an instantiated task that has finished executing, it extracts the outputs using .NET reflection.
        /// 2) Otherwise, it parses the task's output specifications and (statically) infers the outputs.
        /// </summary>
        /// <param name="howToExecuteTask"></param>
        /// <param name="task"></param>
        /// <param name="bucket"></param>
        /// <returns>true, if successful</returns>
        private bool GatherTaskOutputs(TaskExecutionMode howToExecuteTask, ITask task, ItemBucket bucket)
        {
            bool gatheredTaskOutputsSuccessfully = true;

            foreach (TaskOutput taskOutputSpecification in
                        GetTaskOutputSpecifications(true))
            {
                // if the task's outputs are supposed to be gathered
                if (
                        (taskOutputSpecification.ConditionAttribute == null)
                        ||
                        Utilities.EvaluateCondition(taskOutputSpecification.ConditionAttribute.Value,
                            taskOutputSpecification.ConditionAttribute,
                            bucket.Expander, null, ParserOptions.AllowAll, loggingServices, buildEventContext)
                    )
                {
                    ErrorUtilities.VerifyThrow(taskOutputSpecification.TaskParameterAttribute != null,
                        "Invalid task output specification -- this should have been caught when the <Output> XML was parsed.");
                    ErrorUtilities.VerifyThrow(taskOutputSpecification.TaskParameterAttribute.Value.Length > 0,
                        "Invalid task output specification -- this should have been caught when the <Output> XML was parsed.");

                    // expand all embedded properties, item metadata and item vectors in the task parameter name
                    string taskParameterName = bucket.Expander.ExpandAllIntoString(taskOutputSpecification.TaskParameterAttribute);

                    ProjectErrorUtilities.VerifyThrowInvalidProject(taskParameterName.Length > 0, taskOutputSpecification.TaskParameterAttribute,
                        "InvalidEvaluatedAttributeValue", taskParameterName, taskOutputSpecification.TaskParameterAttribute.Value, XMakeAttributes.taskParameter, XMakeElements.output);

                    string itemName = null;
                    string propertyName = null;

                    // check where the outputs are going -- into a vector, or a property?
                    if (taskOutputSpecification.IsItemVector)
                    {
                        ErrorUtilities.VerifyThrow(taskOutputSpecification.ItemNameAttribute != null,
                            "How can it be an output item if the item name is null?  This should have been caught when the <Output> XML was parsed.");

                        ErrorUtilities.VerifyThrow(taskOutputSpecification.ItemNameAttribute.Value.Length > 0,
                            "Invalid task output specification -- this should have been caught when the <Output> XML was parsed.");

                        // expand all embedded properties, item metadata and item vectors in the item type name
                        itemName = bucket.Expander.ExpandAllIntoString(taskOutputSpecification.ItemNameAttribute);

                        ProjectErrorUtilities.VerifyThrowInvalidProject(itemName.Length > 0, taskOutputSpecification.ItemNameAttribute,
                            "InvalidEvaluatedAttributeValue", itemName, taskOutputSpecification.ItemNameAttribute.Value, XMakeAttributes.itemName, XMakeElements.output);
                    }
                    else
                    {
                        ErrorUtilities.VerifyThrow(taskOutputSpecification.IsProperty,
                            "Invalid task output specification -- this should have been caught when the <Output> XML was parsed.");

                        ErrorUtilities.VerifyThrow(taskOutputSpecification.PropertyNameAttribute != null,
                            "How can it be an output property if the property name is null?  This should have been caught when the <Output> XML was parsed.");

                        ErrorUtilities.VerifyThrow(taskOutputSpecification.PropertyNameAttribute.Value.Length > 0,
                            "Invalid task output specification -- this should have been caught when the <Output> XML was parsed.");

                        // expand all embedded properties, item metadata and item vectors in the property name
                        propertyName = bucket.Expander.ExpandAllIntoString(taskOutputSpecification.PropertyNameAttribute);

                        ProjectErrorUtilities.VerifyThrowInvalidProject(propertyName.Length > 0, taskOutputSpecification.PropertyNameAttribute,
                            "InvalidEvaluatedAttributeValue", propertyName, taskOutputSpecification.PropertyNameAttribute.Value, XMakeAttributes.propertyName, XMakeElements.output);
                    }

                    // if we're gathering outputs by .NET reflection
                    if (howToExecuteTask == TaskExecutionMode.ExecuteTaskAndGatherOutputs)
                    {
                        gatheredTaskOutputsSuccessfully = GatherGeneratedTaskOutputs(bucket.Lookup, taskOutputSpecification, taskParameterName, itemName, propertyName, task);
                    }
                    // if we're inferring outputs based on information in the task and <Output> tags
                    else
                    {
                        Debug.Assert(howToExecuteTask == TaskExecutionMode.InferOutputsOnly);

                        InferTaskOutputs(bucket.Lookup, taskOutputSpecification, taskParameterName, itemName, propertyName, bucket);
                    }
                }

                if (!gatheredTaskOutputsSuccessfully)
                {
                    break;
                }
            }

            return gatheredTaskOutputsSuccessfully;
        }

        /// <summary>
        /// Uses the given task output specification to grab the task's outputs using .NET reflection.
        /// </summary>
        /// <remarks>
        /// This method is "internal" for unit-testing purposes only.
        /// </remarks>
        /// <param name="taskOutputSpecification"></param>
        /// <param name="taskParameterName"></param>
        /// <param name="itemName">can be null</param>
        /// <param name="propertyName">can be null</param>
        /// <param name="task"></param>
        /// <returns>true, if successful</returns>
        internal bool GatherGeneratedTaskOutputs
        (
            Lookup lookup,
            TaskOutput taskOutputSpecification,
            string taskParameterName,
            string itemName,
            string propertyName,
            ITask task)
        {
            ErrorUtilities.VerifyThrow(task != null, "Need instantiated task to retrieve outputs from.");

            bool gatheredGeneratedOutputsSuccessfully = true;

            try
            {
                PropertyInfo parameter = TaskClass.GetProperty(taskParameterName);

                // flag an error if we find a parameter that has no .NET property equivalent
                ProjectErrorUtilities.VerifyThrowInvalidProject(parameter != null,
                    taskOutputSpecification.TaskParameterAttribute,
                    "UnexpectedTaskOutputAttribute", taskParameterName, TaskName);

                // output parameters must have their corresponding .NET properties marked with the Output attribute
                ProjectErrorUtilities.VerifyThrowInvalidProject(TaskClass.GetNamesOfPropertiesWithOutputAttribute().ContainsKey(taskParameterName),
                    taskOutputSpecification.TaskParameterAttribute,
                    "UnmarkedOutputTaskParameter", parameter.Name, TaskName);

                // grab the outputs from the task's designated output parameter (which is a .NET property)
                object outputs = parameter.GetValue(task, null);
                Type type = parameter.PropertyType;

                // don't use the C# "is" operator as it always returns false if the object is null
                if (
                    typeof(ITaskItem[]).IsAssignableFrom(type) ||   /* ITaskItem array or derived type, or */
                    typeof(ITaskItem).IsAssignableFrom(type)        /* ITaskItem or derived type */
                   )
                {
                    GatherTaskItemOutputs(lookup, taskOutputSpecification, itemName, propertyName, outputs);
                }
                // don't use the C# "is" operator as it always returns false if the object is null
                else if (
                    (type.IsArray && type.GetElementType().IsValueType) ||  /* array of value types, or */
                    (type == typeof(string[])) ||                           /* string array, or */
                    (type.IsValueType) ||                                   /* value type, or */
                    (type == typeof(string))                                /* string */
                    )
                {
                    GatherArrayStringAndValueOutputs(lookup, taskOutputSpecification, itemName, propertyName, parameter, outputs);
                }
                else
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, taskOutputSpecification.TaskParameterAttribute,
                        "UnsupportedTaskParameterTypeError", parameter.PropertyType, parameter.Name, TaskName);
                }
            }
            // handle invalid TaskItems in task outputs
            catch (InvalidOperationException e)
            {
                loggingServices.LogError(buildEventContext, Utilities.CreateBuildEventFileInfo(taskOutputSpecification.TaskParameterAttribute, projectFileOfTaskNode),
                    "InvalidTaskItemsInTaskOutputs", TaskName, taskParameterName, e.Message);

                gatheredGeneratedOutputsSuccessfully = false;
            }
            // handle any exception thrown by the task's getter
            catch (TargetInvocationException e)
            {
                // Exception thrown by the called code itself
                // Log the stack, so the task vendor can fix their code
                // Log the task line number, whatever the value of ContinueOnError;
                // because this will be a hard error anyway.
                loggingServices.LogFatalTaskError(buildEventContext, e.InnerException,
                    CreateBuildEventFileInfoForTask(),
                    TaskName);

                // We do not recover from a task exception while getting outputs,
                // so do not merely set gatheredGeneratedOutputsSuccessfully = false; here

                ProjectErrorUtilities.VerifyThrowInvalidProject(false, taskOutputSpecification.TaskParameterAttribute,
                    "FailedToRetrieveTaskOutputs", TaskName, taskParameterName, e.InnerException.Message);
            }
            catch (Exception e) // Catching Exception, but rethrowing unless it's a well-known exception.
            {
                if (ExceptionHandling.NotExpectedReflectionException(e))
                    throw;

                ProjectErrorUtilities.VerifyThrowInvalidProject(false, taskOutputSpecification.TaskParameterAttribute,
                    "FailedToRetrieveTaskOutputs", TaskName, taskParameterName, e.Message);
            }

            return gatheredGeneratedOutputsSuccessfully;
        }


        private void GatherArrayStringAndValueOutputs(Lookup lookup, TaskOutput taskOutputSpecification, string itemName, string propertyName, PropertyInfo parameter, object outputs)
        {
            // if the task has generated outputs (if it didn't, don't do anything)
            if (outputs != null)
            {
                Array convertibleOutputs = (parameter.PropertyType.IsArray)
                    ? (Array)outputs
                    : new object[] { outputs };

                if (taskOutputSpecification.IsItemVector)
                {
                    ErrorUtilities.VerifyThrow((itemName != null) && (itemName.Length > 0), "Need item type.");

                    // to store the outputs as items, use the string representations of the outputs as item-specs
                    foreach (object output in convertibleOutputs)
                    {
                        // if individual outputs in the array are null, ignore them
                        if (output != null)
                        {
                            string stringValueFromTask = (string)Convert.ChangeType(output, typeof(string), CultureInfo.InvariantCulture);

                            // attempting to put an empty string into an item is a no-op (bug #444501).
                            if (stringValueFromTask.Length > 0)
                            {
                                lookup.AddNewItem(new BuildItem(itemName, EscapingUtilities.Escape(stringValueFromTask)));
                            }
                        }
                    }
                }
                else
                {
                    Debug.Assert(taskOutputSpecification.IsProperty);
                    ErrorUtilities.VerifyThrow((propertyName != null) && (propertyName.Length > 0), "Need property name.");

                    // to store an object array in a property, join all the string representations of the objects with
                    // semi-colons to make the property value
                    StringBuilder joinedOutputs = new StringBuilder();

                    foreach (object output in convertibleOutputs)
                    {
                        // if individual outputs in the array are null, ignore them
                        if (output != null)
                        {
                            if (joinedOutputs.Length > 0)
                            {
                                joinedOutputs.Append(';');
                            }

                            string stringValueFromTask = (string)Convert.ChangeType(output, typeof(string), CultureInfo.InvariantCulture);
                            joinedOutputs.Append(EscapingUtilities.Escape(stringValueFromTask));
                        }
                    }

                    lookup.SetProperty(new BuildProperty(propertyName, joinedOutputs.ToString(), PropertyType.OutputProperty));
                }
            }
        }

        private void GatherTaskItemOutputs(Lookup lookup, TaskOutput taskOutputSpecification, string itemName, string propertyName, object outputs)
        {
            // if the task has generated outputs (if it didn't, don't do anything)
            if (outputs != null)
            {
                ITaskItem[] taskItemOutputs = (outputs is ITaskItem[])
                    ? (ITaskItem[])outputs
                    : new ITaskItem[] { (ITaskItem)outputs };

                if (taskOutputSpecification.IsItemVector)
                {
                    ErrorUtilities.VerifyThrow((itemName != null) && (itemName.Length > 0), "Need item type.");

                    foreach (ITaskItem output in taskItemOutputs)
                    {
                        // if individual items in the array are null, ignore them
                        if (output != null)
                        {
                            lookup.AddNewItem(new BuildItem(itemName, output));
                        }
                    }
                }
                else
                {
                    Debug.Assert(taskOutputSpecification.IsProperty);
                    ErrorUtilities.VerifyThrow((propertyName != null) && (propertyName.Length > 0), "Need property name.");

                    // to store an ITaskItem array in a property, join all the item-specs with semi-colons to make the
                    // property value, and ignore/discard the attributes on the ITaskItems
                    StringBuilder joinedOutputs = new StringBuilder();

                    foreach (ITaskItem output in taskItemOutputs)
                    {
                        // if individual items in the array are null, ignore them
                        if (output != null)
                        {
                            if (joinedOutputs.Length > 0)
                            {
                                joinedOutputs.Append(';');
                            }

                            joinedOutputs.Append(EscapingUtilities.Escape(output.ItemSpec));
                        }
                    }

                    lookup.SetProperty(new BuildProperty(propertyName, joinedOutputs.ToString(), PropertyType.OutputProperty));
                }
            }
        }

        /// <summary>
        /// Uses the given task output specification to (statically) infer the task's outputs.
        /// </summary>
        /// <param name="taskOutputSpecification"></param>
        /// <param name="taskParameterName"></param>
        /// <param name="itemName">can be null</param>
        /// <param name="propertyName">can be null</param>
        /// <param name="bucket"></param>
        private void InferTaskOutputs
        (
            Lookup lookup,
            TaskOutput taskOutputSpecification,
            string taskParameterName,
            string itemName,
            string propertyName,
            ItemBucket bucket
        )
        {
            // if the task has a value set for the output parameter, expand all embedded properties and item metadata in it
            XmlAttribute taskParameterAttribute = null;

            // Lookup attribute name needs to be case-insensitive
            // DevDiv bugs: 33981
            foreach (XmlAttribute taskNodeAttribute in taskNode.Attributes)
            {
                if (String.Compare(taskNodeAttribute.Name, taskParameterName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    taskParameterAttribute = taskNodeAttribute;
                    break;
                }
            }
 
            if (taskParameterAttribute != null)
            {
                if (taskOutputSpecification.IsItemVector)
                {
                    // This is an output item.

                    ErrorUtilities.VerifyThrow((itemName != null) && (itemName.Length > 0), "Need item type.");

                    // Expand only with properties first, so that expressions like Include="@(foo)" will transfer the metadata of the "foo" items as well, not just their item specs.
                    Expander propertyAndMetadataExpander = new Expander(bucket.Expander, ExpanderOptions.ExpandPropertiesAndMetadata);
                    List<string> outputItemSpecs = propertyAndMetadataExpander.ExpandAllIntoStringListLeaveEscaped(taskParameterAttribute);

                    foreach (string outputItemSpec in outputItemSpecs)
                    {
                        BuildItemGroup items = bucket.Expander.ExpandSingleItemListExpressionIntoItemsLeaveEscaped(outputItemSpec, taskParameterAttribute);

                        // if the output item-spec is an item vector, get the items in it
                        if (items != null)
                        {
                            foreach (BuildItem item in items)
                            {
                                // we want to preserve the attributes on the item
                                BuildItem clonedItem = item.VirtualClone();
                                // but we do need to change the item type
                                clonedItem.Name = itemName;

                                lookup.AddNewItem(clonedItem);
                            }
                        }
                        else
                        {
                            // if the output item-spec is not an item vector, accept it as-is
                            lookup.AddNewItem(new BuildItem(itemName, outputItemSpec));
                        }
                    }
                }
                else
                {
                    // This is an output property.

                    Debug.Assert(taskOutputSpecification.IsProperty);
                    ErrorUtilities.VerifyThrow((propertyName != null) && (propertyName.Length > 0), "Need property name.");

                    string taskParameterValue = bucket.Expander.ExpandAllIntoString(taskParameterAttribute);

                    if (taskParameterValue.Length > 0)
                    {
                        lookup.SetProperty(new BuildProperty(propertyName, taskParameterValue, PropertyType.OutputProperty));
                    }
                }
            }
        }

        /// <summary>
        /// Parses the task element for its output specifications, which are declared using &lt;Output&gt; tags.
        /// </summary>
        private List<TaskOutput> GetTaskOutputSpecifications(bool showWarnings)
        {
            List<TaskOutput> taskOutputSpecifications = new List<TaskOutput>();

            foreach (XmlNode childNode in taskNode.ChildNodes)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(XMakeElements.IsValidTaskChildNode(childNode), childNode,
                    "UnrecognizedChildElement", childNode.Name, TaskName);

                if (childNode.Name == XMakeElements.output)
                {
                    TaskOutput taskOutputSpecification = new TaskOutput((XmlElement)childNode);

                    // The "ItemName" attribute of the <Output> tag is usually just a straight 
                    // string representing the item name.  If it contains any "@" signs, the 
                    // project author most likely made a mistake, and so we throw a warning here.
                    XmlAttribute itemNameAttribute = taskOutputSpecification.ItemNameAttribute;
                    if (showWarnings && taskOutputSpecification.IsItemVector &&
                        (-1 != itemNameAttribute.Value.IndexOf('@')))
                    {
                        loggingServices.LogWarning(buildEventContext, Utilities.CreateBuildEventFileInfo(itemNameAttribute,
                            projectFileOfTaskNode), "AtSignInTaskOutputItemName", itemNameAttribute.Value);
                    }

                    // The "PropertyName" attribute of the <Output> tag is usually just a straight 
                    // string representing the property name.  If it contains any "$" signs, the 
                    // project author most likely made a mistake, and so we throw a warning here.
                    XmlAttribute propertyNameAttribute = taskOutputSpecification.PropertyNameAttribute;
                    if (showWarnings && taskOutputSpecification.IsProperty &&
                        (-1 != propertyNameAttribute.Value.IndexOf('$')))
                    {
                        loggingServices.LogWarning(buildEventContext, Utilities.CreateBuildEventFileInfo(propertyNameAttribute,
                            projectFileOfTaskNode), "DollarSignInTaskOutputPropertyName", propertyNameAttribute.Value);
                    }

                    taskOutputSpecifications.Add(taskOutputSpecification);
                }
            }

            return taskOutputSpecifications;
        }


        /// <summary>
        /// Given an instantiated task, this helper method sets the specified parameter based on its type.
        /// </summary>
        /// <returns>true, if successful</returns>
        private bool InitializeTaskParameter
        (
            ITask task,
            XmlAttribute taskParameterAttribute,
            bool isRequired,
            ItemBucket bucket,
            out bool taskParameterSet
        )
        {
            bool success = false;
            taskParameterSet = false;

            try
            {
                string parameterName = taskParameterAttribute.Name;
                string parameterValue = taskParameterAttribute.Value;

                try
                {
                    // check if the task has a .NET property corresponding to the parameter
                    PropertyInfo parameter = TaskClass.GetProperty(parameterName);

                    if (parameter != null)
                    {
                        ProjectErrorUtilities.VerifyThrowInvalidProject(parameter.CanWrite, taskParameterAttribute,
                            "SetAccessorNotAvailableOnTaskParameter", parameterName, TaskName);

                        Type parameterType = parameter.PropertyType;

                        // try to set the parameter
                        if (parameterType.IsValueType ||
                            (parameterType == typeof(string)) ||
                            (parameterType == typeof(ITaskItem)))
                        {
                            success = InitializeTaskScalarParameter(task, taskParameterAttribute,
                                parameter, parameterType, parameterValue, bucket, out taskParameterSet);
                        }
                        else if ((parameterType.IsArray && parameterType.GetElementType().IsValueType) ||
                            (parameterType == typeof(string[])) ||
                            (parameterType == typeof(ITaskItem[])))
                        {
                            success = InitializeTaskVectorParameter(task, taskParameterAttribute, isRequired,
                                parameter, parameterType, parameterValue, bucket, out taskParameterSet);
                        }
                        else
                        {
                            loggingServices.LogError(buildEventContext, Utilities.CreateBuildEventFileInfo(taskParameterAttribute, projectFileOfTaskNode),
                                "UnsupportedTaskParameterTypeError", parameterType, parameter.Name, TaskName);
                        }

                        if (!success)
                        {
                            // flag an error if the parameter could not be set
                            loggingServices.LogError(buildEventContext, Utilities.CreateBuildEventFileInfo(taskParameterAttribute, projectFileOfTaskNode),
                                "InvalidTaskAttributeError", parameterName, parameterValue, TaskName);
                        }
                    }
                    else
                    {
                        // flag an error if we find a parameter that has no .NET property equivalent
                        loggingServices.LogError(buildEventContext, Utilities.CreateBuildEventFileInfo(taskParameterAttribute, projectFileOfTaskNode),
                            "UnexpectedTaskAttribute", parameterName, TaskName);
                    }
                }
                catch (AmbiguousMatchException)
                {
                    loggingServices.LogError(buildEventContext, Utilities.CreateBuildEventFileInfo(taskParameterAttribute, projectFileOfTaskNode),
                        "AmbiguousTaskParameterError", TaskName, parameterName);
                }
            }
            catch (Exception e) // Catching Exception, but rethrowing unless it's a well-known exception.
            {
                if (ExceptionHandling.NotExpectedReflectionException(e))
                    throw;

                // Reflection related exception
                loggingServices.LogError(buildEventContext, CreateBuildEventFileInfoForTask(), "TaskParametersError", TaskName, e.Message);

                success = false;
            }

            return success;
        }

        /// <summary>
        /// Given an instantiated task, this helper method sets the specified scalar parameter based on its type.
        /// </summary>
        /// <remarks>This is "internal" only for the purpose of unit testing. Otherwise, it should be "private".</remarks>
        /// <returns>true, if successful</returns>
        internal bool InitializeTaskScalarParameter
        (
            ITask task,
            XmlAttribute taskParameterAttribute,
            PropertyInfo parameter,
            Type parameterType,
            string parameterValue,
            ItemBucket bucket,
            out bool taskParameterSet
        )
        {
            taskParameterSet = false;

            bool success = false;

            try
            {
                if (parameterType == typeof(ITaskItem))
                {
                    // We don't know how many items we're going to end up with, but we'll
                    // keep adding them to this arraylist as we find them.
                    List<TaskItem> finalTaskItems = bucket.Expander.ExpandAllIntoTaskItems(parameterValue, taskParameterAttribute);

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
                            ProjectErrorUtilities.VerifyThrowInvalidProject(false,
                                taskParameterAttribute,
                                "CannotPassMultipleItemsIntoScalarParameter", bucket.Expander.ExpandAllIntoString(parameterValue, taskParameterAttribute),
                                parameter.Name, parameterType, TaskName);
                        }

                        success = SetTaskParameter(task, parameter, (ITaskItem)finalTaskItems[0]);
                        taskParameterSet = true;
                    }
                }
                else
                {
                    // Expand out all the metadata, properties, and item vectors in the string.
                    string expandedParameterValue = bucket.Expander.ExpandAllIntoString(parameterValue, taskParameterAttribute);

                    if (expandedParameterValue.Length == 0)
                    {
                        success = true;
                    }
                    // Convert the string to the appropriate datatype, and set the task's parameter.
                    else if (parameterType == typeof(bool))
                    {
                        success = SetTaskParameter(task, parameter, ConversionUtilities.ConvertStringToBool(expandedParameterValue));
                        taskParameterSet = true;
                    }
                    else if (parameterType == typeof(string))
                    {
                        success = SetTaskParameter(task, parameter, expandedParameterValue);
                        taskParameterSet = true;
                    }
                    else
                    {
                        success = SetTaskParameter(task, parameter, Convert.ChangeType(expandedParameterValue, parameterType, CultureInfo.InvariantCulture));
                        taskParameterSet = true;
                    }
                }
            }
            // handle invalid type
            catch (InvalidCastException)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, taskParameterAttribute,
                    "InvalidTaskParameterValueError", bucket.Expander.ExpandAllIntoString(parameterValue, taskParameterAttribute), parameter.Name, parameterType, TaskName);
            }
            // handle argument exception (thrown by ConvertStringToBool)
            catch (ArgumentException)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, taskParameterAttribute,
                    "InvalidTaskParameterValueError", bucket.Expander.ExpandAllIntoString(parameterValue, taskParameterAttribute), parameter.Name, parameterType, TaskName);
            }
            // handle bad string representation of a type
            catch (FormatException)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, taskParameterAttribute,
                    "InvalidTaskParameterValueError", bucket.Expander.ExpandAllIntoString(parameterValue, taskParameterAttribute), parameter.Name, parameterType, TaskName);
            }
            // handle overflow when converting string representation of a numerical type
            catch (OverflowException)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, taskParameterAttribute,
                    "InvalidTaskParameterValueError", bucket.Expander.ExpandAllIntoString(parameterValue, taskParameterAttribute), parameter.Name, parameterType, TaskName);
            }

            return success;
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
        /// <returns>true, if successful</returns>
        internal bool InitializeTaskVectorParameter
        (
            ITask task,
            XmlAttribute taskParameterAttribute,
            bool isRequired,
            PropertyInfo parameter,
            Type parameterType,
            string parameterValue,
            ItemBucket bucket,
            out bool taskParameterSet
        )
        {
            ErrorUtilities.VerifyThrow(parameterValue != null,
                "Didn't expect null parameterValue in InitializeTaskVectorParameter");

            taskParameterSet = false;

            bool success = false;

            ArrayList finalTaskInputs = new ArrayList();

            List<TaskItem> finalTaskItems = bucket.Expander.ExpandAllIntoTaskItems(parameterValue, taskParameterAttribute);

            int i = 0;

            try
            {
                // If the task parameter is not a ITaskItem[], then we need to convert
                // all the TaskItem's in our arraylist to the appropriate datatype.
                if (parameterType != typeof(ITaskItem[]))
                {
                    // Loop through all the TaskItems in our arraylist, and convert them.
                    for (i = 0; i < finalTaskItems.Count; i++)
                    {
                        if (parameterType == typeof(string[]))
                        {
                            finalTaskInputs.Add(finalTaskItems[i].ItemSpec);
                        }
                        else if (parameterType == typeof(bool[]))
                        {
                            finalTaskInputs.Add(ConversionUtilities.ConvertStringToBool(finalTaskItems[i].ItemSpec));
                        }
                        else
                        {
                            finalTaskInputs.Add(Convert.ChangeType(finalTaskItems[i].ItemSpec,
                                parameterType.GetElementType(), CultureInfo.InvariantCulture));
                        }
                    }
                }
                else
                {
                    finalTaskInputs.AddRange(finalTaskItems);
                }

                // If there were no items, don't change the parameter's value.  EXCEPT if it's marked as a required 
                // parameter, in which case we made an explicit decision to pass in an empty array.  This is 
                // to avoid project authors having to add Conditions on all their tasks to avoid calling them
                // when a particular item list is empty.  This way, we just call the task with an empty list,
                // the task will loop over an empty list, and return quickly.
                if ((finalTaskInputs.Count > 0) || (isRequired))
                {
                    // Send the array into the task parameter.
                    success = SetTaskParameter(task, parameter, finalTaskInputs.ToArray(parameterType.GetElementType()));
                    taskParameterSet = true;
                }
                else
                {
                    success = true;
                }
            }
            // Handle invalid type.
            catch (InvalidCastException)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, taskParameterAttribute,
                    "InvalidTaskParameterValueError", finalTaskItems[i].ItemSpec, parameter.Name, parameterType, TaskName);
            }
            // Handle argument exception (thrown by ConvertStringToBool)
            catch (ArgumentException)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, taskParameterAttribute,
                    "InvalidTaskParameterValueError", finalTaskItems[i].ItemSpec, parameter.Name, parameterType, TaskName);
            }
            // Handle bad string representation of a type.
            catch (FormatException)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, taskParameterAttribute,
                    "InvalidTaskParameterValueError", finalTaskItems[i].ItemSpec, parameter.Name, parameterType, TaskName);
            }
            // Handle overflow when converting string representation of a numerical type.
            catch (OverflowException)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, taskParameterAttribute,
                    "InvalidTaskParameterValueError", finalTaskItems[i].ItemSpec, parameter.Name, parameterType, TaskName);
            }

            return success;
        }

        /// <summary>
        /// Given an instantiated task, this helper method sets the specified parameter.
        /// </summary>
        /// <returns>true, if successful</returns>
        private bool SetTaskParameter
        (
            ITask task,
            PropertyInfo parameter,
            object parameterValue
        )
        {
            bool success = false;

            try
            {
                parameter.SetValue(task, parameterValue, null);
                success = true;
            }
            // if a logger has failed, abort immediately
            catch (LoggerException)
            {
                // Polite logger failure
                throw;
            }
            catch (InternalLoggerException)
            {
                // Logger threw arbitrary exception
                throw;
            }
            // handle any exception thrown by the task's setter itself
            catch (TargetInvocationException e)
            {
                // At this point, the interesting stack is the internal exception.
                // Log the task line number, whatever the value of ContinueOnError;
                // because this will be a hard error anyway.

                // Exception thrown by the called code itself
                // Log the stack, so the task vendor can fix their code
                loggingServices.LogFatalTaskError(buildEventContext, e.InnerException,
                    CreateBuildEventFileInfoForTask(),
                    TaskName);
            }
            catch (Exception e) // Catching Exception, but rethrowing unless it's a well-known exception.
            {
                if (ExceptionHandling.NotExpectedReflectionException(e))
                    throw;

                loggingServices.LogFatalTaskError(buildEventContext, e,
                    CreateBuildEventFileInfoForTask(),
                    TaskName);
            }

            return success;
        }

        /// <summary>
        /// This function correctly computes the line/column number of the task node 
        /// in the project file (or .TARGETS file) that called it. The XmlNode available 
        /// to the task engine lacks this information so we call back into the build engine
        /// to obtain it.
        /// </summary>
        private BuildEventFileInfo CreateBuildEventFileInfoForTask()
        {
            int lineNumber = 0;
            int columnNumber = 0;
            parentModule.GetLineColumnOfXmlNode(handleId, out lineNumber, out columnNumber);
            return new BuildEventFileInfo(projectFileOfTaskNode, lineNumber, columnNumber);
        }

        #endregion

        #region Member data

        // the XML backing the task
        private XmlElement taskNode;
        // the .NET class that defines the task
        private LoadedType taskClass;
        // The optional host object for this task.
        private ITaskHost hostObject;
        // the logging services provider
        private EngineLoggingServices loggingServices;
        // the id for the proxy data
        private int handleId;
        // event contextual information where the event is fired from
        private BuildEventContext buildEventContext;
        // The node on which this task engine is running.
        private TaskExecutionModule parentModule;
        // indicates whether to ignore task execution failures
        private bool continueOnError;
        // the conditional expression that controls task execution
        private XmlAttribute conditionAttribute;
        // the project file that the task XML was defined in -- this file could be different from the file of this task's parent
        // project if the task was defined in an imported project file, or if the task only exists in-memory
        private string projectFileOfTaskNode;
        // Full name of the project file containing the task
        private string parentProjectFullFileName;
        // Hash to contain a list of properties from all of the batches
        private Hashtable lookupHash = null;

        private TaskEngineAssemblyResolver resolver;

        #endregion
    }
}
