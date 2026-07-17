// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd;

internal sealed partial class CoordinatorClient
{
    /// <summary>
    ///  Describes whether this client owns a root coordinator grant or has joined an inherited grant.
    /// </summary>
    private enum GrantOwnership
    {
        /// <summary>
        ///  Owns a root coordinator grant.
        /// </summary>
        Root,

        /// <summary>
        ///  Joins an inherited coordinator grant.
        /// </summary>
        Nested,
    }
}
