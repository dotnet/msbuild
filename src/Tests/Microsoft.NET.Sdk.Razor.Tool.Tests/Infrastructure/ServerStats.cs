// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal struct ServerStats
    {
        internal readonly int Connections;
        internal readonly int CompletedConnections;

        internal ServerStats(int connections, int completedConnections)
        {
            Connections = connections;
            CompletedConnections = completedConnections;
        }
    }
}
