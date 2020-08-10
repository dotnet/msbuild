// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.Diagnostics;
using ElementLocation = Microsoft.Build.Construction.ElementLocation;
#if FEATURE_APPDOMAIN
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Remoting;
#endif
using System.Reflection;
using Microsoft.Build.BackEnd.Logging;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The host allows task factories access to method to allow them to log message during the construction of the task factories.
    /// </summary>
    internal class TaskFactoryLoggingHost :
#if FEATURE_APPDOMAIN
        MarshalByRefObject,
#endif
        IBuildEngine
    {
        /// <summary>
        /// Location of the task node in the original file
        /// </summary>
        private ElementLocation _elementLocation;

        /// <summary>
        /// The task factory logging context
        /// </summary>
        private BuildLoggingContext _loggingContext;

        /// <summary>
        /// Is the system running in multi-process mode and requires events to be serializable
        /// </summary>
        private bool _isRunningWithMultipleNodes;

#if FEATURE_APPDOMAIN
        /// <summary>
        /// A client sponsor is a class
        /// which will respond to a lease renewal request and will
        /// increase the lease time allowing the object to stay in memory
        /// </summary>
        private ClientSponsor _sponsor;
#endif

        /// <summary>
        /// True if the task connected to this proxy is alive
        /// </summary>
        private bool _activeProxy;

        /// <summary>
        /// Constructor
        /// </summary>
        public TaskFactoryLoggingHost(bool isRunningWithMultipleNodes, ElementLocation elementLocation, BuildLoggingContext loggingContext)
        {
            ErrorUtilities.VerifyThrowArgumentNull(loggingContext, nameof(loggingContext));
            ErrorUtilities.VerifyThrowInternalNull(elementLocation, nameof(elementLocation));

            _activeProxy = true;
            _isRunningWithMultipleNodes = isRunningWithMultipleNodes;
            _loggingContext = loggingContext;
            _elementLocation = elementLocation;
        }

        /// <summary>
        /// Returns true in the multiproc case
        /// REVIEW: Should this mean the same thing in the distributed build case?  If we have
        /// a build which happens to be on a distributed cluster, but the build manager has only
        /// alotted a single machine to this build, is this true?  Because the build manager
        /// could later decide to add more nodes to this build.
        /// UNDONE: This means we are building with multiple processes. If we are building on
        /// one machine then I think the maxcpu-count is still 1. In my mind this means multiple nodes either distributed or on the same machine.
        /// </summary>
        public bool IsRunningMultipleNodes
        {
            get
            {
                VerifyActiveProxy();
                return _isRunningWithMultipleNodes;
            }
        }

        /// <summary>
        /// Reflects the value of the ContinueOnError attribute.
        /// </summary>
        public bool ContinueOnError
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// The line number this task is on
        /// </summary>
        public int LineNumberOfTaskNode
        {
            get
            {
                VerifyActiveProxy();
                return _elementLocation.Line;
            }
        }

        /// <summary>
        /// The column number this task is on
        /// </summary>
        public int ColumnNumberOfTaskNode
        {
            get
            {
                VerifyActiveProxy();
                return _elementLocation.Column;
            }
        }

        /// <summary>
        /// The project file this task is in.
        /// Typically this is an imported .targets file.
        /// Unfortunately the interface has shipped with a poor name, so we cannot change it.
        /// </summary>
        public string ProjectFileOfTaskNode
        {
            get
            {
                VerifyActiveProxy();
                return _elementLocation.File;
            }
        }

        /// <summary>
        /// Sets or retrieves the logging context
        /// </summary>
        internal BuildLoggingContext LoggingContext
        {
            [DebuggerStepThrough]
            get
            { return _loggingContext; }
        }

        #region IBuildEngine Members

        /// <summary>
        /// Logs an error event for the current task
        /// </summary>
        /// <param name="e">The event args</param>
        public void LogErrorEvent(Microsoft.Build.Framework.BuildErrorEventArgs e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e, nameof(e));
            VerifyActiveProxy();

            // If we are in building across process we need the events to be serializable. This method will 
            // check to see if we are building with multiple process and if the event is serializable. It will 
            // also log a warning if the event is not serializable and drop the logging message.
            if (IsRunningMultipleNodes && !IsEventSerializable(e))
            {
                return;
            }

            e.BuildEventContext = _loggingContext.BuildEventContext;
            _loggingContext.LoggingService.LogBuildEvent(e);
        }

        /// <summary>
        /// Logs a warning event for the current task
        /// </summary>
        /// <param name="e">The event args</param>
        public void LogWarningEvent(Microsoft.Build.Framework.BuildWarningEventArgs e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e, nameof(e));
            VerifyActiveProxy();

            // If we are in building across process we need the events to be serializable. This method will 
            // check to see if we are building with multiple process and if the event is serializable. It will 
            // also log a warning if the event is not serializable and drop the logging message.
            if (IsRunningMultipleNodes && !IsEventSerializable(e))
            {
                return;
            }

            e.BuildEventContext = _loggingContext.BuildEventContext;
            _loggingContext.LoggingService.LogBuildEvent(e);
        }

        /// <summary>
        /// Logs a message event for the current task
        /// </summary>
        /// <param name="e">The event args</param>
        public void LogMessageEvent(Microsoft.Build.Framework.BuildMessageEventArgs e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e, nameof(e));
            VerifyActiveProxy();

            // If we are in building across process we need the events to be serializable. This method will 
            // check to see if we are building with multiple process and if the event is serializable. It will 
            // also log a warning if the event is not serializable and drop the logging message.
            if (IsRunningMultipleNodes && !IsEventSerializable(e))
            {
                return;
            }

            e.BuildEventContext = _loggingContext.BuildEventContext;
            _loggingContext.LoggingService.LogBuildEvent(e);
        }

        /// <summary>
        /// Logs a custom event for the current task
        /// </summary>
        /// <param name="e">The event args</param>
        public void LogCustomEvent(Microsoft.Build.Framework.CustomBuildEventArgs e)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e, nameof(e));
            VerifyActiveProxy();

            // If we are in building across process we need the events to be serializable. This method will 
            // check to see if we are building with multiple process and if the event is serializable. It will 
            // also log a warning if the event is not serializable and drop the logging message.
            if (IsRunningMultipleNodes && !IsEventSerializable(e))
            {
                return;
            }

            e.BuildEventContext = _loggingContext.BuildEventContext;
            _loggingContext.LoggingService.LogBuildEvent(e);
        }

        /// <summary>
        /// Builds a single project file
        /// </summary>
        /// <param name="projectFileName">The project file name</param>
        /// <param name="targetNames">The set of targets to build.</param>
        /// <param name="globalProperties">The global properties to use</param>
        /// <param name="targetOutputs">The outputs from the targets</param>
        /// <returns>True on success, false otherwise.</returns>
        public bool BuildProjectFile(string projectFileName, string[] targetNames, System.Collections.IDictionary globalProperties, System.Collections.IDictionary targetOutputs)
        {
            throw new NotImplementedException();
        }

        #endregion

#if FEATURE_APPDOMAIN
        /// <summary>
        /// InitializeLifetimeService is called when the remote object is activated.
        /// This method will determine how long the lifetime for the object will be.
        /// </summary>
        /// <returns>The lease object to control this object's lifetime.</returns>
        public override object InitializeLifetimeService()
        {
            VerifyActiveProxy();

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
                if (int.TryParse(initialLeaseTimeFromEnvironment, out leaseTimeFromEnvironment) && leaseTimeFromEnvironment > 0)
                {
                    initialLeaseTime = leaseTimeFromEnvironment;
                }
            }

            lease.InitialLeaseTime = TimeSpan.FromMinutes(initialLeaseTime);

            // Make a new client sponsor. A client sponsor is a class
            // which will respond to a lease renewal request and will
            // increase the lease time allowing the object to stay in memory
            _sponsor = new ClientSponsor();

            // When a new lease is requested lets make it last 1 minutes longer. 
            int leaseExtensionTime = 1;

            string leaseExtensionTimeFromEnvironment = Environment.GetEnvironmentVariable("MSBUILDENGINEPROXYLEASEEXTENSIONTIME");
            if (!String.IsNullOrEmpty(leaseExtensionTimeFromEnvironment))
            {
                int leaseExtensionFromEnvironment;
                if (int.TryParse(leaseExtensionTimeFromEnvironment, out leaseExtensionFromEnvironment) && leaseExtensionFromEnvironment > 0)
                {
                    leaseExtensionTime = leaseExtensionFromEnvironment;
                }
            }

            _sponsor.RenewalTime = TimeSpan.FromMinutes(leaseExtensionTime);

            // Register the sponsor which will increase lease timeouts when the lease expires
            lease.Register(_sponsor);

            return lease;
        }

        /// <summary>
        /// Indicates to the TaskHost that it is no longer needed.
        /// Called by TaskBuilder when the task using the EngineProxy is done.
        /// </summary>
        internal void MarkAsInactive()
        {
            VerifyActiveProxy();
            _activeProxy = false;

            _loggingContext = null;
            _elementLocation = null;

            // Clear out the sponsor (who is responsible for keeping the EngineProxy remoting lease alive until the task is done)
            // this will be null if the engineproxy was never sent across an appdomain boundry.
            if (_sponsor != null)
            {
                ILease lease = (ILease)RemotingServices.GetLifetimeService(this);

                lease?.Unregister(_sponsor);

                _sponsor.Close();
                _sponsor = null;
            }
        }
#endif

        /// <summary>
        /// Determine if the event is serializable. If we are running with multiple nodes we need to make sure the logging events are serializable. If not
        /// we need to log a warning.
        /// </summary>
        internal bool IsEventSerializable(BuildEventArgs e)
        {
            if (!e.GetType().GetTypeInfo().IsSerializable)
            {
                _loggingContext.LogWarning(null, new BuildEventFileInfo(string.Empty), "ExpectedEventToBeSerializable", e.GetType().Name);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Verify the task host is active or not
        /// </summary>
        private void VerifyActiveProxy()
        {
            ErrorUtilities.VerifyThrow(_activeProxy, "Attempted to use an inactive task factory logging host.");
        }
    }
}
