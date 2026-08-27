// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;

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
            try
            {
                bool result = Mutex.TryOpenExisting(mutexName, out Mutex? mutex);
                mutex?.Dispose();

                return result;
            }
            catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex) && ex is not PathTooLongException)
            {
                // In unexpected state fall back to non-server execution.
                CommunicationsUtilities.Trace($"Failed to open mutex '{mutexName}', treating it as not open. Exception: {ex}");

                return false;
            }
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
