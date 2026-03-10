// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Optional build coordinator integration.
    /// When MSBUILDCOORDINATORENABLED=1 is set and a coordinator process is running,
    /// BuildManager will register with it to receive a node budget and coordinate
    /// with other concurrent builds. Otherwise this code is completely inert.
    /// </summary>
    public partial class BuildManager
    {
        /// <summary>
        /// Client for communicating with an external build coordinator process.
        /// Null unless a coordinator is running AND this build successfully registered.
        /// </summary>
        private BuildCoordinatorClient? _coordinatorClient;

        /// <summary>
        /// Returns true if the coordinator feature is explicitly enabled via environment variable.
        /// </summary>
        private static bool IsCoordinatorEnabled()
        {
            return string.Equals(
                Environment.GetEnvironmentVariable("MSBUILDCOORDINATORENABLED"),
                "1",
                StringComparison.Ordinal);
        }

        /// <summary>
        /// Attempts to register this build with an external build coordinator process.
        /// If a coordinator is running, it may adjust MaxNodeCount or queue the build.
        /// If no coordinator is running (or the feature is disabled), does nothing and
        /// the build proceeds normally.
        /// </summary>
        private void TryRegisterWithCoordinator()
        {
            if (!IsCoordinatorEnabled() || _buildParameters == null)
            {
                return;
            }

            var client = new BuildCoordinatorClient();
            int requestedNodes = _buildParameters.MaxNodeCount;

            bool registered = client.TryRegister(
                requestedNodes,
                out int grantedNodes,
                onQueuePositionChanged: (position, total, waitSec) =>
                {
                    Trace.WriteLine($"MSBuild coordinator: Queued position {position}/{total}, waiting {waitSec}s");
                },
                ct: _executionCancellationTokenSource?.Token ?? CancellationToken.None);

            if (registered)
            {
                _coordinatorClient = client;

                if (grantedNodes != requestedNodes)
                {
                    _buildParameters.MaxNodeCount = grantedNodes;
                    Trace.WriteLine($"MSBuild coordinator: Node budget {grantedNodes} (requested {requestedNodes})");
                }

                client.StartHeartbeat();
            }
            else
            {
                client.Dispose();
            }
        }

        /// <summary>
        /// Unregisters from the coordinator so other queued builds can scale up.
        /// </summary>
        private void UnregisterFromCoordinator()
        {
            _coordinatorClient?.Dispose();
            _coordinatorClient = null;
        }

        /// <summary>
        /// Returns true if this build is being managed by a coordinator,
        /// meaning node reuse should be disabled so nodes exit immediately.
        /// </summary>
        private bool IsCoordinatorManaged => _coordinatorClient != null;
    }
}

#endif
