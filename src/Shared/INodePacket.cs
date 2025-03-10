// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.BackEnd
{
    #region Enums
    /// <summary>
    /// Enumeration of all of the packet types used for communication.
    /// </summary>
    internal enum NodePacketType : byte
    {
        /// <summary>
        /// Notifies the Node to set a configuration for a particular build.  This is sent before
        /// any BuildRequests are made and will not be sent again for a particular build.  This instructs
        /// the node to prepare to receive build requests.
        ///
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
        /// </summary>
        NodeConfiguration,

        /// <summary>
        /// A BuildRequestConfiguration object.
        /// When sent TO a node, this informs the node of a build configuration.
        /// When sent FROM a node, this requests a BuildRequestConfigurationResponse to map the configuration to the
        /// appropriate global configuration ID.
        ///
        /// Contents:
        /// Configuration ID
        /// Project Filename
        /// Project Properties
        /// Project Tools Version
        /// </summary>
        BuildRequestConfiguration,

        /// <summary>
        /// A response to a request to map a build configuration
        ///
        /// Contents:
        /// Node Configuration ID
        /// Global Configuration ID
        /// </summary>
        BuildRequestConfigurationResponse,

        /// <summary>
        /// Information about a project that has been loaded by a node.
        ///
        /// Contents:
        /// Global Configuration ID
        /// Initial Targets
        /// Default Targets
        /// </summary>
        ProjectLoadInfo,

        /// <summary>
        /// Packet used to inform the scheduler that a node's active build request is blocked.
        ///
        /// Contents:
        /// Build Request ID
        /// Active Targets
        /// Blocked Target, if any
        /// Child Requests, if any
        /// </summary>
        BuildRequestBlocker,

        /// <summary>
        /// Packet used to unblocked a blocked request on a node.
        ///
        /// Contents:
        /// Build Request ID
        /// Build Results for child requests, if any.
        /// </summary>
        BuildRequestUnblocker,

        /// <summary>
        /// A BuildRequest object
        ///
        /// Contents:
        /// Build Request ID
        /// Configuration ID
        /// Project Instance ID
        /// Targets
        /// </summary>
        BuildRequest,

        /// <summary>
        /// A BuildResult object
        ///
        /// Contents:
        /// Build ID
        /// Project Instance ID
        /// Targets
        /// Outputs (per Target)
        /// Results (per Target)
        /// </summary>
        BuildResult,

        /// <summary>
        /// A logging message.
        ///
        /// Contents:
        /// Build Event Type
        /// Build Event Args
        /// </summary>
        LogMessage,

        /// <summary>
        /// Informs the node that the build is complete.
        ///
        /// Contents:
        /// Prepare For Reuse
        /// </summary>
        NodeBuildComplete,

        /// <summary>
        /// Reported by the node (or node provider) when a node has terminated.  This is the final packet that will be received
        /// from a node.
        ///
        /// Contents:
        /// Reason
        /// </summary>
        NodeShutdown,

        /// <summary>
        /// Notifies the task host to set the task-specific configuration for a particular task execution.
        /// This is sent in place of NodeConfiguration and gives the task host all the information it needs
        /// to set itself up and execute the task that matches this particular configuration.
        ///
        /// Contains:
        /// Node ID (of parent MSBuild node, to make the logging work out)
        /// Startup directory
        /// Environment variables
        /// UI Culture information
        /// App Domain Configuration XML
        /// Task name
        /// Task assembly location
        /// Parameter names and values to set to the task prior to execution
        /// </summary>
        TaskHostConfiguration,

        /// <summary>
        /// Informs the parent node that the task host has finished executing a
        /// particular task.  Does not need to contain identifying information
        /// about the task, because the task host will only ever be connected to
        /// one parent node at a a time, and will only ever be executing one task
        /// for that node at any one time.
        ///
        /// Contents:
        /// Task result (success / failure)
        /// Resultant parameter values (for output gathering)
        /// </summary>
        TaskHostTaskComplete,

        /// <summary>
        /// Message sent from the node to its paired task host when a task that
        /// supports ICancellableTask is cancelled.
        ///
        /// Contents:
        /// (nothing)
        /// </summary>
        TaskHostTaskCancelled,

        /// <summary>
        /// Message sent from a node when it needs to have an SDK resolved.
        /// </summary>
        ResolveSdkRequest,

        /// <summary>
        /// Message sent back to a node when an SDK has been resolved.
        /// </summary>
        ResolveSdkResponse,

        /// <summary>
        /// Message sent from a node when a task is requesting or returning resources from the scheduler.
        /// </summary>
        ResourceRequest,

        /// <summary>
        /// Message sent back to a node informing it about the resource that were granted by the scheduler.
        /// </summary>
        ResourceResponse,

        /// <summary>
        /// Message sent from a node reporting a file access.
        /// </summary>
        FileAccessReport,

        /// <summary>
        /// Message sent from a node reporting process data.
        /// </summary>
        ProcessReport,

        // Server command packets with hardcoded values that don't have the 6th bit set.
        // It is reserved for ExtendedHeaderFlag (0x40 = 0100 0000).
        // Do not set it for any other packet types to avoid conflicts.
        #region ServerNode enums 

        /// <summary>
        /// Command in form of MSBuild command line for server node - MSBuild Server.
        /// Keep this enum value constant intact as this is part of contract with dotnet CLI.
        /// </summary>
        ServerNodeBuildCommand = 0x90, // Binary: 10010000

        /// <summary>
        /// Response from server node command
        /// Keep this enum value constant intact as this is part of contract with dotnet CLI
        /// </summary>
        ServerNodeBuildResult = 0x91, // Binary: 10010001

        /// <summary>
        /// Info about server console activity.
        /// Keep this enum value constant intact as this is part of contract with dotnet CLI
        /// </summary>
        ServerNodeConsoleWrite = 0x92, // Binary: 10010010

        /// <summary>
        /// Command to cancel ongoing build.
        /// Keep this enum value constant intact as this is part of contract with dotnet CLI
        /// </summary>
        ServerNodeBuildCancel = 0x93, // Binary: 10010011

        #endregion
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

    internal static class PacketTypeExtensions
    {
        public const byte PacketVersion = 1;

        private const byte ExtendedHeaderFlag = 0x40; // Bit 6 indicates extended header with version

        /// <summary>
        /// Determines if a packet has an extended header by checking if the extended header flag is set.
        /// are never interpreted as having extended headers, even if they happen to have the flag bit set.
        /// </summary>
        /// <param name="rawType">The raw packet type byte.</param>
        /// <returns>True if the packet has an extended header, false otherwise</returns>
        public static bool HasExtendedHeader(byte rawType) => (rawType & ExtendedHeaderFlag) != 0;

        // Get base type, stripping the extended header flag
        public static NodePacketType GetNodePacketType(byte rawType) => (NodePacketType)(rawType & ~ExtendedHeaderFlag);

        // Create a type with extended header flag
        public static byte CreateExtendedHeaderType(NodePacketType type) => (byte)((byte)type | ExtendedHeaderFlag);

        // Read extended header (returns version)
        public static byte ReadVersion(Stream stream) => (byte)stream.ReadByte();

        // Write extended header with version
        public static void WriteVersion(Stream stream, byte version) => stream.WriteByte(version);
    }
}
