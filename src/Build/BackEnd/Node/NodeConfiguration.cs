// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_APPDOMAIN
using System;
using System.Runtime.CompilerServices;
#endif
using System.Diagnostics;

using Microsoft.Build.Execution;
using Microsoft.Build.Logging;
#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// NodeConfiguration contains all of the information necessary for a node to configure itself for building.
    /// </summary>
    internal class NodeConfiguration : INodePacket
    {
        /// <summary>
        /// The node id
        /// </summary>
        private int _nodeId;

        /// <summary>
        /// The system parameters which were defined on the host.
        /// </summary>
        private BuildParameters _buildParameters;

#if FEATURE_APPDOMAIN
        /// <summary>
        /// The app domain information needed for setting up AppDomain-isolated tasks.
        /// </summary>
        private AppDomainSetup _appDomainSetup;
#endif

        /// <summary>
        /// The forwarding loggers to use.
        /// </summary>
        private LoggerDescription[] _forwardingLoggers;

        /// <summary>
        /// The logging configuration for the node.
        /// </summary>
        private LoggingNodeConfiguration _loggingNodeConfiguration;

#pragma warning disable 1572 // appDomainSetup not always there
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <param name="buildParameters">The build parameters</param>
        /// <param name="forwardingLoggers">The forwarding loggers.</param>
        /// <param name="appDomainSetup">The AppDomain setup information.</param>
        /// <param name="loggingNodeConfiguration">The logging configuration for the node.</param>
        public NodeConfiguration(
            int nodeId,
            BuildParameters buildParameters,
            LoggerDescription[] forwardingLoggers,
#if FEATURE_APPDOMAIN
            AppDomainSetup appDomainSetup,
#endif
            LoggingNodeConfiguration loggingNodeConfiguration)
        {
            _nodeId = nodeId;
            _buildParameters = buildParameters;
            _forwardingLoggers = forwardingLoggers;
#if FEATURE_APPDOMAIN
            _appDomainSetup = appDomainSetup;
#endif
            _loggingNodeConfiguration = loggingNodeConfiguration;
        }
#pragma warning restore

        /// <summary>
        /// Private constructor for deserialization
        /// </summary>
        private NodeConfiguration()
        {
        }

        /// <summary>
        /// Gets or sets the node id
        /// </summary>
        public int NodeId
        {
            [DebuggerStepThrough]
            get
            { return _nodeId; }

            [DebuggerStepThrough]
            set
            { _nodeId = value; }
        }

        /// <summary>
        /// Retrieves the system parameters.
        /// </summary>
        public BuildParameters BuildParameters
        {
            [DebuggerStepThrough]
            get
            { return _buildParameters; }
        }

        /// <summary>
        /// Retrieves the logger descriptions.
        /// </summary>
        public LoggerDescription[] LoggerDescriptions
        {
            [DebuggerStepThrough]
            get
            { return _forwardingLoggers; }
        }

#if FEATURE_APPDOMAIN
        /// <summary>
        /// Retrieves the app domain setup information.
        /// </summary>
        public AppDomainSetup AppDomainSetup
        {
            [DebuggerStepThrough]
            get
            { return _appDomainSetup; }
        }
#endif

        /// <summary>
        /// The logging configuration for the node.
        /// </summary>
        public LoggingNodeConfiguration LoggingNodeConfiguration
        {
            [DebuggerStepThrough]
            get
            { return _loggingNodeConfiguration; }
        }

        #region INodePacket Members

        /// <summary>
        /// Retrieves the packet type.
        /// </summary>
        public NodePacketType Type
        {
            [DebuggerStepThrough]
            get
            { return NodePacketType.NodeConfiguration; }
        }

        #endregion

        #region INodePacketTranslatable Members

        /// <summary>
        /// Translates the packet to/from binary form.
        /// </summary>
        /// <param name="translator">The translator to use.</param>
        public void Translate(ITranslator translator)
        {
            translator.Translate(ref _nodeId);
            translator.Translate(ref _buildParameters, BuildParameters.FactoryForDeserialization);
            translator.TranslateArray(ref _forwardingLoggers, LoggerDescription.FactoryForTranslation);
#if FEATURE_APPDOMAIN
            byte[] appDomainConfigBytes = null;

            // Set the configuration bytes just before serialization in case the SetConfigurationBytes was invoked during lifetime of this instance.
            // The null guard also keeps the JIT-isolated helper uninvoked when hosted on the .NET
            // runtime, where the setup is always null (see BuildManager.GetNodeConfiguration).
            if (translator.Mode == TranslationDirection.WriteToStream && _appDomainSetup != null)
            {
                appDomainConfigBytes = GetAppDomainConfigBytes(_appDomainSetup);
            }

            translator.Translate(ref appDomainConfigBytes);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                _appDomainSetup = CreateAppDomainSetupFromConfigBytes(appDomainConfigBytes);
            }
#endif
            translator.Translate(ref _loggingNodeConfiguration);
        }

#if FEATURE_APPDOMAIN
        // The AppDomainSetup configuration-bytes APIs do not exist when this .NET Framework assembly
        // is hosted on the .NET runtime, and an unresolvable member fails the JIT of the entire
        // referencing method — these never-inlined helpers keep those references out of Translate.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static byte[] GetAppDomainConfigBytes(AppDomainSetup appDomainSetup) => appDomainSetup.GetConfigurationBytes();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static AppDomainSetup CreateAppDomainSetupFromConfigBytes(byte[] appDomainConfigBytes)
        {
            AppDomainSetup appDomainSetup = new AppDomainSetup();
            appDomainSetup.SetConfigurationBytes(appDomainConfigBytes);
            return appDomainSetup;
        }
#endif

        /// <summary>
        /// Factory for deserialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            NodeConfiguration configuration = new NodeConfiguration();
            configuration.Translate(translator);

            return configuration;
        }
        #endregion

        /// <summary>
        /// We need to clone this object since it gets modified for each node which is launched.
        /// </summary>
        internal NodeConfiguration Clone()
        {
            return new NodeConfiguration(_nodeId, _buildParameters, _forwardingLoggers
#if FEATURE_APPDOMAIN
                , _appDomainSetup
#endif
                , _loggingNodeConfiguration);
        }
    }
}
