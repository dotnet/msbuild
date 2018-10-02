// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Permissions;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;
using System.Globalization;
using System.Threading;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Remoting;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class serves as a surrogate for the build engine. It limits access to the build engine by implementing only a subset
    /// of all public methods on the Engine class.
    /// </summary>
    internal sealed class EngineProxy : MarshalByRefObject, IBuildEngine3
    {
        #region Data
        // The logging interface
        private EngineLoggingServices loggingServices;

        // We've already computed and cached the line/column number of the task node in the project file.
        private bool haveProjectFileLocation = false;

        // The line number of the task node in the calling project file.
        private int lineNumber;

        // The column number of the task node in the calling project file.
        private int columnNumber;

        /// <summary>
        /// The full path to the project that's currently building.
        /// </summary>
        private string parentProjectFullFileName;

        /// <summary>
        /// The project file that contains the XML for task. This may be an import file and not the primary
        /// project file
        /// </summary>
        private string projectFileOfTaskNode;

        /// <summary>
        /// The token identifing the context of this evaluation 
        /// </summary>
        private int handleId;

        /// <summary>
        /// Continue on error value per batch exposed via IBuildEngine
        /// </summary>
        private bool continueOnError;

        /// <summary>
        /// The module within which this class has been created. Used for all callbacks to 
        /// engine.
        /// </summary>
        private TaskExecutionModule parentModule;

        /// <summary>
        /// Event contextual information, this tells the loggers where the task events were fired from
        /// </summary>
        private BuildEventContext buildEventContext;

        /// <summary>
        /// True if the task connected to this proxy is alive
        /// </summary>
        private bool activeProxy;

        /// <summary>
        /// This reference type is used to block access to a single entry methods of the interface
        /// </summary>
        private object callbackMonitor;

        /// <summary>
        /// A client sponsor is a class
        /// which will respond to a lease renewal request and will
        /// increase the lease time allowing the object to stay in memory
        /// </summary>
        private ClientSponsor sponsor;

        /// <summary>
        /// Will hold cached copy of typeof(BuildErrorEventArgs) used by each call to LogError
        /// </summary>
        private static Type buildErrorEventArgsType = null;

        /// <summary>
        /// Will hold cached copy of typeof(BuildErrorEventArgs) used by each call to LogError
        /// </summary>
        private static Type buildWarningEventArgsType = null;

        #endregion

        /// <summary>
        /// Private default constructor disallows parameterless instantiation.
        /// </summary>
        private EngineProxy()
        {
            // do nothing
        }
     
        /// <summary>
        /// Create an instance of this class to represent the IBuildEngine2 interface to the task
        /// including the event location where the log messages are raised
        /// </summary>
        /// <param name="parentModule">Parent Task Execution Module</param>
        /// <param name="handleId"></param>
        /// <param name="parentProjectFullFileName">the full path to the currently building project</param>
        /// <param name="projectFileOfTaskNode">the path to the actual file (project or targets) where the task invocation is located</param>
        /// <param name="loggingServices"></param>
        /// <param name="buildEventContext">Event Context where events will be seen to be raised from. Task messages will get this as their event context</param>
        internal EngineProxy
        (
            TaskExecutionModule parentModule, 
            int handleId, 
            string parentProjectFullFileName,
            string projectFileOfTaskNode, 
            EngineLoggingServices loggingServices,
            BuildEventContext buildEventContext
        )
        {
            ErrorUtilities.VerifyThrow(parentModule != null, "No parent module.");
            ErrorUtilities.VerifyThrow(loggingServices != null, "No logging services.");
            ErrorUtilities.VerifyThrow(projectFileOfTaskNode != null, "Need project file path string");

            this.parentModule = parentModule;
            this.handleId = handleId;
            this.parentProjectFullFileName = parentProjectFullFileName;
            this.projectFileOfTaskNode = projectFileOfTaskNode;
            this.loggingServices = loggingServices;
            this.buildEventContext = buildEventContext;
            this.callbackMonitor = new object();

            activeProxy = true;
        }

        /// <summary>
        /// Stub implementation -- forwards to engine being proxied.
        /// </summary>
        public void LogErrorEvent(BuildErrorEventArgs e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e, "e");
            ErrorUtilities.VerifyThrowInvalidOperation(activeProxy == true, "AttemptingToLogFromInactiveTask");

            if (parentModule.IsRunningMultipleNodes && !e.GetType().IsSerializable)
            {
                loggingServices.LogWarning(buildEventContext, new BuildEventFileInfo(string.Empty), "ExpectedEventToBeSerializable", e.GetType().Name);
                return;
            }

            string message = GetUpdatedMessage(e.File, e.Message, parentProjectFullFileName);

            if (ContinueOnError)
            {
                // Convert the error into a warning.  We do this because the whole point of 
                // ContinueOnError is that a project author expects that the task might fail,
                // but wants to ignore the failures.  This implies that we shouldn't be logging
                // errors either, because you should never have a successful build with errors.
                BuildWarningEventArgs warningEvent = new BuildWarningEventArgs
                        (   e.Subcategory,
                            e.Code,
                            e.File,
                            e.LineNumber,
                            e.ColumnNumber,
                            e.EndLineNumber,
                            e.EndColumnNumber,
                            message,  // this is the new message from above
                            e.HelpKeyword,
                            e.SenderName);

                warningEvent.BuildEventContext = buildEventContext;
                loggingServices.LogWarningEvent(warningEvent);

                // Log a message explaining why we converted the previous error into a warning.
                loggingServices.LogComment(buildEventContext,MessageImportance.Normal, "ErrorConvertedIntoWarning");
            }
            else
            {
                if(e.GetType().Equals(BuildErrorEventArgsType))
                {
                    // We'd like to add the project file to the subcategory, but since this property
                    // is read-only on the BuildErrorEventArgs type, this requires creating a new
                    // instance.  However, if some task logged a custom error type, we don't want to
                    // impolitely (as we already do above on ContinueOnError) throw the custom type
                    // data away.
                    e = new BuildErrorEventArgs
                        (
                            e.Subcategory,
                            e.Code, 
                            e.File, 
                            e.LineNumber, 
                            e.ColumnNumber, 
                            e.EndLineNumber, 
                            e.EndColumnNumber, 
                            message,  // this is the new message from above
                            e.HelpKeyword, 
                            e.SenderName
                        );
                }

                e.BuildEventContext = buildEventContext;
                loggingServices.LogErrorEvent(e);
            }
        }

        /// <summary>
        /// Stub implementation -- forwards to engine being proxied.
        /// </summary>
        public void LogWarningEvent(BuildWarningEventArgs e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e, "e");
            ErrorUtilities.VerifyThrowInvalidOperation(activeProxy == true, "AttemptingToLogFromInactiveTask");

            if (parentModule.IsRunningMultipleNodes && !e.GetType().IsSerializable)
            {
                loggingServices.LogWarning(buildEventContext, new BuildEventFileInfo(string.Empty), "ExpectedEventToBeSerializable", e.GetType().Name);
                return;
            }

            if (e.GetType().Equals(BuildWarningEventArgsType))
            {
                // We'd like to add the project file to the message, but since this property
                // is read-only on the BuildWarningEventArgs type, this requires creating a new
                // instance.  However, if some task logged a custom warning type, we don't want 
                // to impolitely throw the custom type data away.

                string message = GetUpdatedMessage(e.File, e.Message, parentProjectFullFileName);

                e = new BuildWarningEventArgs
                (
                    e.Subcategory,
                    e.Code,
                    e.File,
                    e.LineNumber,
                    e.ColumnNumber,
                    e.EndLineNumber,
                    e.EndColumnNumber,
                    message, // this is the new message from above
                    e.HelpKeyword,
                    e.SenderName
                );
            }

            e.BuildEventContext = buildEventContext;
            loggingServices.LogWarningEvent(e);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="file">File field from the original BuildEventArgs</param>
        /// <param name="message">Message field from the original BuildEventArgs</param>
        /// <param name="parentProjectFullFileName">Full file name of the parent (building) project.</param>
        /// <returns></returns>
        private static string GetUpdatedMessage(string file, string message, string parentProjectFullFileName)
        {
#if BUILDING_DF_LKG
            // In the dogfood LKG, add the project path to the end, because we need it to help diagnose builds.

            // Don't bother doing anything if we don't have a project path (e.g., we loaded from XML directly)
            if (String.IsNullOrEmpty(parentProjectFullFileName))
            {
                return message;
            }

            // Don't bother adding the project file path if it's already in the file part
            if(String.Equals(file, parentProjectFullFileName, StringComparison.OrdinalIgnoreCase))
            {
                return message;
            }

            string updatedMessage = String.IsNullOrEmpty(message) ?
                                        String.Format(CultureInfo.InvariantCulture, "[{0}]", parentProjectFullFileName) :
                                        String.Format(CultureInfo.InvariantCulture, "{0} [{1}]", message, parentProjectFullFileName);

            return updatedMessage;
#else
            // In the regular product, don't modify the message. We want to do this properly, with a field on the event args, in a future version.
            return message;
#endif
        }

        /// <summary>
        /// Stub implementation -- forwards to engine being proxied.
        /// </summary>
        public void LogMessageEvent(BuildMessageEventArgs e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e, "e");
            ErrorUtilities.VerifyThrowInvalidOperation(activeProxy == true, "AttemptingToLogFromInactiveTask");

            if (parentModule.IsRunningMultipleNodes && !e.GetType().IsSerializable)
            {
                loggingServices.LogWarning(buildEventContext, new BuildEventFileInfo(string.Empty), "ExpectedEventToBeSerializable", e.GetType().Name);
                    return;
            }
            e.BuildEventContext = buildEventContext;
            loggingServices.LogMessageEvent(e);
        }

        /// <summary>
        /// Stub implementation -- forwards to engine being proxied.
        /// </summary>
        public void LogCustomEvent(CustomBuildEventArgs e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e, "e");
            ErrorUtilities.VerifyThrowInvalidOperation(activeProxy == true, "AttemptingToLogFromInactiveTask");

            if (parentModule.IsRunningMultipleNodes && !e.GetType().IsSerializable)
            {
                loggingServices.LogWarning(buildEventContext, new BuildEventFileInfo(string.Empty), "ExpectedEventToBeSerializable", e.GetType().Name);
                    return;
            }

            e.BuildEventContext = buildEventContext;
            loggingServices.LogCustomEvent(e);
        }

        /// <summary>
        /// Returns true if the ContinueOnError flag was set to true for this particular task
        /// in the project file.
        /// </summary>
        public bool ContinueOnError
        {
            get
            {
                ErrorUtilities.VerifyThrowInvalidOperation(activeProxy == true, "AttemptingToLogFromInactiveTask");

                return this.continueOnError;
            }
        }

        /// <summary>
        /// Called by the task engine to update the value for each batch
        /// </summary>
        /// <param name="shouldContinueOnError"></param>
        internal void UpdateContinueOnError(bool shouldContinueOnError)
        {
            this.continueOnError = shouldContinueOnError;
        }

        /// <summary>
        /// Retrieves the line number of the task node withing the project file that called it.
        /// </summary>
        /// <remarks>This method is expensive in terms of perf.  Do not call it in mainline scenarios.</remarks>
        /// <owner>RGoel</owner>
        public int LineNumberOfTaskNode
        {
            get
            {
                ErrorUtilities.VerifyThrowInvalidOperation(activeProxy == true, "AttemptingToLogFromInactiveTask");

                ComputeProjectFileLocationOfTaskNode();
                return this.lineNumber;
            }
        }

        /// <summary>
        /// Retrieves the line number of the task node withing the project file that called it.
        /// </summary>
        /// <remarks>This method is expensive in terms of perf.  Do not call it in mainline scenarios.</remarks>
        /// <owner>RGoel</owner>
        public int ColumnNumberOfTaskNode
        {
            get
            {
                ErrorUtilities.VerifyThrowInvalidOperation(activeProxy == true, "AttemptingToLogFromInactiveTask");

                ComputeProjectFileLocationOfTaskNode();
                return this.columnNumber;
            }
        }

        /// <summary>
        /// Returns the full path to the project file that contained the call to this task.
        /// </summary>
        public string ProjectFileOfTaskNode
        {
            get
            {
                ErrorUtilities.VerifyThrowInvalidOperation(activeProxy == true, "AttemptingToLogFromInactiveTask");

                return projectFileOfTaskNode;
            }
        }

        /// <summary>
        /// Computes the line/column number of the task node in the project file (or .TARGETS file)
        /// that called it.
        /// </summary>
        private void ComputeProjectFileLocationOfTaskNode()
        {
            if (!haveProjectFileLocation)
            {
                parentModule.GetLineColumnOfXmlNode(handleId, out this.lineNumber, out this.columnNumber);
                haveProjectFileLocation = true;
            }
        }

        /// <summary>
        /// Stub implementation -- forwards to engine being proxied.
        /// </summary>
        /// <param name="projectFileName"></param>
        /// <param name="targetNames"></param>
        /// <param name="globalProperties"></param>
        /// <param name="targetOutputs"></param>
        /// <returns>result of call to engine</returns>
        public bool BuildProjectFile
            (
            string projectFileName,
            string[] targetNames,
            IDictionary globalProperties,
            IDictionary targetOutputs
            )
        {
            return BuildProjectFile(projectFileName, targetNames, globalProperties, targetOutputs, null);
        }

        /// <summary>
        /// Stub implementation -- forwards to engine being proxied.
        /// </summary>
        /// <param name="projectFileName"></param>
        /// <param name="targetNames"></param>
        /// <param name="globalProperties"></param>
        /// <param name="targetOutputs"></param>
        /// <param name="toolsVersion">Tools Version to override on the project. May be null</param>
        /// <returns>result of call to engine</returns>
        public bool BuildProjectFile
            (
            string projectFileName, 
            string[] targetNames, 
            IDictionary globalProperties, 
            IDictionary targetOutputs,
            string toolsVersion
            )
        {
            lock (callbackMonitor)
            {
                ErrorUtilities.VerifyThrowInvalidOperation(activeProxy == true, "AttemptingToLogFromInactiveTask");

                // Wrap the project name into an array
                string[] projectFileNames = new string[1];
                projectFileNames[0] = projectFileName;
                string[] toolsVersions = new string[1];
                toolsVersions[0] = toolsVersion;
                IDictionary[] targetOutputsPerProject = new IDictionary[1];
                targetOutputsPerProject[0] = targetOutputs;
                IDictionary[] globalPropertiesPerProject = new IDictionary[1];
                globalPropertiesPerProject[0] = globalProperties;
                return parentModule.BuildProjectFile(handleId, projectFileNames, targetNames, globalPropertiesPerProject, targetOutputsPerProject,
                                                     loggingServices, toolsVersions, false, false, buildEventContext);
            }
        }

        /// <summary>
        /// Stub implementation -- forwards to engine being proxied.
        /// </summary>
        /// <param name="projectFileNames"></param>
        /// <param name="targetNames"></param>
        /// <param name="globalProperties"></param>
        /// <param name="targetOutputsPerProject"></param>
        /// <param name="toolsVersions">Tools Version to overrides per project. May contain null values</param>
        /// <param name="unloadProjectsOnCompletion"></param>
        /// <returns>result of call to engine</returns>
        public bool BuildProjectFilesInParallel
            (
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IDictionary[] targetOutputsPerProject,
            string[] toolsVersions,
            bool useResultsCache,
            bool unloadProjectsOnCompletion
            )
        {
            lock (callbackMonitor)
            {
                return parentModule.BuildProjectFile(handleId, projectFileNames, targetNames, globalProperties,
                                                         targetOutputsPerProject, loggingServices,
                                                         toolsVersions, useResultsCache, unloadProjectsOnCompletion, buildEventContext);
            }
        }

        /// <summary>
        /// Not implemented for the proxy 
        /// </summary>
	public void Yield()
	{
	}

        /// <summary>
        /// Not implemented for the proxy
        /// </summary>
	public void Reacquire()
	{
	}

        /// <summary>
        /// Stub implementation -- forwards to engine being proxied.
        /// </summary>
        /// <remarks>
        /// 1) it is acceptable to pass null for both <c>targetNames</c> and <c>targetOutputs</c>
        /// 2) if no targets are specified, the default targets are built
        /// 
        /// </remarks>
        /// <param name="projectFileNames">The project to build.</param>
        /// <param name="targetNames">The targets in the project to build (can be null).</param>
        /// <param name="globalProperties">An array of hashtables of additional global properties to apply
        ///     to the child project (array entries can be null). 
        ///     The key and value in the hashtable should both be strings.</param>
        /// <param name="removeGlobalProperties">A list of global properties which should be removed.</param>
        /// <param name="toolsVersions">A tools version recognized by the Engine that will be used during this build (can be null).</param>
        /// <param name="returnTargetOutputs">Should the target outputs be returned in the BuildEngineResults</param>
        /// <returns>Returns a structure containing the success or failures of the build and the target outputs by project.</returns>
        public BuildEngineResult BuildProjectFilesInParallel
            (
            string[] projectFileNames,
            string[] targetNames,
            IDictionary [] globalProperties,
            IList<string>[] removeGlobalProperties,
            string[] toolsVersions,
            bool returnTargetOutputs
            )
        {
            lock (callbackMonitor)
            {
                ErrorUtilities.VerifyThrowInvalidOperation(activeProxy == true, "AttemptingToLogFromInactiveTask");

                ErrorUtilities.VerifyThrowArgumentNull(projectFileNames, "projectFileNames");
                ErrorUtilities.VerifyThrowArgumentNull(globalProperties, "globalPropertiesPerProject");
                
                Dictionary<string, ITaskItem[]>[] targetOutputsPerProject = null;

                if (returnTargetOutputs)
                {
                    targetOutputsPerProject = new Dictionary<string, ITaskItem[]>[projectFileNames.Length];
                    for (int i = 0; i < targetOutputsPerProject.Length; i++)
                    {
                        targetOutputsPerProject[i] = new Dictionary<string, ITaskItem[]>(StringComparer.OrdinalIgnoreCase);
                    }
                }

                bool result = parentModule.BuildProjectFile(handleId, projectFileNames, targetNames, globalProperties,
                                                     targetOutputsPerProject, loggingServices,
                                                     toolsVersions, false, false, buildEventContext);

                return new BuildEngineResult(result, new List<IDictionary<string, ITaskItem[]>>(targetOutputsPerProject));
            }
        }

        /// <summary>
        /// InitializeLifetimeService is called when the remote object is activated. 
        /// This method will determine how long the lifetime for the object will be.
        /// </summary>
        public override object InitializeLifetimeService()
        {
            // Each MarshalByRef object has a reference to the service which
            // controls how long the remote object will stay around
            ILease lease = (ILease)base.InitializeLifetimeService();

            // Set how long a lease should be initially. Once a lease expires
            // the remote object will be disconnected and it will be marked as being availiable 
            // for garbage collection
            int initialLeaseTime = 1;

            string initialLeaseTimeFromEnvironment = Environment.GetEnvironmentVariable("MSBUILDENGINEPROXYINITIALLEASETIME");

            if (!String.IsNullOrEmpty(initialLeaseTimeFromEnvironment))
            {
                int leaseTimeFromEnvironment;
                if (int.TryParse(initialLeaseTimeFromEnvironment , out leaseTimeFromEnvironment) && leaseTimeFromEnvironment > 0)
                {
                      initialLeaseTime = leaseTimeFromEnvironment;        
                }
            }

            lease.InitialLeaseTime = TimeSpan.FromMinutes(initialLeaseTime);

            // Make a new client sponsor. A client sponsor is a class
            // which will respond to a lease renewal request and will
            // increase the lease time allowing the object to stay in memory
            sponsor = new ClientSponsor();

            // When a new lease is requested lets make it last 1 minutes longer. 
            int leaseExtensionTime = 1;

            string leaseExtensionTimeFromEnvironment = Environment.GetEnvironmentVariable("MSBUILDENGINEPROXYLEASEEXTENSIONTIME");
            if (!String.IsNullOrEmpty(leaseExtensionTimeFromEnvironment))
            {
                int leaseExtensionFromEnvironment;
                if (int.TryParse(leaseExtensionTimeFromEnvironment , out leaseExtensionFromEnvironment) && leaseExtensionFromEnvironment > 0)
                {
                      leaseExtensionTime = leaseExtensionFromEnvironment;        
                }
            }

            sponsor.RenewalTime = TimeSpan.FromMinutes(leaseExtensionTime);

            // Register the sponsor which will increase lease timeouts when the lease expires
            lease.Register(sponsor);

            return lease;
        }


        /// <summary>
        /// Indicates to the EngineProxy that it is no longer needed.
        /// Called by TaskEngine when the task using the EngineProxy is done.
        /// </summary>
        internal void MarkAsInActive()
        {
            activeProxy = false;

            // Since the task has a pointer to this class it may store it in a static field. Null out
            // internal data so the leak of this object doesn't lead to a major memory leak.
            loggingServices = null;
            parentModule = null;
            buildEventContext = null;
            
            // Clear out the sponsor (who is responsible for keeping the EngineProxy remoting lease alive until the task is done)
            // this will be null if the engineproxy was never sent accross an appdomain boundry.
            if (sponsor != null)
            {
                ILease lease = (ILease)RemotingServices.GetLifetimeService(this);
             
                if (lease != null)
                {
                    lease.Unregister(sponsor);
                }
                
                sponsor.Close();
                sponsor = null;
            }
        }
	
        #region Properties
        /// <summary>
        /// Provide a way to change the BuildEventContext of the engine proxy. This is important in batching where each batch will need its own buildEventContext.
        /// </summary>
        internal BuildEventContext BuildEventContext
        {
            get { return buildEventContext; }
            set { buildEventContext = value; }
        }

        /// <summary>
        /// This property allows a task to query whether or not the system is running in single process mode or multi process mode.
        /// Single process mode is where the engine is initialized with the number of cpus = 1 and the engine is not a child engine.
        /// The engine is in multi process mode when the engine is initialized with a number of cpus > 1 or the engine is a child engine.
        /// </summary>
        public bool IsRunningMultipleNodes
        {
            get { return parentModule.IsRunningMultipleNodes; }
        }

        /// <summary>
        /// Cached copy of typeof(BuildErrorEventArgs) used during each call to LogError
        /// </summary>
        private static Type BuildErrorEventArgsType
        {
            get
            {
                if (buildErrorEventArgsType == null)
                {
                    buildErrorEventArgsType = typeof(BuildErrorEventArgs);
                }
                return buildErrorEventArgsType;
            }
        }

        /// <summary>
        /// Cached copy of typeof(BuildWarningEventArgs) used during each call to LogWarning
        /// </summary>
        private static Type BuildWarningEventArgsType
        {
            get
            {
                if (buildWarningEventArgsType == null)
                {
                    buildWarningEventArgsType = typeof(BuildWarningEventArgs);
                }
                return buildWarningEventArgsType;
            }
        }

        #endregion
    }
}
