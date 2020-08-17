// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Threading;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This call is used to contain, serialize and deserialize arguments for call
    /// made via INodeProvider and IEngineCallback interfaces. To make calls via these
    /// interfaces asyncronous the parameters are queued up for a IO thread which
    /// reads/writes the shared memory buffer to transfer these parameters cross 
    /// process.
    /// </summary>
    internal abstract class LocalCallDescriptor
    {
        #region Constructors
        internal LocalCallDescriptor()
        {
        }

        internal LocalCallDescriptor(LocalCallType callType)
        {
            this.callType = callType;
            this.callNumber = Interlocked.Increment(ref lastUsedCallNumber);
        }
        #endregion

        // For testing
        #region Properties
        internal int CallNumber
        {
            get
            {
                return this.callNumber;
            }
        }
        internal LocalCallType CallType
        {
            get
            {
                return this.callType;
            }
        }
        virtual internal bool NeedsReply
        {
            get
            {
                return false;
            }
        }
        virtual internal bool IsReply
        {
            get
            {
                return false;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Appropriate action to take if this event is received on the parent process
        /// </summary>
        internal virtual void HostAction( IEngineCallback engineCallback, LocalNodeProvider nodeProvider, int nodeId )
        {
            ErrorUtilities.VerifyThrow(false, "This description doesn't support this operation");
        }

        /// <summary>
        /// Appropriate action to take if this event is received on the child process
        /// </summary>
        internal virtual void NodeAction(Node node, LocalNode localNode)
        {
            ErrorUtilities.VerifyThrow(false, "This description doesn't support this operation");
        }

        /// <summary>
        /// This method constructs a reply to the node if appropriate
        /// </summary>
        /// <returns>The call descriptor to be sent back to the node</returns>
        internal virtual LocalReplyCallDescriptor ReplyFromHostAction()
        {
            ErrorUtilities.VerifyThrow(false, "This description doesn't support this operation");
            return null;
        }

        internal virtual object GetReplyData()
        {
            ErrorUtilities.VerifyThrow(false, "This description doesn't support this operation");
            return null;
        }
        #endregion

        #region Data
        private int callNumber;
        protected LocalCallType callType;
        // A counter used to provide unique Id's to all calls, is not serialized as it is static
        private static int lastUsedCallNumber = 0;
        #endregion

        #region CustomSerializationToStream
        internal virtual void WriteToStream(BinaryWriter writer)
        {
            writer.Write((byte)callType);
            writer.Write((Int32)callNumber);
        }

        internal virtual void CreateFromStream(BinaryReader reader)
        {
           callType = (LocalCallType)reader.ReadByte();
           callNumber = reader.ReadInt32();
        }
        #endregion
    }

    #region Wrapper for LocalReplyCallDescriptor
    internal class LocalReplyCallDescriptor : LocalCallDescriptor
    {
        #region Constructors
        internal LocalReplyCallDescriptor()
        {
            //Do Nothing
        }

        internal LocalReplyCallDescriptor(int requestingCallNumber, object replyData)
            : base(LocalCallType.GenericSingleObjectReply)
        {
            this.requestingCallNumber = requestingCallNumber;
            this.replyData = replyData;
        }
        #endregion

        #region Properties
        internal override bool IsReply
        {
            get
            {
                return true;
            }
        }

        internal virtual int RequestingCallNumber
        {
            get
            {
                return requestingCallNumber;
            }
        }

        internal object ReplyData
        {
            get
            {
                return replyData;
            }
        }
        #endregion

        #region Methods
        internal override void NodeAction(Node node, LocalNode localNode)
        {
            // No node action here, we do our thing in GetReplyData
        }

        internal override object GetReplyData()
        {
            return this.replyData;
        }
        #endregion

        #region Data
        private int requestingCallNumber;
        private object replyData;
        private static BinaryFormatter formatter = new BinaryFormatter();
        #endregion

        #region CustomSerializationToStream
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            writer.Write((Int32)requestingCallNumber);
            if (replyData == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);

                if (replyData is CacheEntry[])
                {
                    writer.Write((byte)0);
                    CacheEntry[] cacheArray =(CacheEntry[]) replyData;
                    writer.Write((Int32)cacheArray.Length);
                    for (int i = 0; i < cacheArray.Length; i++)
                    {
                        if (cacheArray[i] == null)
                        {
                            writer.Write((byte)0);
                        }
                        else
                        {
                            writer.Write((byte)1);
                            CacheEntryCustomSerializer.WriteToStream(cacheArray[i], writer);
                        }
                    }
                }
                else
                {
                    writer.Write((byte)1);
                    formatter.Serialize(writer.BaseStream, replyData);
                }
            }
        }

        internal override void CreateFromStream(BinaryReader reader)
        {
            base.CreateFromStream(reader);
            requestingCallNumber = reader.ReadInt32();

            if (reader.ReadByte() == 0)
            {
                replyData = null;
            }
            else
            {
                if (reader.ReadByte() == 0)
                {
                    int numberOfEntries = reader.ReadInt32();
                    CacheEntry[] cacheArray = new CacheEntry[numberOfEntries];
                    
                    for (int i = 0; i < numberOfEntries; i++)
                    {
                        if (reader.ReadByte() == 0)
                        {
                            cacheArray[i] = null;
                        }
                        else
                        {
                            cacheArray[i] = CacheEntryCustomSerializer.CreateFromStream(reader);
                        }
                    }
                    replyData = cacheArray;
                }
                else
                {
                    replyData = formatter.Deserialize(reader.BaseStream);
                }
            }
        }
        #endregion
    }
    #endregion

    #region Wrapper for PostBuildRequests
    internal class LocalCallDescriptorForPostBuildRequests : LocalCallDescriptor
    {
        #region Constructors
        internal LocalCallDescriptorForPostBuildRequests()
        {
        }

        internal LocalCallDescriptorForPostBuildRequests(BuildRequest [] buildRequests)
            :base(LocalCallType.PostBuildRequests)
        {
            this.buildRequests = buildRequests;
        }

        internal LocalCallDescriptorForPostBuildRequests(BuildRequest buildRequest)
            : base(LocalCallType.PostBuildRequests)
        {
            this.buildRequests = new BuildRequest[1];
            this.buildRequests[0] = buildRequest;
        }

        #endregion
    
        #region Methods
        internal override void HostAction(IEngineCallback engineCallback, LocalNodeProvider nodeProvider, int nodeId)
        {
            engineCallback.PostBuildRequestsToHost(buildRequests);
        }

        /// <summary>
        /// Appropriate action to take if this event is received on the child process
        /// </summary>
        internal override void NodeAction(Node node, LocalNode localNode)
        {
            for (int i = 0; i < buildRequests.Length; i++)
            {
                node.PostBuildRequest(buildRequests[i]);
            }
        }
        #endregion

        #region Data
        private BuildRequest[] buildRequests;
        #endregion

        // For testing
        #region Properties
        internal BuildRequest[] BuildRequests
        {
            get
            {
                return buildRequests;
            }
        }
        #endregion 

        #region CustomSerializationToStream
        internal override void WriteToStream(BinaryWriter writer)
        {
            ErrorUtilities.VerifyThrow(buildRequests != null, "buildRequests should not be null");
            base.WriteToStream(writer);
            writer.Write(buildRequests.Length);
            foreach (BuildRequest request in buildRequests)
            {
                request.WriteToStream(writer);
            }
        }

        internal override void CreateFromStream(BinaryReader reader)
        {
            base.CreateFromStream(reader);
            int numberOfBuildRequests = reader.ReadInt32();
            buildRequests = new BuildRequest[numberOfBuildRequests];
            for (int i = 0; i < numberOfBuildRequests; i++)
            {
                buildRequests[i] = BuildRequest.CreateFromStream(reader);
            }
        }
        #endregion
    }
    #endregion

    #region Wrapper for PostBuildResult
    internal class LocalCallDescriptorForPostBuildResult : LocalCallDescriptor
    {
        #region Constructors

        internal LocalCallDescriptorForPostBuildResult()
        {
        }

        internal LocalCallDescriptorForPostBuildResult(BuildResult buildResult)
            : base(LocalCallType.PostBuildResult)
        {
            this.buildResult = buildResult;
        }

        #endregion

        #region Methods
        internal override void HostAction(IEngineCallback engineCallback, LocalNodeProvider nodeProvider, int nodeId)
        {
            engineCallback.PostBuildResultToHost(buildResult);
        }

        /// <summary>
        /// Appropriate action to take if this event is received on the child process
        /// </summary>
        internal override void NodeAction(Node node, LocalNode localNode)
        {
            node.PostBuildResult(buildResult);
        }
        #endregion

        #region Data
        private BuildResult buildResult;
        #endregion

        // For testing
        #region Properties
        internal BuildResult ResultOfBuild
        {
            get
            {
                return buildResult;
            }
        }
        #endregion

        #region CustomSerializationToStream
        internal override void WriteToStream(BinaryWriter writer)
        {
            ErrorUtilities.VerifyThrow(buildResult != null, "buildResult should not be null");
            base.WriteToStream(writer);
            buildResult.WriteToStream(writer);
        }

        internal override void CreateFromStream(BinaryReader reader)
        {
            base.CreateFromStream(reader);
            buildResult = BuildResult.CreateFromStream(reader);
        }
        #endregion
    }
    #endregion

    #region Wrapper for PostLoggingMessagesToHost
    internal class LocalCallDescriptorForPostLoggingMessagesToHost : LocalCallDescriptor
    {
        #region Constructors
        internal LocalCallDescriptorForPostLoggingMessagesToHost()
        {
        }

        internal LocalCallDescriptorForPostLoggingMessagesToHost(NodeLoggingEvent[] buildEvents)
            : base(LocalCallType.PostLoggingMessagesToHost)
        {
            this.buildEvents = buildEvents;
        }
        #endregion

        #region Methods
        internal override void HostAction(IEngineCallback engineCallback, LocalNodeProvider nodeProvider, int nodeId)
        {
            engineCallback.PostLoggingMessagesToHost(nodeId, buildEvents);
        }
        #endregion

        #region Data
        private NodeLoggingEvent[] buildEvents;
        #endregion

        // For testing
        #region Properties
        internal NodeLoggingEvent[] BuildEvents
        {
            get
            {
                return buildEvents;
            }
        }
        #endregion

        #region CustomSerializationToStream
        internal void WriteToStream(BinaryWriter writer, Hashtable loggingTypeCache)
        {
            ErrorUtilities.VerifyThrow(buildEvents != null, "buildRequests should not be null");
            base.WriteToStream(writer);

            writer.Write((Int32)buildEvents.Length);
            foreach (NodeLoggingEvent nodeEvent in buildEvents)
            {
                if (nodeEvent.GetType() == typeof(NodeLoggingEvent))
                {
                    writer.Write((byte)0);
                }
                else
                {
                    writer.Write((byte)1);
                }
                nodeEvent.WriteToStream(writer, loggingTypeCache);
            }
        }

        internal void CreateFromStream(BinaryReader reader, Hashtable loggingTypeCache)
        {
            base.CreateFromStream(reader);

            int numberOfNodeEvents = reader.ReadInt32();
            buildEvents = new NodeLoggingEvent[numberOfNodeEvents];

            for (int i = 0; i < numberOfNodeEvents; i++)
            {
                NodeLoggingEvent e;
                if (reader.ReadByte() == 0)
                {
                    e = new NodeLoggingEvent();
                }
                else
                {
                    e = new NodeLoggingEventWithLoggerId();
                }
                e.CreateFromStream(reader, loggingTypeCache);
                buildEvents[i] = e;
            }
        }
        #endregion

    }
    #endregion

    #region Wrapper for UpdateNodeSettings
    internal class LocalCallDescriptorForUpdateNodeSettings : LocalCallDescriptor
    {
        #region Constructors
        internal LocalCallDescriptorForUpdateNodeSettings()
        {
        }

        internal LocalCallDescriptorForUpdateNodeSettings
        (
            bool enableLogOnlyCriticalEvents,
            bool enableCentralizedLogging,
            bool useBreadthFirstTraversal
        )
            : base(LocalCallType.UpdateNodeSettings)
        {
            this.logOnlyCriticalEvents = enableLogOnlyCriticalEvents;
            this.centralizedLogging = enableCentralizedLogging;
            this.useBreadthFirstTraversal = useBreadthFirstTraversal;
        }
        #endregion

        #region Methods
        /// <summary>
        /// UNDONE - need to verified after logging spec
        /// </summary>
        internal override void NodeAction(Node node, LocalNode localNode)
        {
            ErrorUtilities.VerifyThrowArgumentNull(node, "node is null");
            node.UpdateNodeSettings(logOnlyCriticalEvents, centralizedLogging, useBreadthFirstTraversal);
        }
        #endregion

        #region Data
        private bool logOnlyCriticalEvents;
        private bool centralizedLogging;
        private bool useBreadthFirstTraversal;
        #endregion

        // For testing
        #region Properties
        internal bool LogOnlyCriticalEvents
        {
            get
            {
                return logOnlyCriticalEvents;
            }
        }

        internal bool CentralizedLogging
        {
            get
            {
                return centralizedLogging;
            }
        }

        internal bool UseBreadthFirstTraversal
        {
            get
            {
                return useBreadthFirstTraversal;
            }
        }
        #endregion

        #region CustomSerializationToStream
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            writer.Write(logOnlyCriticalEvents);
            writer.Write(centralizedLogging);
            writer.Write(useBreadthFirstTraversal);
        }

        internal override void CreateFromStream(BinaryReader reader)
        {
            base.CreateFromStream(reader);
            logOnlyCriticalEvents = reader.ReadBoolean();
            centralizedLogging = reader.ReadBoolean();
            useBreadthFirstTraversal = reader.ReadBoolean();
        }
        #endregion

    }
    #endregion

    #region Wrapper for ShutdownNode
    internal class LocalCallDescriptorForShutdownNode : LocalCallDescriptor
    {
        #region Constructors
        internal LocalCallDescriptorForShutdownNode()
        {
        }

        internal LocalCallDescriptorForShutdownNode(Node.NodeShutdownLevel shutdownLevel, bool exitProcess)
            : base(LocalCallType.ShutdownNode)
        {
            this.exitProcess = exitProcess;
            this.shutdownLevel = shutdownLevel;
        }
        #endregion

        #region Methods
        internal override void NodeAction(Node node, LocalNode localNode)
        {
            localNode.ShutdownNode(shutdownLevel, exitProcess, false);
        }
        #endregion

        #region Data
        private bool exitProcess;
        private Node.NodeShutdownLevel shutdownLevel;
        #endregion

        // For testing
        #region Properties
        internal bool ExitProcess
        {
            get
            {
                return exitProcess;
            }
        }

        internal Node.NodeShutdownLevel ShutdownLevel
        {
            get
            {
                return shutdownLevel;
            }
        }
        #endregion

        #region CustomSerializationToStream
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            writer.Write(exitProcess);
            writer.Write((Int32)shutdownLevel);
        }

        internal override void CreateFromStream(BinaryReader reader)
        {
            base.CreateFromStream(reader);
            exitProcess = reader.ReadBoolean();
            shutdownLevel = (Node.NodeShutdownLevel)reader.ReadInt32();
        }
        #endregion
    }
    #endregion

    #region Wrapper for ShutdownComplete
    internal class LocalCallDescriptorForShutdownComplete : LocalCallDescriptor
    {
        #region Constructors
        internal LocalCallDescriptorForShutdownComplete()
        {
        }

        internal LocalCallDescriptorForShutdownComplete(Node.NodeShutdownLevel shutdownLevel, int totalTaskTime )
            : base(LocalCallType.ShutdownComplete)
        {
            this.shutdownLevel = shutdownLevel;
            this.totalTaskTime = totalTaskTime;
        }
        #endregion

        #region Methods
        internal override void HostAction(IEngineCallback engineCallback, LocalNodeProvider nodeProvider, int nodeId)
        {
            nodeProvider.RecordNodeResponse(nodeId, shutdownLevel, totalTaskTime);
        }
        #endregion

        #region Data
        private Node.NodeShutdownLevel shutdownLevel;
        private int totalTaskTime;
        #endregion

        // For testing
        #region Properties
        internal Node.NodeShutdownLevel ShutdownLevel
        {
            get
            {
                return shutdownLevel;
            }
        }
        #endregion


        #region CustomSerializationToStream
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            writer.Write((Int32)shutdownLevel);
            writer.Write((Int32)totalTaskTime);
        }

        internal override void CreateFromStream(BinaryReader reader)
        {
            base.CreateFromStream(reader);
            shutdownLevel = (Node.NodeShutdownLevel)reader.ReadInt32();
            totalTaskTime = reader.ReadInt32(); 
        }
        #endregion
    }
    #endregion

    #region Wrapper for InitializeNode
    /// <summary>
    /// This class wraps a call to initialize a local node by passing it a new environment and an
    /// nodeid so that it can instantiate a node class.
    /// </summary>
    internal class LocalCallDescriptorForInitializeNode : LocalCallDescriptor
    {
        #region Constructors
        internal LocalCallDescriptorForInitializeNode()
        {
        }

        internal LocalCallDescriptorForInitializeNode
        (
            Hashtable environmentVariablesToSend, 
            LoggerDescription[] nodeLoggers,
            int nodeId,
            BuildPropertyGroup parentGlobalProperties,
            ToolsetDefinitionLocations toolsetSearchLocations,
            int parentProcessId,
            string parentStartupDirectory
        )
            : base(LocalCallType.InitializeNode)
        {
            this.environmentVariables = environmentVariablesToSend;
            this.parentGlobalProperties = parentGlobalProperties;
            this.toolsetSearchLocations = toolsetSearchLocations;
            this.nodeLoggers = nodeLoggers;
            this.nodeId = nodeId;
            this.parentProcessId = parentProcessId;
            this.parentStartupDirectory = parentStartupDirectory;
        }
        #endregion

        #region Methods
        internal override void NodeAction(Node node, LocalNode localNode)
        {
            localNode.Activate(environmentVariables, nodeLoggers, nodeId, parentGlobalProperties, 
                               toolsetSearchLocations, parentProcessId, parentStartupDirectory);
        }
        #endregion

        #region Data
        private Hashtable environmentVariables;
        private LoggerDescription[] nodeLoggers;
        private int nodeId;
        private BuildPropertyGroup parentGlobalProperties;
        private ToolsetDefinitionLocations toolsetSearchLocations;
        private int parentProcessId;
        private string parentStartupDirectory;
        #endregion

        // For testing
        #region Properties
        internal Hashtable EnvironmentVariables
        {
            get
            {
                return environmentVariables;
            }
        }
        internal LoggerDescription[] NodeLoggers
        {
            get
            {
                    return nodeLoggers;
            }
        }
        internal int NodeId
        {
            get
            {
                return nodeId;
            }
        }
        internal BuildPropertyGroup ParentGlobalProperties
        {
            get
            {
                return parentGlobalProperties;
            }
        }
        internal ToolsetDefinitionLocations ToolsetSearchLocations
        {
            get
            {
                return toolsetSearchLocations;
            }
        }
        internal int ParentProcessId
        {
            get
            {
                return parentProcessId;
            }
        }
        #endregion

        #region CustomSerializationToStream
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            #region EnvironmentVariables
            writer.Write((Int32)environmentVariables.Count);
            foreach (string key in environmentVariables.Keys)
            {
                writer.Write(key);
                writer.Write((string)environmentVariables[key]);
            }
            #endregion
            #region NodeLoggers
            if (nodeLoggers == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write((Int32)nodeLoggers.Length);
                for (int i = 0; i < nodeLoggers.Length; i++)
                {
                    nodeLoggers[i].WriteToStream(writer);
                }
            }
            #endregion
            writer.Write((Int32)nodeId);
            writer.Write((Int32)parentProcessId);
            #region ParentGlobalProperties
            if (parentGlobalProperties == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                parentGlobalProperties.WriteToStream(writer);
            }
            #endregion
            writer.Write((byte)toolsetSearchLocations);
            writer.Write(parentStartupDirectory);
        }

        internal override void CreateFromStream(BinaryReader reader)
        {
            base.CreateFromStream(reader);
            #region EnvironmentVariables
            int numberOfVariables = reader.ReadInt32();
            environmentVariables = new Hashtable(numberOfVariables);
            for (int i = 0; i < numberOfVariables; i++)
            {
                string key = reader.ReadString();
                string variable = reader.ReadString();
                environmentVariables.Add(key, variable);
            }
            #endregion
            #region NodeLoggers
            if (reader.ReadByte() == 0)
            {
                nodeLoggers = null;
            }
            else
            {
                int numberOfLoggers = reader.ReadInt32();
                nodeLoggers = new LoggerDescription[numberOfLoggers];
                for (int i = 0; i < numberOfLoggers; i++)
                {
                    LoggerDescription logger = new LoggerDescription();
                    logger.CreateFromStream(reader);
                    nodeLoggers[i] = logger;
                }
            }
            #endregion
            nodeId = reader.ReadInt32();
            parentProcessId = reader.ReadInt32();
            #region ParentGlobalProperties
            if (reader.ReadByte() == 0)
            {
                parentGlobalProperties = null;
            }
            else
            {
                parentGlobalProperties = new BuildPropertyGroup();
                parentGlobalProperties.CreateFromStream(reader);
            }
            #endregion
            toolsetSearchLocations = (ToolsetDefinitionLocations)reader.ReadByte();
            parentStartupDirectory = (string)reader.ReadString();
          }
        #endregion
    }
    #endregion

    #region Wrapper for InitializationComplete
    internal class LocalCallDescriptorForInitializationComplete : LocalCallDescriptor
    {
        #region Constructors
        internal LocalCallDescriptorForInitializationComplete()
        {
        }

        internal LocalCallDescriptorForInitializationComplete(int processId)
            : base(LocalCallType.InitializationComplete)
        {
            this.processId = processId;
        }
        #endregion

        #region Methods
        internal override void HostAction(IEngineCallback engineCallback, LocalNodeProvider nodeProvider, int nodeId)
        {
            nodeProvider.SetNodeProcessId(processId, nodeId);
        }
        #endregion

        #region Data
        private int processId = 0;
        #endregion

        //For Testing
        #region Properties
        internal int ProcessId
        {
            get
            {
                return processId;
            }
        }
        #endregion

        #region CustomSerializationToStream
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            writer.Write((Int32)processId);
        }

        internal override void CreateFromStream(BinaryReader reader)
        {
            base.CreateFromStream(reader);
            processId = reader.ReadInt32();
        }
        #endregion
    }
    #endregion

    #region Wrapper for RequestStatus
    internal class LocalCallDescriptorForRequestStatus : LocalCallDescriptor
    {
        #region Constructors
        internal LocalCallDescriptorForRequestStatus()
        {
        }

        internal LocalCallDescriptorForRequestStatus(int requestId)
            : base(LocalCallType.RequestStatus)
        {
            this.requestId = requestId;
        }
        #endregion

        #region Methods
        internal override void NodeAction(Node node, LocalNode localNode)
        {
            node.RequestStatus(requestId);
        }
        #endregion

        #region Data
        private int requestId = 0;
        #endregion

        // For testing
        #region Properties
        internal int RequestId
        {
            get
            {
                return requestId;
            }
        }
        #endregion

        #region CustomSerializationToStream
        internal override void WriteToStream(BinaryWriter writer)
        {
             base.WriteToStream(writer);
             writer.Write((Int32)requestId);
        }

        internal override void CreateFromStream(BinaryReader reader)
        {
            base.CreateFromStream(reader);
            requestId = reader.ReadInt32();
        }
        #endregion
    }
    #endregion

    #region Wrapper for PostStatus
    internal class LocalCallDescriptorForPostStatus : LocalCallDescriptor
    {
        #region Constructors
        internal LocalCallDescriptorForPostStatus()
        {
        }

        internal LocalCallDescriptorForPostStatus(NodeStatus nodeStatus)
            : base(LocalCallType.PostStatus)
        {
            this.nodeStatus = nodeStatus;
        }
        #endregion

        #region Methods
        internal override void HostAction(IEngineCallback engineCallback, LocalNodeProvider nodeProvider, int nodeId)
        {
            engineCallback.PostStatus(nodeId, nodeStatus, false);
        }
        #endregion

        #region Data
        private NodeStatus nodeStatus;
        #endregion

        // For testing
        #region Properties
        internal NodeStatus StatusOfNode
        {
            get
            {
                return nodeStatus;
            }
        }
        #endregion

        #region CustomSerializationToStream
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            if (nodeStatus == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                nodeStatus.WriteToStream(writer);
            }
        }

        internal override void CreateFromStream(BinaryReader reader)
        {
            base.CreateFromStream(reader);
            if (reader.ReadByte() == 0)
            {
                nodeStatus = null;
            }
            else
            {
                nodeStatus = NodeStatus.CreateFromStream(reader);
            }
        }
        #endregion
    }
    #endregion

    #region Wrapper for PostIntrospectorCommand
    internal class LocalCallDescriptorForPostIntrospectorCommand : LocalCallDescriptor
    {
        #region Constructors
        internal LocalCallDescriptorForPostIntrospectorCommand(TargetInProgessState child, TargetInProgessState parent)
            : base(LocalCallType.PostIntrospectorCommand)
        {
            this.child  = child;
            this.parent = parent;
        }
        #endregion

        #region Methods
        internal override void NodeAction(Node node, LocalNode localNode)
        {
            node.Introspector.BreakCycle(child, parent);
        }
        #endregion

        #region Data
        private TargetInProgessState child;
        private TargetInProgessState parent;
        #endregion

        #region CustomSerializationToStream
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            child.WriteToStream(writer);
            parent.WriteToStream(writer);
        }

        internal override void CreateFromStream(BinaryReader reader)
        {
            base.CreateFromStream(reader);
            child = new TargetInProgessState();
            child.CreateFromStream(reader);
            parent = new TargetInProgessState();
            parent.CreateFromStream(reader);
        }
        #endregion
    }
    #endregion

    #region Wrapper for PostCacheEntriesToHost
    internal class LocalCallDescriptorForPostingCacheEntriesToHost : LocalCallDescriptor
    {
        #region Constructors
        internal LocalCallDescriptorForPostingCacheEntriesToHost()
        {
        }

        internal LocalCallDescriptorForPostingCacheEntriesToHost(CacheEntry[] entries, string scopeName, BuildPropertyGroup scopeProperties, string scopeToolsVersion, CacheContentType cacheContentType)
            : base(LocalCallType.PostCacheEntriesToHost)
        {
            this.entries = entries;
            this.scopeName = scopeName;
            this.scopeProperties = scopeProperties;
            this.scopeToolsVersion = scopeToolsVersion;
            this.cacheContentType = cacheContentType;
            this.exception = null;
        }
        #endregion

        // For testing
        #region Properties
        internal override bool NeedsReply
        {
            get
            {
                return true;
            }
        }

        internal CacheEntry[] Entries
        {
            get
            {
                return entries;
            }
        }
        internal string ScopeName
        {
            get
            {
                return scopeName;
            }
        }
        internal CacheContentType ContentType
        {
            get
            {
                return cacheContentType;
            }
        }
        internal BuildPropertyGroup ScopeProperties
        {
            get
            {
                return scopeProperties;
            }
        }
        internal string ScopeToolsVersion
        {
            get
            {
                return scopeToolsVersion;
            }
        }
        #endregion

        #region Methods
        internal override void HostAction(IEngineCallback engineCallback, LocalNodeProvider nodeProvider, int nodeId)
        {
            exception = engineCallback.PostCacheEntriesToHost(nodeId, this.entries, this.scopeName, this.scopeProperties, this.scopeToolsVersion, this.cacheContentType);
        }

        internal override LocalReplyCallDescriptor ReplyFromHostAction()
        {
            return new LocalReplyCallDescriptor(this.CallNumber, this.exception);
        }
        #endregion

        #region Data
        private CacheEntry[] entries;
        private string scopeName;
        private string scopeToolsVersion;
        private BuildPropertyGroup scopeProperties;
        private CacheContentType cacheContentType;
        private Exception exception;
        #endregion

        #region CustomSerializationToStream
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            #region Entries
            if (entries == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write((Int32)entries.Length);
                for (int i = 0; i < entries.Length; i++)
                {
                   CacheEntryCustomSerializer.WriteToStream(entries[i], writer);
                }
            }
            #endregion
            #region ScopeName
            if (scopeName == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(scopeName);
            }
            #endregion
            #region ScopeProperties
            if (scopeProperties == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                scopeProperties.WriteToStream(writer);
            }
            #endregion
            #region ScopeToolsVersion
            if (scopeToolsVersion == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(scopeToolsVersion);
            }
            #endregion
            writer.Write((byte)cacheContentType);
        }

        internal override void CreateFromStream(BinaryReader reader)
        {
            base.CreateFromStream(reader);
            #region Entries
            if (reader.ReadByte() == 0)
            {
                entries = null;
            }
            else
            {
                int numberOfEntries = reader.ReadInt32();
                entries = new CacheEntry[numberOfEntries];
                for (int i = 0; i < entries.Length; i++)
                {
                    entries[i] = CacheEntryCustomSerializer.CreateFromStream(reader);
                }
            }
            #endregion
            #region ScopeName
            if (reader.ReadByte() == 0)
            {
                scopeName = null;
            }
            else
            {
                scopeName = reader.ReadString();
            }
            #endregion
            #region ScopeProperties
            if (reader.ReadByte() == 0)
            {
                scopeProperties = null;
            }
            else
            {
                scopeProperties = new BuildPropertyGroup();
                scopeProperties.CreateFromStream(reader);
            }
            #endregion
            #region ScopeToolsVersion
            if (reader.ReadByte() == 0)
            {
                scopeToolsVersion = null;
            }
            else
            {
                scopeToolsVersion = reader.ReadString();
            }
            #endregion
            cacheContentType = (CacheContentType)reader.ReadByte();
        }
        #endregion
    }
    #endregion

    #region Wrapper for GetCacheEntriesFromHost
    internal class LocalCallDescriptorForGettingCacheEntriesFromHost : LocalCallDescriptor
    {
        #region Constructors
        internal LocalCallDescriptorForGettingCacheEntriesFromHost()
        {
        }

        internal LocalCallDescriptorForGettingCacheEntriesFromHost(string[] names, string scopeName, BuildPropertyGroup scopeProperties, string scopeToolsVersion, CacheContentType cacheContentType)
            : base(LocalCallType.GetCacheEntriesFromHost)
        {
            this.names = names;
            this.scopeName = scopeName;
            this.scopeProperties = scopeProperties;
            this.scopeToolsVersion = scopeToolsVersion;
            this.cacheContentType = cacheContentType;
        }
        #endregion

        // For testing
        #region Properties
        internal override bool NeedsReply
        {
            get
            {
                return true;
            }
        }
        internal string[] Names
        {
            get
            {
                return names;
            }
        }
        internal string ScopeName
        {
            get
            {
                return scopeName;
            }
        }

        internal BuildPropertyGroup ScopeProperties
        {
            get
            {
                return scopeProperties;
            }
        }

        internal CacheContentType ContentType
        {
            get
            {
                return cacheContentType;
            }
        }

        internal string ScopeToolsVersion
        {
            get
            {
                return scopeToolsVersion;
            }
        }

        #endregion

        #region Methods
        internal override void HostAction(IEngineCallback engineCallback, LocalNodeProvider nodeProvider, int nodeId)
        {
            entries = engineCallback.GetCachedEntriesFromHost(nodeId, this.names, this.scopeName, this.scopeProperties, this.scopeToolsVersion, this.cacheContentType);
        }

        internal override LocalReplyCallDescriptor ReplyFromHostAction()
        {
            return new LocalReplyCallDescriptor(this.CallNumber, this.entries);
        }
        #endregion

        #region Data
        private string[] names;
        private string scopeName;
        private string scopeToolsVersion;
        private BuildPropertyGroup scopeProperties;
        private CacheContentType cacheContentType;
        // The actual reply value is only serialized in the reply call descriptor, no need to serialize it here.
        private CacheEntry[] entries;
        #endregion

        #region CustomSerializationToStream
        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);
            #region Names
            if (names == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write((Int32)names.Length);
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i] == null)
                    {
                        writer.Write((byte)0);
                    }
                    else
                    {
                        writer.Write((byte)1);
                        writer.Write(names[i]);
                    }
                }
            }
            #endregion
            #region ScopeName
            if (scopeName == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(scopeName);
            }
            #endregion 
            #region ScopeProperties
            if (scopeProperties == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                scopeProperties.WriteToStream(writer);
            }
            #endregion
            #region ScopeToolsVersion
            if (scopeToolsVersion == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(scopeToolsVersion);
            }
            #endregion 
            writer.Write((byte)cacheContentType);
        }
        internal override void CreateFromStream(BinaryReader reader)
        {
            base.CreateFromStream(reader);
            #region Names
            if (reader.ReadByte() == 0)
            {
                names = null;
            }
            else
            {
                int numberOfEntries = reader.ReadInt32();
                names = new string[numberOfEntries];
                for (int i = 0; i < names.Length; i++)
                {
                    if (reader.ReadByte() != 0)
                    {
                        names[i] = reader.ReadString();
                    }
                    else
                    {
                        names[i] = null;
                    }
                }
            }
            #endregion
            #region ScopeName
            if (reader.ReadByte() == 0)
            {
                scopeName = null;
            }
            else
            {
                scopeName = reader.ReadString();
            }
            #endregion
            #region ScopeProperties
            if (reader.ReadByte() == 0)
            {
                scopeProperties = null;
            }
            else
            {
                scopeProperties = new BuildPropertyGroup();
                scopeProperties.CreateFromStream(reader);
            }
            #endregion
            #region ScopeToolsVersion
            if (reader.ReadByte() == 0)
            {
                scopeToolsVersion = null;
            }
            else
            {
                scopeToolsVersion = reader.ReadString();
            }
            #endregion
            cacheContentType = (CacheContentType)reader.ReadByte();
        }
        #endregion
        
    }
    #endregion

    #region Enums

    /// <summary>
    /// This enum describes the call types used in the local node provider
    /// </summary>
    internal enum LocalCallType
    {
        /// <summary>
        /// This call type corresponds to an array of build requests
        /// </summary>
        PostBuildRequests = 0,
        /// <summary>
        /// This call type corresponds to a single build result
        /// </summary>
        PostBuildResult = 1,
        /// <summary>
        /// This call type corresponds to an array of messages
        /// from which messages originated
        /// </summary>
        PostLoggingMessagesToHost = 2,
        /// <summary>
        /// Call type to update the settings one the node
        /// </summary>
        UpdateNodeSettings = 3,
        /// <summary>
        /// Call type for request status from the node
        /// </summary>
        RequestStatus = 4,
        /// <summary>
        /// Call type for request status from the node
        /// </summary>
        PostStatus = 5,
        /// <summary>
        /// Call type for initializing the node
        /// </summary>
        InitializeNode = 6,
        /// <summary>
        /// Call type for the node to indicate that it has initialized successfully
        /// </summary>
        InitializationComplete = 7,
        /// <summary>
        /// Call type for shutting down the node
        /// </summary>
        ShutdownNode = 8,
        /// <summary>
        /// Call type for the node to indicate that it has shutdown 
        /// </summary>
        ShutdownComplete = 9,
        /// <summary>
        /// Call type to post an introspector command
        /// </summary>
        PostIntrospectorCommand = 10,
        /// <summary>
        /// This call type corresponds to a single string send from parent to child
        /// </summary>
        GenericSingleObjectReply = 11,
        /// <summary>
        /// Call type for posting cache entries to a node
        /// </summary>
        PostCacheEntriesToHost = 12,
        /// <summary>
        /// Call type for retrieving cache entries from a node
        /// </summary>
        GetCacheEntriesFromHost = 13
    }
    #endregion
}
