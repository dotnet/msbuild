// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework.Coordinator;

namespace Microsoft.Build.BackEnd;

internal sealed partial class CoordinatorClient
{
    /// <summary>
    ///  Sends coordinator heartbeats and waits for in-flight callbacks during disposal.
    /// </summary>
    private sealed class HeartbeatTimer : IDisposable
    {
        private const int NotDisposed = 0;
        private const int Disposing = 1;
        private const int Disposed = 2;

        private readonly Connection _connection;
        private readonly ICoordinatorDebugOutput _output;
        private readonly Timer _timer;
        private int _disposeState;

        private bool IsDisposingOrDisposed => Volatile.Read(ref _disposeState) != NotDisposed;

        public HeartbeatTimer(Connection connection, int intervalMs, ICoordinatorDebugOutput output)
        {
            _connection = connection;
            _output = output;
            _timer = new Timer(
                static state => ((HeartbeatTimer)state!).SendHeartbeat(),
                state: this,
                dueTime: intervalMs,
                period: intervalMs);
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposeState, Disposing, NotDisposed) != NotDisposed)
            {
                return;
            }

            try
            {
                using var disposeEvent = new ManualResetEvent(false);
                if (_timer.Dispose(disposeEvent))
                {
                    disposeEvent.WaitOne();
                }
            }
            finally
            {
                Volatile.Write(ref _disposeState, Disposed);
            }
        }

        private void SendHeartbeat()
        {
            if (IsDisposingOrDisposed)
            {
                return;
            }

            try
            {
                _connection.WriteClientMessage(HeartbeatMessage.Instance);
            }
            catch (IOException)
            {
                _output.WriteLine("CoordinatorClient: Heartbeat failed (pipe broken)");

                // Pipe broken -- nothing we can do. The build continues
                // with whatever nodes were already granted.
            }
        }
    }
}
