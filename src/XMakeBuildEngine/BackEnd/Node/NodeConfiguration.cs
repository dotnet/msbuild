// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>A packet which contains information needed for a node to configure itself for build.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;

using LoggerDescription = Microsoft.Build.Logging.LoggerDescription;
using BuildParameters = Microsoft.Build.Execution.BuildParameters;

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

        /// <summary>
        /// The app domain information needed for setting up AppDomain-isolated tasks.
        /// </summary>
        private AppDomainSetup _appDomainSetup;

        /// <summary>
        /// The forwarding loggers to use.
        /// </summary>
        private LoggerDescription[] _forwardingLoggers;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="nodeId">The node id.</param>
        /// <param name="buildParameters">The build parameters</param>
        /// <param name="forwardingLoggers">The forwarding loggers.</param>
        /// <param name="appDomainSetup">The AppDomain setup information.</param>
        public NodeConfiguration
            (
            int nodeId,
            BuildParameters buildParameters,
            LoggerDescription[] forwardingLoggers,
            AppDomainSetup appDomainSetup
            )
        {
            _nodeId = nodeId;
            _buildParameters = buildParameters;
            _forwardingLoggers = forwardingLoggers;
            _appDomainSetup = appDomainSetup;
        }

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

        /// <summary>
        /// Retrieves the app domain setup information.
        /// </summary>
        public AppDomainSetup AppDomainSetup
        {
            [DebuggerStepThrough]
            get
            { return _appDomainSetup; }
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
        public void Translate(INodePacketTranslator translator)
        {
            translator.Translate(ref _nodeId);
            translator.Translate(ref _buildParameters, BuildParameters.FactoryForDeserialization);
            translator.TranslateArray(ref _forwardingLoggers, LoggerDescription.FactoryForTranslation);
            translator.TranslateDotNet(ref _appDomainSetup);
        }

        /// <summary>
        /// Factory for deserialization.
        /// </summary>
        internal static INodePacket FactoryForDeserialization(INodePacketTranslator translator)
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
            return new NodeConfiguration(_nodeId, _buildParameters, _forwardingLoggers, _appDomainSetup);
        }
    }
}
