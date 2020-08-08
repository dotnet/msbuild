// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.BackEnd
{
    #region Enums
    /// <summary>
    /// Enumeration of all of the packet types used for communication.
    /// </summary>
    internal enum NodePacketType : byte
    {
        /// <summary>
        /// <para>
        /// Notifies the Node to set a configuration for a particular build.  This is sent before
        /// any BuildRequests are made and will not be sent again for a particular build.  This instructs
        /// the node to prepare to receive build requests.
        /// </para>
        /// <para>
        /// Contains:
        /// Build ID
        /// Environment variables
        /// Logging Services Configuration
        /// Node ID
        /// Default Global Properties
        /// Toolset Definition Locations
        /// Startup Directory
        /// UI Culture Information
        /// App Domain Configuration XML
        /// </para>
        /// </summary>
        NodeConfiguration,

        /// <summary>
        /// <para>
        /// A BuildRequestConfiguration object.  
        /// When sent TO a node, this informs the node of a build configuration.
        /// When sent FROM a node, this requests a BuildRequestConfigurationResponse to map the configuration to the
        /// appropriate global configuration ID.
        /// </para>
        /// <para>
        /// Contents:
        /// Configuration ID
        /// Project Filename
        /// Project Properties
        /// Project Tools Version
        /// </para>
        /// </summary>
        BuildRequestConfiguration,

        /// <summary>
        /// <para>A response to a request to map a build configuration</para>
        /// <para>
        /// Contents:
        /// Node Configuration ID
        /// Global Configuration ID
        /// </para>
        /// </summary>
        BuildRequestConfigurationResponse,

        /// <summary>
        /// <para>Information about a project that has been loaded by a node.</para>
        /// <para>
        /// Contents:
        /// Global Configuration ID
        /// Initial Targets
        /// Default Targets
        /// </para>
        /// </summary>
        ProjectLoadInfo,

        /// <summary>
        /// <para>Packet used to inform the scheduler that a node's active build request is blocked.</para>
        /// <para>
        /// Contents:
        /// Build Request ID
        /// Active Targets
        /// Blocked Target, if any
        /// Child Requests, if any
        /// </para>
        /// </summary>
        BuildRequestBlocker,

        /// <summary>
        /// <para>Packet used to unblocked a blocked request on a node.</para>
        /// <para>
        /// Contents:
        /// Build Request ID
        /// Build Results for child requests, if any.
        /// </para>
        /// </summary>
        BuildRequestUnblocker,

        /// <summary>
        /// <para>A BuildRequest object</para>
        /// <para>
        /// Contents:
        /// Build Request ID
        /// Configuration ID
        /// Project Instance ID
        /// Targets
        /// </para>
        /// </summary>
        BuildRequest,

        /// <summary>
        /// <para>A BuildResult object</para>
        /// <para>
        /// Contents:
        /// Build ID
        /// Project Instance ID
        /// Targets
        /// Outputs (per Target)
        /// Results (per Target)
        /// </para>
        /// </summary>
        BuildResult,

        /// <summary>
        /// <para>A logging message.</para>
        /// <para>
        /// Contents:
        /// Build Event Type
        /// Build Event Args
        /// </para>
        /// </summary>
        LogMessage,

        /// <summary>
        /// <para>Informs the node that the build is complete.  </para>
        /// <para>
        /// Contents:
        /// Prepare For Reuse
        /// </para>
        /// </summary>
        NodeBuildComplete,

        /// <summary>
        /// <para>
        /// Reported by the node (or node provider) when a node has terminated.  This is the final packet that will be received
        /// from a node.
        /// </para>
        /// <para>
        /// Contents:
        /// Reason
        /// </para>
        /// </summary>
        NodeShutdown,

        /// <summary>
        /// <para>
        /// Notifies the task host to set the task-specific configuration for a particular task execution. 
        /// This is sent in place of NodeConfiguration and gives the task host all the information it needs
        /// to set itself up and execute the task that matches this particular configuration. 
        /// </para>
        /// <para>
        /// Contains:
        /// Node ID (of parent MSBuild node, to make the logging work out) 
        /// Startup directory
        /// Environment variables 
        /// UI Culture information
        /// App Domain Configuration XML
        /// Task name
        /// Task assembly location
        /// Parameter names and values to set to the task prior to execution
        /// </para>
        /// </summary>
        TaskHostConfiguration,

        /// <summary>
        /// <para>
        /// Informs the parent node that the task host has finished executing a 
        /// particular task.  Does not need to contain identifying information 
        /// about the task, because the task host will only ever be connected to 
        /// one parent node at a a time, and will only ever be executing one task 
        /// for that node at any one time.  
        /// </para>
        /// <para>
        /// Contents:
        /// Task result (success / failure)
        /// Resultant parameter values (for output gathering)
        /// </para>
        /// </summary>
        TaskHostTaskComplete,

        /// <summary>
        /// <para>
        /// Message sent from the node to its paired task host when a task that 
        /// supports ICancellableTask is cancelled.  
        /// </para>
        /// <para>
        /// Contents:
        /// (nothing) 
        /// </para>
        /// </summary>
        TaskHostTaskCancelled,

        /// <summary>
        /// Message sent from a node when it needs to have an SDK resolved.
        /// </summary>
        ResolveSdkRequest,

        /// <summary>
        /// Message sent from back to a node when an SDK has been resolved.
        /// </summary>
        ResolveSdkResponse,
    }
    #endregion

    /// <summary>
    /// This interface represents a packet which may be transmitted using an INodeEndpoint.
    /// Implementations define the serialized form of the data.
    /// </summary>
    internal interface INodePacket : ITranslatable
    {
        #region Properties
        /// <summary>
        /// The type of the packet.  Used to reconstitute the packet using the correct factory.
        /// </summary>
        NodePacketType Type
        {
            get;
        }
        #endregion
    }
}
