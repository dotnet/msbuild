// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class provides a multiple-writer, single-reader queue. This queue can be written to
    /// by multiple threads at a time, but it is designed to be only read by a single thread.
    /// The way is works is as follows: we have two queues, one for reading from, and one for
    /// writing to. The writing queue is protected by a mutex so that multiple threads can write to
    /// it. When a reading thread wants to read all the queued items, we swap the writing queue
    /// for another (empty) one. The writing queue then becomes the reading queue, and the empty
    /// queue becomes the new writing queue. This allows the reader to safely read from the swapped
    /// out queue without regard to new items being written to the other queue.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal sealed class DualQueue<T>
    {
        #region Constructors

        /// <summary>
        /// Default constructor.
        /// </summary>
        internal DualQueue()
        {
            this.queueReadyEvent = new ManualResetEvent(false /* event is reset initially */);
            this.queueEmptyEvent = null;
            this.queueLock = new object();
            this.backingQueueA = new Queue<T>();
            this.backingQueueB = new Queue<T>();

            this.queue = this.backingQueueA;
        }

        #endregion

        #region Properties
        /// <summary>
        /// Event indicating that there are items in the queue
        /// </summary>
        internal WaitHandle QueueReadyEvent
        {
            get
            {
                return this.queueReadyEvent;
            }
        }

        /// <summary>
        /// Event indicating that the queue is empty
        /// </summary>
        internal WaitHandle QueueEmptyEvent
        {
            get
            {
                // Lazily allocate the queue empty event
                lock (queueLock)
                {
                    if (this.queueEmptyEvent == null)
                    {
                        this.queueEmptyEvent = new ManualResetEvent(false /* event is reset initially */);
                    }
                }
                return this.queueEmptyEvent;
            }
        }

        /// <summary>
        /// Primairly used for testing to get the count of items posted to the queue
        /// </summary>
        /// <returns></returns>
        internal int Count
        {

            get
            {
                // Sum both as the number of items is the sum of items in both queues
                Queue<T> readingQueue = backingQueueB;

                // figure out the current reading queue
                if (queue == backingQueueB)
                {
                    readingQueue = backingQueueA;
                }

                return readingQueue.Count + writingQueueCount;
            }
        }

        /// <summary>
        /// The count of items in the writing queue. Used to decide if the queue is backing up
        /// </summary>
        /// <returns></returns>
        internal int WritingQueueCount
        {
            get
            {
                return writingQueueCount;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds the given item to the queue.
        /// </summary>
        /// <param name="item"></param>
        internal void Enqueue(T item)
        {
            lock (queueLock)
            {
                // queue the event
                queue.Enqueue(item);

                writingQueueCount++;

                // if the queue transitions from empty to non-empty reset the queue empty event and raise queue ready event
                if ( writingQueueCount == 1)
                {
                    // raise the event saying queue contains data
                    queueReadyEvent.Set();

                    // reset queue empty
                    if (queueEmptyEvent != null)
                    {
                        queueEmptyEvent.Reset();
                    }
                }
            }
        }

        /// <summary>
        /// Adds the given items to the queue.
        /// </summary>
        /// <param name="items"></param>
        internal void EnqueueArray(T[] items)
        {
            lock (queueLock)
            {
                // queue the event
                foreach (T item in items)
                {
                    queue.Enqueue(item);
                }

                writingQueueCount += items.Length;

                // if the queue transitions from empty to non-empty reset the queue empty event
                if (writingQueueCount == items.Length)
                {
                    // raise the event saying queue contains data
                    queueReadyEvent.Set();

                    // reset queue empty
                    if (queueEmptyEvent != null)
                    {
                        queueEmptyEvent.Reset();
                    }
                }
            }
        }

        /// <summary>
        /// Clear the contents of the queue
        /// </summary>
        internal void Clear()
        {
            lock (queueLock)
            {
                backingQueueA.Clear();
                backingQueueB.Clear();
                writingQueueCount = 0;
                // reset queue ready event because the queue is now empty
                queueReadyEvent.Reset();
                // raise queue empty event because the queue is now empty
                if (queueEmptyEvent != null)
                {
                    queueEmptyEvent.Set();
                }
            }
        }

        /// <summary>
        /// Gets an item off the queue.
        /// </summary>
        /// <returns>The top item off the queue, or null if queue is empty.</returns>
        internal T Dequeue()
        {
            return GetTopItem(true);
        }

        /// <summary>
        /// Get a pointer to the top item without dequeueing it
        /// </summary>
        /// <returns>The top item off the queue, or null if queue is empty.</returns>
        internal T Peek()
        {
            return GetTopItem(false);
        }

        /// <summary>
        /// Finds the top item in the queue. If passed in argument is true the top value is dequeued
        /// </summary>
        /// <returns>The top item off the queue, or null if queue is empty.</returns>
        private T GetTopItem(bool dequeue)
        {
            Queue<T> readingQueue = GetReadingQueue();
            T item = default(T);

            if (readingQueue.Count > 0)
            {
                item = dequeue ? readingQueue.Dequeue() : readingQueue.Peek();
            }

            // if the reading queue is now empty
            if (readingQueue.Count == 0)
            {
                // wait until the current writer (if any) is done with the posting queue
                lock (queueLock)
                {
                    // confirm both queues are now empty -- this check is important because
                    // a writer may have added to the queue while we were waiting for a lock
                    if ((backingQueueA.Count == 0) && (backingQueueB.Count == 0))
                    {
                        // signal there are no more items to read
                        queueReadyEvent.Reset();

                        if (queueEmptyEvent != null)
                        {
                            queueEmptyEvent.Set();
                        }
                    }
                }
            }

            return item;
        }

        /// <summary>
        /// Returns one of the two behind-the-scenes queues that is not being
        /// used for posting into.
        /// </summary>
        /// <returns>The queue to read from.</returns>
        private Queue<T> GetReadingQueue()
        {
            Queue<T> readingQueue = backingQueueB;

            // figure out the current reading queue
            if (queue == backingQueueB)
            {
                readingQueue = backingQueueA;
            }

            // if the current reading queue is non-empty, return it; otherwise, if
            // the current posting queue is non-empty, swap it for the reading queue
            // and return it instead; if both are empty, just return the current reading
            // queue -- this logic allows us to lock only when strictly necessary
            if (readingQueue.Count == 0)
            {
                lock (queueLock)
                {
                    if (queue.Count > 0)
                    {
                        Queue<T> postingQueue = queue;
                        queue = readingQueue;
                        readingQueue = postingQueue;
                        
                        writingQueueCount = 0;
                    }
                }
            }

            return readingQueue;
        }

        /// <summary>
        /// Primairly used for unit tests to verify a item is in one of the internal queues
        /// </summary>
        /// <param name="item">Items to check for in the two internal queues</param>
        /// <returns></returns>
        internal bool Contains(T item)
        {
            // The dual queue in general contains an item if the item exists 
            // in one or even both of the backing queues
            return backingQueueA.Contains(item) || backingQueueB.Contains(item);
        }
        #endregion

        #region Data

        /// <summary>
        /// This event is set when the queue contains items to read.
        /// </summary>
        private ManualResetEvent queueReadyEvent;

        /// <summary>
        /// This event is set when the queue is empty
        /// </summary>
        private ManualResetEvent queueEmptyEvent;

        /// <summary>
        /// This object protects the posting queue.
        /// </summary>
        private object queueLock;

        /// <summary>
        /// This queue reference serves as the "posting queue". This queue reference
        /// points to one of the two queues that are swapped behind the scenes.
        /// </summary>
        private Queue<T> queue;

        /// <summary>
        /// One of the two behind-the-scenes queues that are swapped.
        /// </summary>
        private Queue<T> backingQueueA;

        /// <summary>
        /// One of the two behind-the-scenes queues that are swapped.
        /// </summary>
        private Queue<T> backingQueueB;

        /// <summary>
        /// Count of the current writer queue - we only own the reader queue in Count so we have to keep 
        /// the count for the writer queue separately.
        /// </summary>
        private int writingQueueCount;

        #endregion
    }
}
