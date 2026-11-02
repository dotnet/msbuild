// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework.Coordinator;

internal sealed partial record NodeGrantMessage
{
    /// <summary>
    ///  Flags describing which capability-gated fields are present in a <see cref="NodeGrantMessage"/> payload.
    /// </summary>
    /// <remarks>
    ///  Each flag corresponds to a capability that both the coordinator and the client must advertise during the
    ///  handshake before the associated field is written to (or expected on) the wire. Callers compute the
    ///  applicable flags from the negotiated capabilities of the peer they are writing to, so that peers which
    ///  predate a given capability -- and therefore don't know how to parse its field -- are never sent it.
    /// </remarks>
    [Flags]
    internal enum ExtendedFields : byte
    {
        /// <summary>
        ///  No capability-gated fields are present. This is the original, legacy shape.
        /// </summary>
        None = 0,

        /// <summary>
        ///  The payload includes a <see cref="Guid"/> root grant token, because both peers advertised the
        ///  <see cref="Capabilities.NestedGrants"/> capability.
        /// </summary>
        GrantId = 1 << 0,
    }
}
