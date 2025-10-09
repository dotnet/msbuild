// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Build.Tasks
{
    internal sealed class SystemWideMutex : IDisposable
    {
        public bool HasHandle = false;
        private Mutex? _mutex;
        private bool _isDisposed;

        internal bool IsDisposed => _isDisposed;

        private SystemWideMutex(string mutexName, int millisecondsTimeout = -1)
        {
            try
            {
                _mutex = new Mutex(false, mutexName);
                HasHandle = _mutex.WaitOne(millisecondsTimeout);
            }
            catch (AbandonedMutexException)
            {
                HasHandle = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating/acquiring mutex '{mutexName}': {ex.Message}");
                HasHandle = false;
                _mutex?.Dispose();
                _mutex = null;
            }
        }

        public static SystemWideMutex OpenOrCreateMutex(string name, int millisecondsTimeout = -1)
        {
            return new SystemWideMutex(name, millisecondsTimeout);
        }

        public static bool WasOpen(string mutexName)
        {
            try
            {
                bool result = Mutex.TryOpenExisting(mutexName, out Mutex? mutex);
                mutex?.Dispose();
                return result;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            if (_mutex != null)
            {
                if (HasHandle)
                {
                    _mutex.ReleaseMutex();
                }

                _mutex.Dispose();
                _mutex = null;
            }
        }
    }
}

