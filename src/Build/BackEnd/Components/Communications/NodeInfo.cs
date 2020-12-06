// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using Microsoft.Build.Execution;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Contains information about the state of a node.
    /// </summary>
    internal class NodeInfo
    {
        /// <summary>
        /// The node ID
        /// </summary>
        private int _nodeId;

        /// <summary>
        /// The provider type
        /// </summary>
        private NodeProviderType _providerType;

        /// <summary>
        /// The configuration IDs the node knows about.  These are not necessarily the ones
        /// currently assigned to the node, as that can change.
        /// </summary>
        private HashSet<int> _configurationIDs;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NodeInfo(int nodeId, NodeProviderType providerType)
        {
            _nodeId = nodeId;
            _providerType = providerType;
            _configurationIDs = new HashSet<int>();
        }

        /// <summary>
        /// The ID of the node.
        /// </summary>
        public int NodeId
        {
            get { return _nodeId; }
        }

        /// <summary>
        /// The type of provider which manages this node.
        /// </summary>
        public NodeProviderType ProviderType
        {
            get { return _providerType; }
        }

        /// <summary>
        /// Assigns the specific configuration ID to the node.
        /// </summary>
        /// <returns>
        /// True if the configuration is not already known to the node and must be sent to it, false otherwise.
        /// </returns>
        public bool AssignConfiguration(int configId)
        {
            if (!HasConfiguration(configId))
            {
                _configurationIDs.Add(configId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the specified configuration if is known to the node.
        /// </summary>
        public bool HasConfiguration(int configId)
        {
            return _configurationIDs.Contains(configId);
        }

        /// <summary>
        /// Returns true if this node can service requests with the specified affinity.
        /// </summary>
        internal bool CanServiceRequestWithAffinity(NodeAffinity nodeAffinity)
        {
            return nodeAffinity switch
            {
                NodeAffinity.Any => true,
                NodeAffinity.InProc => _providerType == NodeProviderType.InProc,
                NodeAffinity.OutOfProc => _providerType != NodeProviderType.InProc,
                _ => true,
            };
        }
    }
}
