// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Enumeration of all of the packet types used for communication.
/// Uses lower 6 bits for packet type (0-63), upper 2 bits reserved for flags.
/// </summary>
/// <remarks>
/// Several of these values must be kept in sync with MSBuildTaskHost's NodePacketType.
/// The values shared with MSBuildTaskHost are <see cref="LogMessage"/>,
/// <see cref="NodeBuildComplete"/>, <see cref="NodeShutdown"/>, <see cref="TaskHostConfiguration"/>,
/// <see cref="TaskHostTaskCancelled"/>, and <see cref="TaskHostTaskComplete"/>.
/// </remarks>
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

    /// <summary>
    /// A batch of log events emitted while the RAR task is executing.
    /// </summary>
    RarNodeBufferedLogEvents, // 0x16

    // Packet types 0x17-0x1F reserved for future core functionality

    #region TaskHost callback packets (0x20-0x27)
    // These support bidirectional callbacks from TaskHost to parent for IBuildEngine implementations

    /// <summary>
    /// Request from TaskHost to parent to execute BuildProjectFile* callbacks.
    /// </summary>
    TaskHostBuildRequest = 0x20,

    /// <summary>
    /// Response from parent to TaskHost with BuildProjectFile* results.
    /// </summary>
    TaskHostBuildResponse = 0x21,

    /// <summary>
    /// Request from TaskHost to owning worker node for RequestCores/ReleaseCores.
    /// </summary>
    TaskHostCoresRequest = 0x22,

    /// <summary>
    /// Response from owning worker node to TaskHost with core allocation result.
    /// </summary>
    TaskHostCoresResponse = 0x23,

    /// <summary>
    /// Request from TaskHost to owning worker node for IsRunningMultipleNodes.
    /// </summary>
    TaskHostIsRunningMultipleNodesRequest = 0x24,

    /// <summary>
    /// Response from owning worker node to TaskHost with IsRunningMultipleNodes value.
    /// </summary>
    TaskHostIsRunningMultipleNodesResponse = 0x25,

    // 0x26-0x27 reserved for future TaskHost callback packet types

    #endregion

    // Server command packets placed at end of safe range to maintain separation from core packets
    #region ServerNode enums

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
