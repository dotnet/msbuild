// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.IO;
using Microsoft.Build.Internal;

namespace Microsoft.Build.BackEnd
{
    #region Enums

    /// <summary>
    /// Enumeration of all of the packet types used for communication.
    /// Uses lower 6 bits for packet type (0-63), upper 2 bits reserved for flags.
    /// </summary>
    internal enum NodePacketType : byte
    {
        // Mask for extracting packet type (lower 6 bits)
        TypeMask = 0x3F, // 00111111

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
        NodeConfiguration = 0x00,

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
        BuildRequestConfiguration, // 0x01

        /// <summary>
        /// A response to a request to map a build configuration
        ///
        /// Contents:
        /// Node Configuration ID
        /// Global Configuration ID
        /// </summary>
        BuildRequestConfigurationResponse, // 0x02

        /// <summary>
        /// Information about a project that has been loaded by a node.
        ///
        /// Contents:
        /// Global Configuration ID
        /// Initial Targets
        /// Default Targets
        /// </summary>
        ProjectLoadInfo, // 0x03

        /// <summary>
        /// Packet used to inform the scheduler that a node's active build request is blocked.
        ///
        /// Contents:
        /// Build Request ID
        /// Active Targets
        /// Blocked Target, if any
        /// Child Requests, if any
        /// </summary>
        BuildRequestBlocker, // 0x04

        /// <summary>
        /// Packet used to unblocked a blocked request on a node.
        ///
        /// Contents:
        /// Build Request ID
        /// Build Results for child requests, if any.
        /// </summary>
        BuildRequestUnblocker, // 0x05

        /// <summary>
        /// A BuildRequest object
        ///
        /// Contents:
        /// Build Request ID
        /// Configuration ID
        /// Project Instance ID
        /// Targets
        /// </summary>
        BuildRequest, // 0x06

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
        BuildResult, // 0x07

        /// <summary>
        /// A logging message.
        ///
        /// Contents:
        /// Build Event Type
        /// Build Event Args
        /// </summary>
        LogMessage, // 0x08

        /// <summary>
        /// Informs the node that the build is complete.
        ///
        /// Contents:
        /// Prepare For Reuse
        /// </summary>
        NodeBuildComplete, // 0x09

        /// <summary>
        /// Reported by the node (or node provider) when a node has terminated.  This is the final packet that will be received
        /// from a node.
        ///
        /// Contents:
        /// Reason
        /// </summary>
        NodeShutdown, // 0x0A

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
        TaskHostConfiguration, // 0x0B

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
        TaskHostTaskComplete, // 0x0C

        /// <summary>
        /// Message sent from the node to its paired task host when a task that
        /// supports ICancellableTask is cancelled.
        ///
        /// Contents:
        /// (nothing)
        /// </summary>
        TaskHostTaskCancelled, // 0x0D

        /// <summary>
        /// Message sent from a node when it needs to have an SDK resolved.
        /// </summary>
        ResolveSdkRequest, // 0x0E

        /// <summary>
        /// Message sent back to a node when an SDK has been resolved.
        /// </summary>
        ResolveSdkResponse, // 0x0F

        /// <summary>
        /// Message sent from a node when a task is requesting or returning resources from the scheduler.
        /// </summary>
        ResourceRequest, // 0x10

        /// <summary>
        /// Message sent back to a node informing it about the resource that were granted by the scheduler.
        /// </summary>
        ResourceResponse, // 0x11

        /// <summary>
        /// Message sent from a node reporting a file access.
        /// </summary>
        FileAccessReport, // 0x12

        /// <summary>
        /// Message sent from a node reporting process data.
        /// </summary>
        ProcessReport, // 0x13


        /// Notifies the RAR node to set a configuration for a particular build.
        RarNodeEndpointConfiguration,

        /// <summary>
        /// A request contains the inputs to the RAR task.
        /// </summary>
        RarNodeExecuteRequest, // 0x14

        /// <summary>
        /// A request contains the outputs and log events of a completed RAR task.
        /// </summary>
        RarNodeExecuteResponse, // 0x15

        // Reserve space for future core packet types (0x16-0x3B available for expansion)

        // Server command packets placed at end of safe range to maintain separation from core packets
        #region ServerNode enums 

        /// <summary>
        /// A batch of log events emitted while the RAR task is executing.
        /// </summary>
        RarNodeBufferedLogEvents,

        /// <summary>
        /// Command in form of MSBuild command line for server node - MSBuild Server.
        /// </summary>
        ServerNodeBuildCommand = 0x3C, // End of safe range

        /// <summary>
        /// Response from server node command.
        /// </summary>
        ServerNodeBuildResult = 0x3D,

        /// <summary>
        /// Info about server console activity.
        /// </summary>
        ServerNodeConsoleWrite = 0x3E,

        /// <summary>
        /// Command to cancel ongoing build.
        /// </summary>
        ServerNodeBuildCancel = 0x3F, // Last value in safe range (0x3F = 00111111)

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

    /// <summary>
    /// Provides utilities for handling node packet types and extended headers in MSBuild's distributed build system.
    /// 
    /// This class manages the communication protocol between build nodes, including:
    /// - Packet versioning for protocol compatibility
    /// - Extended header flags for enhanced packet metadata
    /// - Type extraction and manipulation for network communication
    /// 
    /// The packet format uses the upper 2 bits (6-7) for flags while preserving
    /// the lower 6 bits for the actual packet type enumeration.
    /// </summary>
    internal static class NodePacketTypeExtensions
    {
        /// <summary>
        /// Defines the communication protocol version for node communication.
        /// 
        /// Version 1: Introduced for the .NET Task Host protocol. This version
        /// excludes the translation of appDomainConfig within TaskHostConfiguration
        /// to maintain backward compatibility and reduce serialization overhead.
        /// 
        /// When incrementing this version, ensure compatibility with existing
        /// task hosts and update the corresponding deserialization logic.
        /// </summary>
        public const byte PacketVersion = 1;

        // Flag bits in upper 2 bits
        private const byte ExtendedHeaderFlag = 0x40;  // Bit 6: 01000000

        /// <summary>
        /// Determines if a packet has an extended header by checking if the extended header flag is set.
        /// Uses bit 6 which is now safely separated from packet type values.
        /// </summary>
        /// <param name="rawType">The raw packet type byte.</param>
        /// <returns>True if the packet has an extended header, false otherwise</returns>
        public static bool HasExtendedHeader(byte rawType) => (rawType & ExtendedHeaderFlag) != 0;

        /// <summary>
        /// Get base packet type, stripping all flag bits (bits 6 and 7).
        /// </summary>
        /// <param name="rawType">The raw packet type byte with potential flags.</param>
        /// <returns>The clean packet type without flag bits.</returns>
        public static NodePacketType GetNodePacketType(byte rawType) => (NodePacketType)(rawType & (byte)NodePacketType.TypeMask);

        /// <summary>
        /// Create a packet type byte with extended header flag for net task host packets.
        /// </summary>
        /// <param name="handshakeOptions">Handshake options to check.</param>
        /// <param name="type">Base packet type.</param>
        /// <param name="extendedheader">Output byte with flag set if applicable.</param>
        /// <returns>True if extended header flag was set, false otherwise.</returns>
        public static bool TryCreateExtendedHeaderType(HandshakeOptions handshakeOptions, NodePacketType type, out byte extendedheader)
        {
            if (Handshake.IsHandshakeOptionEnabled(handshakeOptions, Handshake.NetTaskHostFlags))
            {
                extendedheader = (byte)((byte)type | ExtendedHeaderFlag);
                return true;
            }

            extendedheader = (byte)type;
            return false;
        }

        /// <summary>
        /// Reads the protocol version from an extended header in the stream.
        /// This method expects the stream to be positioned at the version byte.
        /// </summary>
        /// <param name="stream">The stream to read the version byte from.</param>
        /// <returns>The protocol version byte read from the stream.</returns>
        /// <exception cref="EndOfStreamException">Thrown when the stream ends unexpectedly while reading the version.</exception>
        public static byte ReadVersion(Stream stream)
        {
            int value = stream.ReadByte();
            if (value == -1)
            {
                throw new EndOfStreamException("Unexpected end of stream while reading version");
            }

            return (byte)value;
        }

        /// <summary>
        /// Writes the protocol version byte to the extended header in the stream.
        /// This is typically called after writing a packet type with the extended header flag.
        /// </summary>
        /// <param name="stream">The stream to write the version byte to.</param>
        /// <param name="version">The protocol version to write to the stream.</param>
        public static void WriteVersion(Stream stream, byte version) => stream.WriteByte(version);
    }
}
