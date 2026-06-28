// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Internal;

/// <summary>
///  Enumeration of all possible (currently supported) options for handshakes.
/// </summary>
[Flags]
internal enum HandshakeOptions
{
    None = 0,

    /// <summary>
    ///  Process is a TaskHost.
    /// </summary>
    TaskHost = 1,

    /// <summary>
    ///  Using the 2.0 CLR.
    /// </summary>
    CLR2 = 2,

    /// <summary>
    ///  64-bit Intel process.
    /// </summary>
    X64 = 4,

    /// <summary>
    ///  Node reuse enabled.
    /// </summary>
    NodeReuse = 8,

    /// <summary>
    ///  Building with BelowNormal priority.
    /// </summary>
    LowPriority = 16,

    /// <summary>
    ///  Building with administrator privileges.
    /// </summary>
    Administrator = 32,

    /// <summary>
    ///  Using the .NET Core/.NET 5.0+ runtime.
    /// </summary>
    NET = 64,

    /// <summary>
    ///  ARM64 process.
    /// </summary>
    Arm64 = 128,

    /// <summary>
    ///  Using a long-running sidecar TaskHost process to reduce startup overhead and reuse in-memory caches.
    /// </summary>
    SidecarTaskHost = 256,
}
