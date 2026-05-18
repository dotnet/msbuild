// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.TaskHost.BackEnd;

/// <summary>
/// Enumeration of all of the packet types used for communication.
/// Uses lower 6 bits for packet type (0-63), upper 2 bits reserved for flags.
/// </summary>
/// <remarks>
/// This is a reduced set of packet types used by MSBuildTaskHost.
/// It is derived from the full set of packet types used for MSBuild's internal node communication.
/// </remarks>
internal enum NodePacketType : byte
{
    // Mask for extracting packet type (lower 6 bits)
    TypeMask = 0x3F, // 00111111

    /// <summary>
    /// A logging message.
    ///
    /// Contents:
    /// Build Event Type
    /// Build Event Args
    /// </summary>
    LogMessage = 0x08,

    /// <summary>
    /// Informs the node that the build is complete.
    ///
    /// Contents:
    /// Prepare For Reuse
    /// </summary>
    NodeBuildComplete = 0x09,

    /// <summary>
    /// Reported by the node (or node provider) when a node has terminated.  This is the final packet that will be received
    /// from a node.
    ///
    /// Contents:
    /// Reason
    /// </summary>
    NodeShutdown = 0x0A,

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
    TaskHostConfiguration = 0x0B,

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
    TaskHostTaskComplete = 0x0C,

    /// <summary>
    /// Message sent from the node to its paired task host when a task that
    /// supports ICancellableTask is cancelled.
    ///
    /// Contents:
    /// (nothing)
    /// </summary>
    TaskHostTaskCancelled = 0x0D,
}

/// <summary>
/// This interface represents a packet which may be transmitted using an INodeEndpoint.
/// Implementations define the serialized form of the data.
/// </summary>
internal interface INodePacket : ITranslatable
{
    /// <summary>
    /// Gets the type of the packet.  Used to reconstitute the packet using the correct factory.
    /// </summary>
    NodePacketType Type { get; }
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
    /// Version 2: Adds support of HostServices and target name translation in TaskHostConfiguration.
    /// 
    /// When incrementing this version, ensure compatibility with existing
    /// task hosts and update the corresponding deserialization logic.
    /// </summary>
    public const byte PacketVersion = 2;

    // Flag bits in upper 2 bits
    private const byte ExtendedHeaderFlag = 0x40;  // Bit 6: 01000000

    /// <summary>
    /// Determines if a packet has an extended header by checking if the extended header flag is set.
    /// Uses bit 6 which is now safely separated from packet type values.
    /// </summary>
    /// <param name="rawType">The raw packet type byte.</param>
    /// <returns>True if the packet has an extended header; otherwise, false.</returns>
    public static bool HasExtendedHeader(byte rawType)
        => (rawType & ExtendedHeaderFlag) != 0;

    /// <summary>
    /// Get base packet type, stripping all flag bits (bits 6 and 7).
    /// </summary>
    /// <param name="rawType">The raw packet type byte with potential flags.</param>
    /// <returns>The clean packet type without flag bits.</returns>
    public static NodePacketType GetNodePacketType(byte rawType)
        => (NodePacketType)(rawType & (byte)NodePacketType.TypeMask);

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
    /// Negotiates the packet version to use for communication between nodes.
    /// Returns the lower of the two versions to ensure compatibility between
    /// nodes that may be running different versions of MSBuild.
    /// 
    /// This allows forward and backward compatibility when nodes with different
    /// packet versions communicate - they will use the lowest common version
    /// that both understand.
    /// </summary>
    /// <param name="otherPacketVersion">The packet version supported by the other node.</param>
    /// <returns>The negotiated protocol version that both nodes can use (the minimum of the two versions).</returns>
    public static byte GetNegotiatedPacketVersion(byte otherPacketVersion)
        => Math.Min(PacketVersion, otherPacketVersion);
}
