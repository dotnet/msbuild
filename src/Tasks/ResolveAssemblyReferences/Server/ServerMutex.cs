// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Server
{
    internal sealed class ServerMutex : IDisposable
    {
        private readonly Mutex _mutex;
        public bool IsLocked { get; private set; }
        public bool IsDisposed { get; private set; }

        public ServerMutex(string name)
        {
            _mutex = new Mutex(true, name, out bool createdNew);
            IsLocked = createdNew;
        }

        public bool Wait(int timeout)
        {
            return _mutex.WaitOne(timeout);
        }


        public void Dispose()
        {

            if (IsDisposed)
                return;
            IsDisposed = true;

            try
            {
                if (IsLocked)
                    _mutex.ReleaseMutex();
            }
            finally
            {
                _mutex.Dispose();
                IsLocked = false;
            }
        }
    }
}
