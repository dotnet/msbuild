// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Build.Tasks
{
    internal sealed class FileAccessMutex : IDisposable
    {
        private readonly Mutex _mutex;
        public bool IsDisposed { get; private set; }
        public bool IsLocked { get; private set; }
        public bool CreatedNew { get; private set; }

        public FileAccessMutex(string mutexName, int millisecondsTimeout = -1)
        {
            _mutex = new Mutex(
            initiallyOwned: true,
            name: mutexName,
            createdNew: out bool createdNew);

            CreatedNew = createdNew;
            if (!createdNew)
            {
                IsLocked = _mutex.WaitOne(millisecondsTimeout);
            }
            else
            {
                IsLocked = true;
            }
        }

        public static FileAccessMutex OpenOrCreateMutex(string name, int millisecondsTimeout = -1)
        {
            return new FileAccessMutex(name, millisecondsTimeout);
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
                    _mutex.ReleaseMutex();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error releasing mutex: {ex.Message}");
            }
            finally
            {
                _mutex.Dispose();
                IsLocked = false;
            }
        }
    }
}
