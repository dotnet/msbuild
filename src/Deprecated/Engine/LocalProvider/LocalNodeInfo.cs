// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This contains information and data for each node. This class organizes the data so that
    /// LocalNodeProvider can be simplified.
    /// </summary>
    internal class LocalNodeInfo
    {
        #region Constructors
        internal LocalNodeInfo(int availableNodeNumberHint)
        {
            this.nodeState              = LocalNodeProvider.NodeState.NotLaunched;
            this.targetList             = new LinkedList<BuildRequest>();
            this.nodeCommandQueue       = new DualQueue<LocalCallDescriptor>();
            this.nodeHiPriCommandQueue  = new DualQueue<LocalCallDescriptor>();
            this.nodeReserveHandle      = null;
            this.communicationFailed    = false;
            this.processId              = unInitializedProcessId;

            // Figure out the next available node number
            ReserveNextAvailableNodeNumber(availableNodeNumberHint);
        }
        #endregion

        #region Properties

        internal SharedMemory SharedMemoryToNode
        {
            get
            {
                return this.sharedMemoryToNode;
            }
            set
            {
                this.sharedMemoryToNode = value;
            }
        }

        internal SharedMemory SharedMemoryFromNode
        {
            get
            {
                return this.sharedMemoryFromNode;
            }
            set
            {
                this.sharedMemoryFromNode = value;
            }
        }

        internal DualQueue<LocalCallDescriptor> NodeCommandQueue
        {
            get
            {
                return this.nodeCommandQueue;
            }
        }

        internal DualQueue<LocalCallDescriptor> NodeHiPriCommandQueue
        {
            get
            {
                return this.nodeHiPriCommandQueue;
            }
        }


        internal LinkedList<BuildRequest> TargetList
        {
            get
            {
                return this.targetList;
            }
            set
            {
                this.targetList = value;
            }
        }

        internal LocalNodeProvider.NodeState NodeState
        {
            get
            {
                return this.nodeState;
            }
            set
            {
                this.nodeState = value;
            }
        }

        internal int NodeNumber
        {
            get
            {
                return this.nodeNumber;
            }
        }

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

        internal int ProcessId
        {
            get
            {
                return this.processId;
            }
            set
            {
                this.processId = value;
            }
        }

        internal bool CommunicationFailed
        {
            get
            {
                return this.communicationFailed;
            }
            set
            {
                this.communicationFailed = value;
            }
        }

        public bool ShutdownResponseReceived
        {
            get 
            {
                return shutdownResponseReceived;
            }
            set 
            { 
                shutdownResponseReceived = value;
            }
        }
        #endregion

        #region Methods

        /// <summary>
        /// This method creates the shared memory buffers for communicating with the node
        /// </summary>
        /// <returns>Was the shared memory created and is useable</returns>
        internal bool CreateSharedMemoryBuffers()
        {
            this.sharedMemoryToNode =
                new SharedMemory
                (
                    LocalNodeProviderGlobalNames.NodeInputMemoryName(this.nodeNumber),
                    SharedMemoryType.WriteOnly,
                    false
                );

            if (!this.sharedMemoryToNode.IsUsable)
            {
                return false;
            }


            this.sharedMemoryFromNode =
                new SharedMemory
                (
                    LocalNodeProviderGlobalNames.NodeOutputMemoryName(this.nodeNumber),
                    SharedMemoryType.ReadOnly,
                    false
                );

            if (!this.sharedMemoryFromNode.IsUsable)
            {
                return false;
            }

            return true;
        }

        internal void ReleaseNode()
        {
            if ( nodeReserveHandle != null )
            {
                nodeReserveHandle.Close();
                processId = invalidProcessId;
                nodeReserveHandle = null;
            }
        }
        /// <summary>
        /// This function attempts to find out a node number for which
        /// the event named Node_x_ProviderMutex doesn't exist. The existance
        /// of the event indicates that some other node provider is using the node.
        /// </summary>
        private void ReserveNextAvailableNodeNumber(int currentNodeNumber)
        {
            while (nodeReserveHandle == null)
            {
                bool createdNew;
                nodeReserveHandle = 
                    new EventWaitHandle(false, EventResetMode.ManualReset, LocalNodeProviderGlobalNames.NodeReserveEventName(currentNodeNumber), out createdNew);
                if (!createdNew)
                {
                    nodeReserveHandle.Close();
                    nodeReserveHandle = null;
                    currentNodeNumber++;
                }
                else
                {
                    nodeNumber = currentNodeNumber;
                    // Create the shared memory resources
                    if (!CreateSharedMemoryBuffers())
                    {
                        nodeReserveHandle.Close();
                        nodeReserveHandle = null;
                        currentNodeNumber++;
                    }
                }
            }
        }

        #endregion

        #region Data
        private SharedMemory sharedMemoryToNode;
        private SharedMemory sharedMemoryFromNode;
        private DualQueue<LocalCallDescriptor> nodeCommandQueue;
        private DualQueue<LocalCallDescriptor> nodeHiPriCommandQueue;
        private EventWaitHandle nodeReserveHandle;
        private bool communicationFailed;

        private LinkedList<BuildRequest> targetList;
        private LocalNodeProvider.NodeState nodeState;
        private int nodeNumber;
        private int nodeId;
        private int processId;
        private bool shutdownResponseReceived;

        internal const int invalidProcessId = -1;
        internal const int unInitializedProcessId = -2;

        #endregion

    }
}
