using System;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;

using Microsoft.Build.Shared;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;

using ILoggingService = Microsoft.Build.BackEnd.Logging.ILoggingService;
using LegacyThreadingData = Microsoft.Build.Execution.LegacyThreadingData;

namespace Microsoft.Build.UnitTests.QA
{
    #region delegate

    /// <summary>
    /// Delegate the call to GetComponent if one is available
    /// </summary>
    internal delegate IBuildComponent GetComponentDelegate(BuildComponentType type);

    #endregion

    /// <summary>
    /// The mock component host object.
    /// </summary>
    internal class QAMockHost : MockLoggingService, IBuildComponentHost, IBuildComponent
    {
        #region IBuildComponentHost Members

        /// <summary>
        /// The logging service
        /// </summary>
        private ILoggingService loggingService = null;

        /// <summary>
        /// The request engine
        /// </summary>
        private IBuildRequestEngine requestEngine = null;

        /// <summary>
        /// The test data provider
        /// </summary>
        private ITestDataProvider testDataProvider = null;
        /// <summary>
        /// Number of miliseconds of engine idle time to cause a shutdown
        /// </summary>
        private int engineShutdownTimeout = 30000;

        /// <summary>
        /// Number of initial node count
        /// </summary>
        private int initialNodeCount = 1;

        /// <summary>
        /// Default node id
        /// </summary>
        private int nodeId = 1;

        /// <summary>
        /// Only log critical events by default
        /// </summary>
        private bool logOnlyCriticalEvents = true;

        /// <summary>
        /// The last status of the engine reported
        /// </summary>
        private BuildRequestEngineStatus lastEngineStatus;

        /// <summary>
        /// Internal Event that is fired when the engine status changes
        /// </summary>
        private AutoResetEvent engineStatusChangedEvent;

        /// <summary>
        /// Delegate which handles initilizing the components requested for
        /// </summary>
        private GetComponentDelegate getComponentCallback;

        /// <summary>
        /// All the build components returned by the host
        /// </summary>
        private Queue<IBuildComponent> buildComponents;

        /// <summary>
        /// The build parameters.
        /// </summary>
        private BuildParameters buildParameters;

        /// <summary>
        /// Global timeout is 30 seconds
        /// </summary>
        public static int globalTimeOut = 30000;

        /// <summary>
        /// Retrieves the LegacyThreadingData associated with a particular component host
        /// </summary>
        private LegacyThreadingData legacyThreadingData;

        /// <summary>
        /// Constructor 
        /// </summary>
        internal QAMockHost(GetComponentDelegate getComponentCallback)
        {
            this.buildParameters = new BuildParameters();
            this.getComponentCallback = getComponentCallback;
            this.engineStatusChangedEvent = new AutoResetEvent(false);
            this.lastEngineStatus = BuildRequestEngineStatus.Shutdown;
            this.loggingService = this;
            this.requestEngine = null;
            this.testDataProvider = null;
            this.buildComponents = new Queue<IBuildComponent>();
            this.legacyThreadingData = new LegacyThreadingData();
        }

        /// <summary>
        /// Returns the node logging service.  We don't distinguish here.
        /// </summary>
        /// <param name="buildId">The build for which the service should be returned.</param>
        /// <returns>The logging service.</returns>
        public ILoggingService LoggingService
        {
            get
            {
                return this.loggingService;
            }
        }

        /// <summary>
        /// Retrieves the LegacyThreadingData associated with a particular component host
        /// </summary>
        LegacyThreadingData IBuildComponentHost.LegacyThreadingData
        {
            get
            {
                return legacyThreadingData;
            }
        }

        /// <summary>
        /// Retrieves the host name.
        /// </summary>
        public string Name
        {
            get
            {
                return "QAMockHost";
            }
        }

        /// <summary>
        /// Returns the build parameters.
        /// </summary>
        public BuildParameters BuildParameters
        {
            get
            {
                return buildParameters;
            }
        }

        /// <summary>
        /// Constructs and returns a component of the specified type.
        /// </summary>
        public IBuildComponent GetComponent(BuildComponentType type)
        {
            IBuildComponent returnComponent = null;

            switch(type)
            {
                case BuildComponentType.LoggingService: // Singleton
                    return (IBuildComponent)this.loggingService;

                case BuildComponentType.TestDataProvider: // Singleton
                    if (this.testDataProvider != null)
                    {
                        return (IBuildComponent)this.testDataProvider;
                    }

                    returnComponent = this.getComponentCallback(type);
                    if (returnComponent != null)
                    {
                        returnComponent.InitializeComponent(this);
                        this.testDataProvider = (ITestDataProvider)returnComponent;
                    }

                    break;

                case BuildComponentType.RequestEngine: // Singleton
                    if (this.requestEngine != null)
                    {
                        return (IBuildComponent)this.requestEngine;
                    }

                    returnComponent = this.getComponentCallback(type);
                    if (returnComponent != null)
                    {
                        returnComponent.InitializeComponent(this);
                        this.requestEngine = (IBuildRequestEngine)returnComponent;
                        this.requestEngine.OnEngineException += new EngineExceptionDelegate(RequestEngine_OnEngineException);
                        this.requestEngine.OnNewConfigurationRequest += new NewConfigurationRequestDelegate(RequestEngine_OnNewConfigurationRequest);
                        this.requestEngine.OnRequestBlocked += new RequestBlockedDelegate(RequestEngine_OnNewRequest);
                        this.requestEngine.OnRequestComplete += new RequestCompleteDelegate(RequestEngine_OnRequestComplete);
                        this.requestEngine.OnStatusChanged += new EngineStatusChangedDelegate(RequestEngine_OnStatusChanged);
                    }

                    break;

                default:
                    returnComponent = this.getComponentCallback(type);
                    if (returnComponent != null)
                    {
                        returnComponent.InitializeComponent(this);
                    }
                    break;
            }

            if (returnComponent != null)
            {
                lock (this.buildComponents)
                {
                    this.buildComponents.Enqueue(returnComponent);
                }
            }

            return returnComponent;
        }

        /// <summary>
        /// Registers a component factory.
        /// </summary>
        public void RegisterFactory(BuildComponentType type, BuildComponentFactoryDelegate factory)
        {
        }

        #endregion

        #region Public properties

        /// <summary>
        /// Node ID
        /// </summary>
        internal int NodeId
        {
            get
            {
                return this.nodeId;
            }
            set
            {
                this.nodeId = value;
            }
        }

        /// <summary>
        /// True to log only critical events
        /// </summary>
        internal bool LogOnlyCriticalEvents
        {
            get
            {
                return this.logOnlyCriticalEvents;
            }
            set
            {
                this.logOnlyCriticalEvents = value;
            }
        }

        /// <summary>
        /// Number if idle mili seconds to wait before shutting down the build request engine
        /// </summary>
        internal int EngineShutdownTimeout
        {
            get
            {
                return this.engineShutdownTimeout;
            }
            set
            {
                this.engineShutdownTimeout = value;
            }
        }

        /// <summary>
        /// Number of initial nodes
        /// </summary>
        internal int InitialNodeCount
        {
            get
            {
                return this.initialNodeCount;
            }
            set
            {
                this.initialNodeCount = value;
            }
        }

        #endregion

        #region IBuildComponent Members

        /// <summary>
        /// Sets the component host. Since we are a host and we do not have a parent host we do not need to do anything here.
        /// </summary>
        /// <param name="host">The component host</param>
        public void InitializeComponent(IBuildComponentHost host)
        {
            return;
        }

        /// <summary>
        /// Shuts down the component. First shutdown the request engine. Then shutdown the remaining of the component.
        /// Check if the test data provider exists before shutting it down as it may not have been registered yet because this is also
        /// called in TearDown and teardown is called if an exception is received.
        /// </summary>
        public void ShutdownComponent()
        {
            ShutDownRequestEngine();
            if (this.testDataProvider != null)
            {
                ((IBuildComponent)this.testDataProvider).ShutdownComponent();
            }

            this.buildComponents.Clear();
            this.loggingService = null;
            this.requestEngine = null;
        }

        /// <summary>
        /// Cancels the current build
        /// </summary>
        public void AbortBuild()
        {
            this.requestEngine.CleanupForBuild();
        }

        /// <summary>
        /// Wait for the Build request engine to shutdown
        /// </summary>
        public void ShutDownRequestEngine()
        {
            if (this.LastEngineStatus != BuildRequestEngineStatus.Shutdown)
            {
                this.requestEngine.CleanupForBuild();
                ((IBuildComponent)this.requestEngine).ShutdownComponent();
                WaitForEngineStatus(BuildRequestEngineStatus.Shutdown);
            }
        }

        /// <summary>
        /// Waits for the engine status requested. If a status is not changed within a certain amout of time then fail.
        /// </summary>
        public void WaitForEngineStatus(BuildRequestEngineStatus status)
        {
            while (this.LastEngineStatus != status)
            {
                if (this.engineStatusChangedEvent.WaitOne(QAMockHost.globalTimeOut, false) == false)
                {
                    Assert.Fail("Requested engine status was not received within - " + QAMockHost.globalTimeOut.ToString() + " seconds.");
                }
            }
        }

        #endregion

        #region Event Methods

        /// <summary>
        /// Gets called by the build request engine when the build request engine state changes.
        /// Special handeling when a shutdown has been sent so that only shutdown status set the event
        /// </summary>
        private void RequestEngine_OnStatusChanged(BuildRequestEngineStatus newStatus)
        {
            this.LastEngineStatus = newStatus;
            this.engineStatusChangedEvent.Set();
        }

        /// <summary>
        /// Gets called by the build request engine when the build request has been completed
        /// </summary>
        private void RequestEngine_OnRequestComplete(BuildRequest request, BuildResult result)
        {
            if (this.testDataProvider != null)
            {
                this.testDataProvider.NewResult = new ResultFromEngine(request, result);
            }
        }

        /// <summary>
        /// Gets called by the build request engine when there is a new build request (engine callback)
        /// </summary>
        /// <param name="request"></param>
        private void RequestEngine_OnNewRequest(BuildRequestBlocker blocker)
        {
            if (this.testDataProvider != null)
            {
                foreach (BuildRequest request in blocker.BuildRequests)
                {
                    this.testDataProvider.NewRequest = request;
                }
            }
        }

        /// <summary>
        /// Gets called by the build request engine when the a configuration for a new build request is not present
        /// </summary>
        /// <param name="config"></param>
        private void RequestEngine_OnNewConfigurationRequest(BuildRequestConfiguration config)
        {
            if (this.testDataProvider != null)
            {
                this.testDataProvider.NewConfiguration = config;
            }
        }

        /// <summary>
        /// Gets called by the build request engine when the build request engine when there is an exception
        /// </summary>
        /// <param name="e"></param>
        private void RequestEngine_OnEngineException(Exception e)
        {
            if (this.testDataProvider != null)
            {
                this.testDataProvider.EngineException = e;
            }
        }

        private BuildRequestEngineStatus LastEngineStatus
        {
            get
            {
                return this.lastEngineStatus;
            }
            set
            {
               this.lastEngineStatus = value;
            }
        }

        #endregion
    }
}
