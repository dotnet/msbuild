// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.UnitTests.BackEnd;
using System;
using System.Threading;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Engine.UnitTests.BackEnd;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using LegacyThreadingData = Microsoft.Build.Execution.LegacyThreadingData;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Mock host which is used during tests which need a host object
    /// </summary>
    internal class MockHost : MockLoggingService, IBuildComponentHost, IBuildComponent
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

        #region SystemParameterFields

        #endregion;

        /// <summary>
        /// Constructor
        /// </summary>
        public MockHost()
            : this(new BuildParameters())
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public MockHost(BuildParameters buildParameters)
        {
            _buildParameters = buildParameters;

            _buildParameters.ProjectRootElementCache = new ProjectRootElementCache(false);

            _configCache = new ConfigCache();
            ((IBuildComponent)_configCache).InitializeComponent(this);

            // We are a logging service
            _loggingService = this;

            _legacyThreadingData = new LegacyThreadingData();

            _requestEngine = new BuildRequestEngine();
            ((IBuildComponent)_requestEngine).InitializeComponent(this);

            _resultsCache = new ResultsCache();
            ((IBuildComponent)_resultsCache).InitializeComponent(this);

            _requestBuilder = new Microsoft.Build.UnitTests.BackEnd.BuildRequestEngine_Tests.MockRequestBuilder();
            ((IBuildComponent)_requestBuilder).InitializeComponent(this);

            _targetBuilder = new TestTargetBuilder();
            ((IBuildComponent)_targetBuilder).InitializeComponent(this);

            _sdkResolverService = new MockSdkResolverService();
            ((IBuildComponent)_sdkResolverService).InitializeComponent(this);
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
            switch (type)
            {
                case BuildComponentType.ConfigCache:
                    return (IBuildComponent)_configCache;

                case BuildComponentType.LoggingService:
                    return (IBuildComponent)_loggingService;

                case BuildComponentType.RequestEngine:
                    return (IBuildComponent)_requestEngine;

                case BuildComponentType.TargetBuilder:
                    return (IBuildComponent)_targetBuilder;

                case BuildComponentType.ResultsCache:
                    return (IBuildComponent)_resultsCache;

                case BuildComponentType.RequestBuilder:
                    return (IBuildComponent)_requestBuilder;

                case BuildComponentType.SdkResolverService:
                    return (IBuildComponent)_sdkResolverService;

                default:
                    throw new ArgumentException("Unexpected type " + type);
            }
        }

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

        #region IBuildComponent Members

        /// <summary>
        /// Initialize this component using the provided host
        /// </summary>
        public void InitializeComponent(IBuildComponentHost host)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Clean up any state
        /// </summary>
        public void ShutdownComponent()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
