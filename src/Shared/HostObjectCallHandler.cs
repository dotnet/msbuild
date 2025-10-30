// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Handles ITaskHost method calls from child process in the parent process (msbuild.exe).
    /// Executes methods on the real ITaskHost and sends responses back to child.
    /// </summary>
    internal class HostObjectCallHandler
    {
        private readonly ITaskHost _realTaskHost;
        private readonly Action<INodePacket> _endpointCall;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="taskHost">The real ITaskHost object (typically IEnumerable&lt;ITaskItem&gt;).</param>
        /// <param name="endpoint">The node endpoint for sending responses to child.</param>
        public HostObjectCallHandler(ITaskHost taskHost, Action<INodePacket> endpoint)
        {
            _realTaskHost = taskHost ?? throw new ArgumentNullException(nameof(taskHost));
            _endpointCall = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        }

        /// <summary>
        /// Handles a method call packet from the child process.
        /// Executes the method on the real ITaskHost and sends response back.
        /// </summary>
        /// <param name="callPacket">The method call packet from child.</param>
        public void HandleMethodCall(HostObjectRequest callPacket)
        {
            HostObjectResponse response;

            try
            {
                object? result = null;

                switch (callPacket.MethodName)
                {
                    case "GetAllItems":
                        result = GetAllItems();
                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Unknown TaskHost method: {callPacket.MethodName}");
                }

                response = new HostObjectResponse(callPacket.CallId, result);
            }
            catch (Exception ex)
            {
                // Capture exception and send back to child
                response = new HostObjectResponse(callPacket.CallId, ex);
            }

            // Send response back to child process
            _endpointCall(response);
        }

        /// <summary>
        /// Gets all ITaskItem objects from the real ITaskHost.
        /// Converts IEnumerable&lt;ITaskItem&gt; to array for serialization.
        /// </summary>
        private ITaskItem[] GetAllItems()
        {
            var enumerable = _realTaskHost as IEnumerable<ITaskItem>;

            if (enumerable == null)
            {
                return Array.Empty<ITaskItem>();
            }

            // Convert to array for serialization
            return enumerable.ToArray();
        }
    }
}
