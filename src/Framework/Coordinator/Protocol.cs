// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  The current version of the coordinator protocol.
/// </summary>
internal static class Protocol
{
    /// <summary>
    ///  Protocol version. Increment when the wire format changes.
    /// </summary>
    public const byte Version = 1;
}
