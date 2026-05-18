// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Client interface for communicating with an external build coordinator.
    /// If no coordinator is running, implementations should gracefully no-op
    /// so the build proceeds with its original MaxNodeCount.
    /// </summary>
    internal interface ICoordinatorClient : IDisposable
    {
        /// <summary>Whether this client is registered with a coordinator.</summary>
        bool IsConnected { get; }

        /// <summary>The number of nodes granted by the coordinator.</summary>
        int GrantedNodes { get; }

        /// <summary>
        /// Try to register with the coordinator. Returns true if a coordinator was found
        /// and the build was registered (or promoted from queue).
        /// </summary>
        bool TryRegister(int requestedNodes, out int grantedNodes, CancellationToken ct = default);

        /// <summary>
        /// Start periodic heartbeats so the coordinator can detect stale builds.
        /// </summary>
        void StartHeartbeat();

        /// <summary>
        /// Unregister from the coordinator and stop heartbeats.
        /// </summary>
        void Unregister();
    }
}
