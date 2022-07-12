// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;

namespace Microsoft.Build.Execution
{
    internal sealed class ServerNamedMutex : IDisposable
    {
        private readonly Mutex _serverMutex;

        public bool IsDisposed { get; private set; }

        public bool IsLocked { get; private set; }

        public ServerNamedMutex(string mutexName, out bool createdNew)
        {
            _serverMutex = new Mutex(
                initiallyOwned: true,
                name: mutexName,
                createdNew: out createdNew);

            if (createdNew)
            {
                IsLocked = true;
            }
        }

        internal static ServerNamedMutex OpenOrCreateMutex(string name, out bool createdNew)
        {
            return new ServerNamedMutex(name, out createdNew);
        }

        public static bool WasOpen(string mutexName)
        {
            bool result = Mutex.TryOpenExisting(mutexName, out Mutex? mutex);
            mutex?.Dispose();

            return result;
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;

            try
            {
                if (IsLocked)
                {
                    _serverMutex.ReleaseMutex();
                }
            }
            finally
            {
                _serverMutex.Dispose();
                IsLocked = false;
            }
        }
    }
}
