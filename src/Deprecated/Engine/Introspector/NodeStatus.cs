// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is a container for node status
    /// </summary>
    internal class NodeStatus
    {
        #region Constructors

        /// <summary>
        /// Default constructor creating a NodeStatus
        /// </summary>
        internal NodeStatus
        (
            int requestId,
            bool isActive,
            int queueDepth,
            long lastTaskActivityTimeStamp,
            long lastEngineActivityTimeStamp,
            bool isLaunchInProgress
        )
        {
            this.requestId = requestId;
            this.isActive = isActive;
            this.queueDepth = queueDepth;
            this.lastTaskActivityTimeStamp = lastTaskActivityTimeStamp;
            this.lastEngineActivityTimeStamp = lastEngineActivityTimeStamp;
            this.isLaunchInProgress = isLaunchInProgress;
            this.unhandledException = null;

            this.statusTimeStamp = DateTime.Now.Ticks;
        }

        /// <summary>
        /// Create a node status describing an unhandled error
        /// </summary>
        internal NodeStatus
        (
            Exception unhandledException
        )
        {
            this.requestId = UnrequestedStatus;
            this.isActive = true;
            this.isLaunchInProgress = false;
            this.unhandledException = unhandledException;

            this.statusTimeStamp = DateTime.Now.Ticks;
        }

        /// <summary>
        /// Create a node status indicating that breadth first traversal should be used
        /// </summary>
        internal NodeStatus
        (
            bool useBreadthFirstTraversal
        )
        {
            this.requestId = UnrequestedStatus;
            this.isActive = true;
            this.isLaunchInProgress = false;
            this.unhandledException = null;
            this.traversalType = useBreadthFirstTraversal;
        }

        /// <summary>
        /// Create a node status indicating that node process has exited
        /// </summary>
        internal NodeStatus
        (
            int requestId
        )
        {
            this.requestId = requestId;
            this.isActive = true;
            this.isLaunchInProgress = false;
            this.unhandledException = null;
            this.hasExited = true;
        }
        #endregion

        #region Properties

        /// <summary>
        /// The time period for which the node has been idle when the status report was filled out
        /// </summary>
        internal long TimeSinceLastTaskActivity
        {
            get
            {
                return (statusTimeStamp - lastTaskActivityTimeStamp);
            }
        }


        /// <summary>
        /// The time period for which the node has been idle when the status report was filled out
        /// </summary>
        internal long TimeSinceLastLoopActivity
        {
            get
            {
                return (statusTimeStamp - lastEngineActivityTimeStamp);
            }
        }

        /// <summary>
        /// The time stamp at which the node was last active
        /// </summary>
        internal long LastTaskActivity
        {
            get
            {
                return lastTaskActivityTimeStamp;
            }
        }

        /// <summary>
        /// The time stamp at which there was activity in the node's build loop
        /// </summary>
        internal long LastLoopActivity
        {
            get
            {
                return lastEngineActivityTimeStamp;
            }
        }

        /// <summary>
        /// True if the node is active (i.e. has been launched and can accept commands)
        /// </summary>
        internal bool IsActive
        {
            get
            {
                return this.isActive;
            }
        }

        /// <summary>
        /// True if the node process is no longer alive
        /// </summary>
        internal bool HasExited
        {
            get
            {
                return this.hasExited;
            }
        }
        
        /// <summary>
        /// The token of the request to which this is a response (-1 if status is unrequested)
        /// </summary>
        internal int RequestId
        {
            get
            {
                return this.requestId;
            }
        }

        /// <summary>
        /// The number of requests that need to be processed
        /// </summary>
        internal int QueueDepth
        {
            get
            {
                return this.queueDepth;
            }
        }

        /// <summary>
        /// The state of the targets which are in progress on the node
        /// </summary>
        internal TargetInProgessState [] StateOfInProgressTargets
        {
            get
            {
                return this.stateOfInProgressTargets;
            }
            set
            {
                this.stateOfInProgressTargets = value;
            }
        }

        /// <summary>
        /// True if the node is in the process of being launched, but is not yet active
        /// </summary>
        internal bool IsLaunchInProgress
        {
            get
            {
                return isLaunchInProgress;
            }
        }

        /// <summary>
        /// Returns the exception that occurred on the node
        /// </summary>
        internal Exception UnhandledException
        {
            get
            {
                return unhandledException;
            }
        }

        internal bool TraversalType
        {
            get
            {
                return traversalType;
            }
        }
        #endregion

        #region Data
        private long statusTimeStamp; // the timestamp indicating when this status structure was filled out
        private int  requestId; // the token of the request to which this is a response (-1 if status is unrequested)
        private bool isActive; // is the node active
        private bool isLaunchInProgress; // is the node in the process of being launched
        private int  queueDepth; // the number of build request in the node's queue
        private long lastTaskActivityTimeStamp; // the time stamp of the last task activity
        private long lastEngineActivityTimeStamp; // the time stamp of the last engine activity
        private TargetInProgessState[] stateOfInProgressTargets;
        private Exception unhandledException; // unhandled exception
        private bool traversalType; // if true use breadth first traversal
        private bool hasExited; // if true the node process is no longer  alive
        private static BinaryFormatter formatter = new BinaryFormatter();
        internal const int UnrequestedStatus = -1; // used to indicate that the node is generating status without request
        #endregion

        #region CustomSerializationToStream
        internal void WriteToStream(BinaryWriter writer)
        {
            writer.Write(traversalType);
            writer.Write((Int64)statusTimeStamp);
            writer.Write((Int32)requestId);
            writer.Write(isActive);
            writer.Write(isLaunchInProgress);
            writer.Write((Int32)queueDepth);
            writer.Write((Int64)lastTaskActivityTimeStamp);
            writer.Write((Int64)lastEngineActivityTimeStamp);

            if (stateOfInProgressTargets == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write((Int32)stateOfInProgressTargets.Length);
                for (int i = 0; i < stateOfInProgressTargets.Length; i++)
                {
                    if (stateOfInProgressTargets[i] == null)
                    {
                        writer.Write((byte)0);
                    }
                    else
                    {
                       writer.Write((byte)1);
                       stateOfInProgressTargets[i].WriteToStream(writer);
                    }
                }
            }

            if (unhandledException == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                formatter.Serialize(writer.BaseStream, unhandledException);
            }
        }

        internal static NodeStatus CreateFromStream(BinaryReader reader)
        {
            NodeStatus status = new NodeStatus(null);
            status.traversalType = reader.ReadBoolean();
            status.statusTimeStamp = reader.ReadInt64();
            status.requestId = reader.ReadInt32();
            status.isActive = reader.ReadBoolean();
            status.isLaunchInProgress = reader.ReadBoolean();
            status.queueDepth = reader.ReadInt32();
            status.lastTaskActivityTimeStamp = reader.ReadInt64();
            status.lastEngineActivityTimeStamp = reader.ReadInt64();

            if (reader.ReadByte() == 0)
            {
                status.stateOfInProgressTargets = null;
            }
            else
            {
                int numberOfInProgressTargets = reader.ReadInt32();
                status.stateOfInProgressTargets = new TargetInProgessState[numberOfInProgressTargets];
                for (int i = 0; i < numberOfInProgressTargets; i++)
                {
                    if (reader.ReadByte() == 0)
                    {
                        status.stateOfInProgressTargets[i] = null;
                    }
                    else
                    {
                        TargetInProgessState state = new TargetInProgessState();
                        state.CreateFromStream(reader);
                        status.stateOfInProgressTargets[i] = state;
                    }
                }
            }

            if (reader.ReadByte() == 0)
            {
                status.unhandledException = null;
            }
            else
            {
                status.unhandledException = (Exception)formatter.Deserialize(reader.BaseStream);
            }
            return status;
        }
        #endregion
    }
}
