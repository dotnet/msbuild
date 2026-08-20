// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Threading;

#nullable disable

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Hosts multiple independent logical worker nodes in one child process.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class OutOfProcMultiNode
    {
        private readonly int _nodeCount;

        public OutOfProcMultiNode(int nodeCount)
        {
            if (nodeCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(nodeCount));
            }

            _nodeCount = nodeCount;
        }

        public NodeEngineShutdownReason Run(bool lowPriority, out Exception shutdownException)
        {
            var nodes = new OutOfProcNode[_nodeCount];
            var threads = new Thread[_nodeCount];
            var shutdownReasons = new NodeEngineShutdownReason[_nodeCount];
            var shutdownExceptions = new Exception[_nodeCount];

            for (int i = 0; i < _nodeCount; i++)
            {
                int slot = i;
                nodes[slot] = new OutOfProcNode(usesSharedProcess: true);
                threads[slot] = new Thread(() =>
                {
                    shutdownReasons[slot] = nodes[slot].Run(lowPriority, slot, out shutdownExceptions[slot]);
                })
                {
                    IsBackground = true,
                    Name = $"Out-of-proc logical node {slot}",
                };
            }

            foreach (Thread thread in threads)
            {
                thread.Start();
            }

            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            shutdownException = null;
            NodeEngineShutdownReason aggregateReason = NodeEngineShutdownReason.BuildComplete;
            for (int i = 0; i < _nodeCount; i++)
            {
                if (shutdownExceptions[i] != null)
                {
                    shutdownException = shutdownException is null
                        ? shutdownExceptions[i]
                        : new AggregateException(shutdownException, shutdownExceptions[i]);
                }

                if (shutdownReasons[i] == NodeEngineShutdownReason.Error)
                {
                    aggregateReason = NodeEngineShutdownReason.Error;
                }
                else if (shutdownReasons[i] == NodeEngineShutdownReason.ConnectionFailed
                    && aggregateReason != NodeEngineShutdownReason.Error)
                {
                    aggregateReason = NodeEngineShutdownReason.ConnectionFailed;
                }
            }

            return aggregateReason;
        }
    }
}
