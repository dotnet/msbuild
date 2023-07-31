// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Telemetry.PersistenceChannel
{
    /// <summary>
    ///     A light fixed size queue. If Enqueue is called and queue's limit has reached the last item will be removed.
    ///     This data structure is thread safe.
    /// </summary>
    internal class FixedSizeQueue<T>
    {
        private readonly int _maxSize;
        private readonly Queue<T> _queue = new Queue<T>();
        private readonly object _queueLockObj = new object();

        internal FixedSizeQueue(int maxSize)
        {
            _maxSize = maxSize;
        }

        internal void Enqueue(T item)
        {
            lock (_queueLockObj)
            {
                if (_queue.Count == _maxSize)
                {
                    _queue.Dequeue();
                }

                _queue.Enqueue(item);
            }
        }

        internal bool Contains(T item)
        {
            lock (_queueLockObj)
            {
                return _queue.Contains(item);
            }
        }
    }
}
