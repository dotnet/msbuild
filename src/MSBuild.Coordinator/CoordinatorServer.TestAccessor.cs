// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Internal;

namespace Microsoft.Build.Coordinator;

internal sealed partial class CoordinatorServer
{
    internal static class TestAccessor
    {
        public static bool TryDisposeConnectionForProcess(CoordinatorServer server, int processId)
        {
            ConnectedClient? connectionToDispose = null;

            using (server._clientsLock.EnterDisposableReadLock())
            {
                foreach (ConnectedClient connection in server._clientsById.Values)
                {
                    if (connection.ProcessId == processId)
                    {
                        connectionToDispose = connection;
                        break;
                    }
                }
            }

            connectionToDispose?.Dispose();
            return connectionToDispose is not null;
        }
    }
}
