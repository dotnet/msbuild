// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Coordinator;

internal sealed partial class CoordinatorServer
{
    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CoordinatorServer server)
    {
        public bool IsDisposing => Volatile.Read(ref server._disposeState) == Disposing;

        public void SetHeartbeatCallbackStarted(Action callback)
            => server._heartbeatCallbackStartedForTests = callback;

        public void TriggerHeartbeatCheck()
        {
            lock (server._lifecycleLock)
            {
                server._heartbeatMonitor!.Change(dueTime: 0, period: Timeout.Infinite);
            }
        }
    }
}
