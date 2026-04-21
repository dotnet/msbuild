// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Internal;

namespace Microsoft.Build.BackEnd;

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
    /// null: CLR2 (NET35) task host. Version-dependent fields skipped (not compiled in NET35).
    /// 0: The constant value for Framework-to-Framework (CLR4) task host. Supports HostServices, TargetName, ProjectFile.
    /// 1: .NET task host support.
    /// 2: Added support for translating/reading HostServices, ProjectFile, TargetName in TaskHostConfiguration.
    /// 3: Added App Host support.
    /// 4: Added IsRunningMultipleNodes, Request/ReleaseCores, BuildProjectFile callbacks support for OOP TaskHost.
    /// 
    /// When incrementing this version, ensure compatibility with existing
    /// task hosts and update the corresponding deserialization logic.
    /// </summary>
    public const byte PacketVersion = 4;

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
        if (Handshake.IsHandshakeOptionEnabled(handshakeOptions, HandshakeOptions.TaskHost) && Handshake.IsHandshakeOptionEnabled(handshakeOptions, HandshakeOptions.NET))
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
    public static byte GetNegotiatedPacketVersion(byte otherPacketVersion) => Math.Min(PacketVersion, otherPacketVersion);
}
