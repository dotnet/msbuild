// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Threading;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Hosts multiple independent logical worker nodes in one child process.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class OutOfProcMultiNode
    {
        internal delegate NodeEngineShutdownReason SlotRunner(int slot, out Exception shutdownException);

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

            for (int i = 0; i < _nodeCount; i++)
            {
                nodes[i] = new OutOfProcNode(usesSharedProcess: true);
            }

            return RunSlots(
                _nodeCount,
                (int slot, out Exception exception) => nodes[slot].Run(lowPriority, slot, out exception),
                slot => nodes[slot].RequestCoordinatedShutdown(),
                out shutdownException);
        }

        internal static NodeEngineShutdownReason RunSlots(
            int nodeCount,
            SlotRunner runSlot,
            Action<int> requestSlotShutdown,
            out Exception shutdownException)
        {
            var threads = new Thread[nodeCount];
            var shutdownReasons = new NodeEngineShutdownReason[nodeCount];
            var shutdownExceptions = new Exception[nodeCount];
            int coordinatedShutdownRequested = 0;

            for (int i = 0; i < nodeCount; i++)
            {
                int slot = i;
                threads[slot] = new Thread(() =>
                {
                    try
                    {
                        shutdownReasons[slot] = runSlot(slot, out shutdownExceptions[slot]);
                    }
                    catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                    {
                        shutdownReasons[slot] = NodeEngineShutdownReason.Error;
                        shutdownExceptions[slot] = ex;
                    }

                    if ((shutdownReasons[slot] == NodeEngineShutdownReason.Error
                            || shutdownReasons[slot] == NodeEngineShutdownReason.ConnectionFailed)
                        && Interlocked.CompareExchange(ref coordinatedShutdownRequested, 1, 0) == 0)
                    {
                        for (int siblingSlot = 0; siblingSlot < nodeCount; siblingSlot++)
                        {
                            if (siblingSlot != slot)
                            {
                                try
                                {
                                    requestSlotShutdown(siblingSlot);
                                }
                                catch (Exception ex) when (!ExceptionHandling.IsCriticalException(ex))
                                {
                                    shutdownReasons[slot] = NodeEngineShutdownReason.Error;
                                    shutdownExceptions[slot] = shutdownExceptions[slot] is null
                                        ? ex
                                        : new AggregateException(shutdownExceptions[slot], ex);
                                }
                            }
                        }
                    }
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
            for (int i = 0; i < nodeCount; i++)
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
