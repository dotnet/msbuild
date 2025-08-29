// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Create a wrapper so that when dispose is called we execute the delegate.
    /// </summary>
    internal class BuildCacheDisposeWrapper : IDisposable
    {
        /// <summary>
        /// Has this been disposed
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Delegate to call when we are in dispose
        /// </summary>
        private readonly CallDuringDispose _callDuringDispose;

        /// <summary>
        /// Constructor
        /// </summary>
        internal BuildCacheDisposeWrapper(CallDuringDispose callDuringDispose)
        {
            _callDuringDispose = callDuringDispose;
        }

        /// <summary>
        /// Delegate to call when we are in dispose
        /// </summary>
        internal delegate void CallDuringDispose();

        /// <summary>
        /// IDisposable
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Clear the caches
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            // Check to see if Dispose has already been called.
            if (!_disposed && disposing)
            {
                _disposed = true;
                _callDuringDispose?.Invoke();
            }
        }
    }
}
