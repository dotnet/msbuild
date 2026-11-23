// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Internal;

/// <summary>
/// A runtime-specific request, before the task host's installation identity is resolved.
/// </summary>
internal union TaskHostRequest(
    NetTaskHostRequest,
    FrameworkTaskHostRequest,
    Clr2TaskHostRequest)
{
    internal HandshakeOptions HandshakeOptions => this switch
    {
        NetTaskHostRequest request => request.HandshakeOptions,
        FrameworkTaskHostRequest request => request.HandshakeOptions,
        Clr2TaskHostRequest request => request.HandshakeOptions,
    };

    internal TaskHostLaunchPaths LaunchPaths => this switch
    {
        NetTaskHostRequest request => request.LaunchPaths,
        FrameworkTaskHostRequest request => request.LaunchPaths,
        Clr2TaskHostRequest request => request.LaunchPaths,
    };
}

// Both paths affect reuse decisions, even when a Framework host does not use them to launch.
internal readonly record struct TaskHostLaunchPaths(string? MSBuildAssemblyPath = null, string? DotnetHostPath = null);

internal readonly record struct NetTaskHostRequest(TaskHostHandshakeOptions Options, TaskHostLaunchPaths LaunchPaths = default)
{
    internal HandshakeOptions HandshakeOptions => Options.ToWireOptions() | HandshakeOptions.TaskHost | HandshakeOptions.NET;

    internal NetTaskHostHandshake CreateHandshake(string? toolsDirectory = null) => new(Options, toolsDirectory);
}

internal readonly record struct FrameworkTaskHostRequest(TaskHostHandshakeOptions Options, TaskHostLaunchPaths LaunchPaths = default)
{
    internal HandshakeOptions HandshakeOptions => Options.ToWireOptions() | HandshakeOptions.TaskHost;

    internal FrameworkTaskHostHandshake CreateHandshake() => new(Options);

    internal FrameworkTaskHostHandshake CreateHandshake(string toolsDirectory, Version fileVersion) => new(Options, toolsDirectory, fileVersion);
}

internal readonly record struct Clr2TaskHostRequest(TaskHostHandshakeOptions Options, TaskHostLaunchPaths LaunchPaths = default)
{
    internal HandshakeOptions HandshakeOptions => Options.ToWireOptions() | HandshakeOptions.TaskHost | HandshakeOptions.CLR2;

    internal Clr2TaskHostHandshake CreateHandshake(string toolsDirectory, Version? fileVersion = null) => new(Options, toolsDirectory, fileVersion);
}
