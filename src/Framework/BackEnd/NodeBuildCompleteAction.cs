// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd;

internal enum NodeBuildCompleteAction
{
    // Use the original pooling-or-exit behavior without retaining the connection.
    Legacy,
    ReuseWithConnection,
    Shutdown
}
