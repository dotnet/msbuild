// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// Proxy implementation of ITaskHost that runs in the child process (MSBuildTaskHost.exe)
    /// and forwards all calls to the parent process (msbuild.exe) via named pipe IPC.
    /// This is NOT MarshalByRefObject - it uses manual serialization over named pipes.
    /// </summary>
    internal class HostObjectProxy : ITaskHost, IEnumerable<ITaskItem>
    {
        private readonly NodeEndpointOutOfProcTaskHost _endpoint;
        private int _nextCallId = 0;

        // Synchronization for blocking calls
        private readonly Dictionary<int, ManualResetEvent> _pendingCalls = new Dictionary<int, ManualResetEvent>();
        private readonly Dictionary<int, HostObjectResponse> _responses = new Dictionary<int, HostObjectResponse>();
        private readonly object _callLock = new object();

        // Cache for items once retrieved
        private ITaskItem[] _cachedItems;
        private bool _itemsRetrieved = false;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="endpoint">The node endpoint for communicating with parent process.</param>
        public HostObjectProxy(NodeEndpointOutOfProcTaskHost endpoint)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        }

        /// <summary>
        /// Called by OutOfProcTaskHostNode when a TaskHostMethodResponse packet arrives
        /// from the parent process. Unblocks the waiting thread.
        /// </summary>
        /// <param name="response">The response packet from parent.</param>
        internal void HandleResponse(HostObjectResponse response)
        {
            lock (_callLock)
            {
                _responses[response.CallId] = response;

                if (_pendingCalls.TryGetValue(response.CallId, out ManualResetEvent waitHandle))
                {
                    waitHandle.Set();
                }
            }
        }

        /// <summary>
        /// Invokes a method on the parent's ITaskHost and waits for the result.
        /// This blocks the calling thread until the parent responds.
        /// </summary>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <returns>The return value from the parent process.</returns>
        private object InvokeRemoteMethod(string methodName)
        {
            int callId = Interlocked.Increment(ref _nextCallId);
            ManualResetEvent waitHandle = new ManualResetEvent(false);

            lock (_callLock)
            {
                _pendingCalls[callId] = waitHandle;
            }

            try
            {
                // Create and send method call packet to parent
                var callPacket = new HostObjectRequest(callId, methodName);
                _endpoint.SendData(callPacket);

                // Wait for response (with 5 minute timeout to prevent deadlock)
                bool signaled = waitHandle.WaitOne(TimeSpan.FromMinutes(5));

                if (!signaled)
                {
                    throw new TimeoutException($"Timeout waiting for response to TaskHost method '{methodName}' (CallId: {callId})");
                }

                // Get the response
                HostObjectResponse response;
                lock (_callLock)
                {
                    response = _responses[callId];
                    _responses.Remove(callId);
                    _pendingCalls.Remove(callId);
                }

                // Check if parent threw an exception
                if (!string.IsNullOrEmpty(response.ExceptionMessage))
                {
                    throw new InvalidOperationException(
                        $"Remote TaskHost method '{methodName}' threw {response.ExceptionType}: {response.ExceptionMessage}\n{response.ExceptionStackTrace}");
                }

                return response.ReturnValue;
            }
            finally
            {
                waitHandle.Dispose();
            }
        }

        /// <summary>
        /// Gets all ITaskItem objects from the parent's ITaskHost.
        /// Results are cached for performance.
        /// </summary>
        private ITaskItem[] GetAllItems()
        {
            if (!_itemsRetrieved)
            {
                var result = InvokeRemoteMethod("GetAllItems");
                _cachedItems = result as ITaskItem[] ?? Array.Empty<ITaskItem>();
                _itemsRetrieved = true;
            }

            return _cachedItems;
        }

        /// <summary>
        /// When VSHostObject enumerates the HostObject, this retrieves all items from parent.
        /// </summary>
        public IEnumerator<ITaskItem> GetEnumerator()
        {
            ITaskItem[] items = GetAllItems();
            return ((IEnumerable<ITaskItem>)items).GetEnumerator();
        }

        /// <summary>
        /// Non-generic IEnumerable implementation.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
