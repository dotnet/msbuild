// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Internal;

/// <summary>
/// Runtime-specific configuration, before the task host's installation identity is resolved.
/// </summary>
internal union TaskHostHandshakeConfiguration(
    NetTaskHostHandshakeConfiguration,
    FrameworkTaskHostHandshakeConfiguration,
    Clr2TaskHostHandshakeConfiguration)
{
    internal HandshakeOptions HandshakeOptions => this switch
    {
        NetTaskHostHandshakeConfiguration configuration => configuration.HandshakeOptions,
        FrameworkTaskHostHandshakeConfiguration configuration => configuration.HandshakeOptions,
        Clr2TaskHostHandshakeConfiguration configuration => configuration.HandshakeOptions,
    };
}

internal readonly record struct NetTaskHostHandshakeConfiguration(TaskHostHandshakeOptions Options)
{
    internal HandshakeOptions HandshakeOptions => Options.ToWireOptions() | HandshakeOptions.TaskHost | HandshakeOptions.NET;

    internal NetTaskHostHandshake CreateHandshake(string? toolsDirectory = null) => new(Options, toolsDirectory);
}

internal readonly record struct FrameworkTaskHostHandshakeConfiguration(TaskHostHandshakeOptions Options)
{
    internal HandshakeOptions HandshakeOptions => Options.ToWireOptions() | HandshakeOptions.TaskHost;

    internal FrameworkTaskHostHandshake CreateHandshake() => new(Options);

    internal FrameworkTaskHostHandshake CreateHandshake(string toolsDirectory, Version fileVersion) => new(Options, toolsDirectory, fileVersion);
}

internal readonly record struct Clr2TaskHostHandshakeConfiguration(TaskHostHandshakeOptions Options)
{
    internal HandshakeOptions HandshakeOptions => Options.ToWireOptions() | HandshakeOptions.TaskHost | HandshakeOptions.CLR2;

    internal Clr2TaskHostHandshake CreateHandshake(string toolsDirectory, Version? fileVersion = null) => new(Options, toolsDirectory, fileVersion);
}
