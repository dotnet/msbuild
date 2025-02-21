// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Microsoft.Build.Engine.UnitTests.BackEnd;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.TelemetryInfra;
using LegacyThreadingData = Microsoft.Build.Execution.LegacyThreadingData;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Mock host which is used during tests which need a host object
    /// </summary>
    internal sealed class MockHost : MockLoggingService, IBuildComponentHost, IBuildComponent
    {
        /// <summary>
        /// Configuration cache
        /// </summary>
        private IConfigCache _configCache;

        /// <summary>
        /// Logging service which will do the actual logging
        /// </summary>
        private ILoggingService _loggingService;

        /// <summary>
        /// Request engine to process the build requests
        /// </summary>
        private IBuildRequestEngine _requestEngine;

        /// <summary>
        /// Target Builder
        /// </summary>
        private ITargetBuilder _targetBuilder;

        /// <summary>
        /// The build parameters.
        /// </summary>
        private BuildParameters _buildParameters;

        /// <summary>
        /// Cache of requests
        /// </summary>
        private IResultsCache _resultsCache;

        /// <summary>
        /// Builder which will do the actual building of the requests
        /// </summary>
        private IRequestBuilder _requestBuilder;

        /// <summary>
        /// Holds the data for legacy threading semantics
        /// </summary>
        private LegacyThreadingData _legacyThreadingData;

        private ISdkResolverService _sdkResolverService;

        private IBuildCheckManagerProvider _buildCheckManagerProvider;

        private TelemetryForwarderProvider _telemetryForwarder;

        #region SystemParameterFields

        #endregion;

        /// <summary>
        /// Initializes a new instance of the <see cref="MockHost"/> class.
        /// </summary>
        /// <param name="overrideConfigCache">The override config cache.</param>
        /// <param name="overrideResultsCache">The override results cache.</param>
        public MockHost(ConfigCache overrideConfigCache = null, ResultsCache overrideResultsCache = null)
            : this(new BuildParameters(), overrideConfigCache, overrideResultsCache)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MockHost"/> class.
        /// </summary>
        /// <param name="buildParameters">The mock host's build parameters.</param>
        /// <param name="overrideConfigCache">The override config cache.</param>
        /// <param name="overrideResultsCache">The override results cache.</param>
        public MockHost(BuildParameters buildParameters, ConfigCache overrideConfigCache = null, ResultsCache overrideResultsCache = null)
        {
            _buildParameters = buildParameters;

            _buildParameters.ProjectRootElementCache = new ProjectRootElementCache(false);

            if (overrideConfigCache != null && overrideResultsCache != null)
            {
                _configCache = new ConfigCacheWithOverride(overrideConfigCache);
                _resultsCache = new ResultsCacheWithOverride(overrideResultsCache);
            }
            else if (overrideConfigCache == null && overrideResultsCache == null)
            {
                _configCache = new ConfigCache();
                _resultsCache = new ResultsCache();
            }
            else if (overrideConfigCache == null)
            {
                throw new ArgumentNullException($"Attempted to create an override cache with a null {nameof(overrideConfigCache)}.");
            }
            else
            {
                throw new ArgumentNullException($"Attempted to create an override cache with a null {nameof(overrideResultsCache)}.");
            }

            _configCache.InitializeComponent(this);

            // We are a logging service
            _loggingService = this;

            _legacyThreadingData = new LegacyThreadingData();

            _requestEngine = new BuildRequestEngine();
            ((IBuildComponent)_requestEngine).InitializeComponent(this);

            _resultsCache.InitializeComponent(this);

            _requestBuilder = new BuildRequestEngine_Tests.MockRequestBuilder();
            ((IBuildComponent)_requestBuilder).InitializeComponent(this);

            _targetBuilder = new TestTargetBuilder();
            ((IBuildComponent)_targetBuilder).InitializeComponent(this);

            _sdkResolverService = new MockSdkResolverService();
            ((IBuildComponent)_sdkResolverService).InitializeComponent(this);

            _buildCheckManagerProvider = new NullBuildCheckManagerProvider();
            ((IBuildComponent)_buildCheckManagerProvider).InitializeComponent(this);

            _telemetryForwarder = new TelemetryForwarderProvider();
            ((IBuildComponent)_telemetryForwarder).InitializeComponent(this);
        }

        /// <summary>
        /// Able to modify the loggingService this is required for testing
        /// </summary>
        public ILoggingService LoggingService
        {
            get { return _loggingService; }
            internal set { _loggingService = value; }
        }

        /// <summary>
        /// Retrieves the name of the host.
        /// </summary>
        public string Name
        {
            get
            {
                return "BackEnd.MockHost";
            }
        }

        /// <summary>
        /// Retrieve the build parameters.
        /// </summary>
        /// <returns></returns>
        public BuildParameters BuildParameters
        {
            get
            {
                return _buildParameters;
            }
        }

        /// <summary>
        /// Retrieves the LegacyThreadingData associated with a particular component host
        /// </summary>
        LegacyThreadingData IBuildComponentHost.LegacyThreadingData
        {
            get
            {
                return _legacyThreadingData;
            }
        }

        /// <summary>
        /// Able to modify the request builder this is required for testing
        /// </summary>
        internal IRequestBuilder RequestBuilder
        {
            get { return _requestBuilder; }
            set { _requestBuilder = value; }
        }

        /// <summary>
        /// Get the a component based on the request component type
        /// </summary>
        public IBuildComponent GetComponent(BuildComponentType type)
        {
            return type switch
            {
                BuildComponentType.ConfigCache => (IBuildComponent)_configCache,
                BuildComponentType.LoggingService => (IBuildComponent)_loggingService,
                BuildComponentType.RequestEngine => (IBuildComponent)_requestEngine,
                BuildComponentType.TargetBuilder => (IBuildComponent)_targetBuilder,
                BuildComponentType.ResultsCache => (IBuildComponent)_resultsCache,
                BuildComponentType.RequestBuilder => (IBuildComponent)_requestBuilder,
                BuildComponentType.SdkResolverService => (IBuildComponent)_sdkResolverService,
                BuildComponentType.BuildCheckManagerProvider => (IBuildComponent)_buildCheckManagerProvider,
                BuildComponentType.TelemetryForwarder => (IBuildComponent)_telemetryForwarder,
                _ => throw new ArgumentException("Unexpected type " + type),
            };
        }

        public TComponent GetComponent<TComponent>(BuildComponentType type) where TComponent : IBuildComponent
            => (TComponent)GetComponent(type);

        /// <summary>
        /// Register a new build component factory with the host.
        /// </summary>
        public void RegisterFactory(BuildComponentType type, BuildComponentFactoryDelegate factory)
        {
            throw new NotImplementedException();
        }

        #region INodePacketFactory Members

        /// <summary>
        /// Deserialize a packet
        /// </summary>
        public INodePacket DeserializePacket(NodePacketType type, byte[] serializedPacket)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
