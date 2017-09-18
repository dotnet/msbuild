using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.Build.Shared.Concurrent
{
    // The following class is back-ported from .NET 4.X CoreFX library because
    // MSBuildTaskHost requires 3.5 .NET Framework. Only important methods (Enqueue, TryDequeue) are kept.
    internal class ConcurrentQueue<T>
    {
        // This implementation provides an unbounded, multi-producer multi-consumer queue
        // that supports the standard Enqueue/TryDequeue operations, as well as support for
        // snapshot enumeration (GetEnumerator, ToArray, CopyTo), peeking, and Count/IsEmpty.
        // It is composed of a linked list of bounded ring buffers, each of which has a head
        // and a tail index, isolated from each other to minimize false sharing.  As long as
        // the number of elements in the queue remains less than the size of the current
        // buffer (Segment), no additional allocations are required for enqueued items.  When
        // the number of items exceeds the size of the current segment, the current segment is
        // "frozen" to prevent further enqueues, and a new segment is linked from it and set
        // as the new tail segment for subsequent enqueues.  As old segments are consumed by
        // dequeues, the head reference is updated to point to the segment that dequeuers should
        // try next.  To support snapshot enumeration, segments also support the notion of
        // preserving for observation, whereby they avoid overwriting state as part of dequeues.
        // Any operation that requires a snapshot results in all current segments being
        // both frozen for enqueues and preserved for observation: any new enqueues will go
        // to new segments, and dequeuers will consume from the existing segments but without
        // overwriting the existing data.

        /// <summary>Initial length of the segments used in the queue.</summary>
        private const int InitialSegmentLength = 32;
        /// <summary>
        /// Maximum length of the segments used in the queue.  This is a somewhat arbitrary limit:
        /// larger means that as long as we don't exceed the size, we avoid allocating more segments,
        /// but if we do exceed it, then the segment becomes garbage.
        /// </summary>
        private const int MaxSegmentLength = 1024 * 1024;

        /// <summary>
        /// Lock used to protect cross-segment operations, including any updates to <see cref="_tail"/> or <see cref="_head"/>
        /// and any operations that need to get a consistent view of them.
        /// </summary>
        private object _crossSegmentLock;
        /// <summary>The current tail segment.</summary>
        private volatile Segment _tail;
        /// <summary>The current head segment.</summary>
        private volatile Segment _head;

        static internal object VolatileReader(ref object o) => Thread.VolatileRead(ref o);
        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentQueue{T}"/> class.
        /// </summary>
        public ConcurrentQueue()
        {
            _crossSegmentLock = new object();
            _tail = _head = new Segment(InitialSegmentLength);
        }

        /// <summary>Adds an object to the end of the <see cref="ConcurrentQueue{T}"/>.</summary>
        /// <param name="item">
        /// The object to add to the end of the <see cref="ConcurrentQueue{T}"/>.
        /// The value can be a null reference (Nothing in Visual Basic) for reference types.
        /// </param>
        public void Enqueue(T item)
        {
            // Try to enqueue to the current tail.
            if (!_tail.TryEnqueue(item))
            {
                // If we're unable to, we need to take a slow path that will
                // try to add a new tail segment.
                EnqueueSlow(item);
            }
        }

        /// <summary>Adds to the end of the queue, adding a new segment if necessary.</summary>
        private void EnqueueSlow(T item)
        {
            while (true)
            {
                Segment tail = _tail;

                // Try to append to the existing tail.
                if (tail.TryEnqueue(item))
                {
                    return;
                }

                // If we were unsuccessful, take the lock so that we can compare and manipulate
                // the tail.  Assuming another enqueuer hasn't already added a new segment,
                // do so, then loop around to try enqueueing again.
                lock (_crossSegmentLock)
                {
                    if (tail == _tail)
                    {
                        // Make sure no one else can enqueue to this segment.
                        tail.EnsureFrozenForEnqueues();

                        // We determine the new segment's length based on the old length.
                        // In general, we double the size of the segment, to make it less likely
                        // that we'll need to grow again.  However, if the tail segment is marked
                        // as preserved for observation, something caused us to avoid reusing this
                        // segment, and if that happens a lot and we grow, we'll end up allocating
                        // lots of wasted space.  As such, in such situations we reset back to the
                        // initial segment length; if these observations are happening frequently,
                        // this will help to avoid wasted memory, and if they're not, we'll
                        // relatively quickly grow again to a larger size.
                        int nextSize = tail._preservedForObservation != 0 ? InitialSegmentLength : Math.Min(tail.Capacity * 2, MaxSegmentLength);
                        var newTail = new Segment(nextSize);

                        // Hook up the new tail.
                        tail._nextSegment = newTail;
                        _tail = newTail;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to remove and return the object at the beginning of the <see
        /// cref="ConcurrentQueue{T}"/>.
        /// </summary>
        /// <param name="result">
        /// When this method returns, if the operation was successful, <paramref name="result"/> contains the
        /// object removed. If no object was available to be removed, the value is unspecified.
        /// </param>
        /// <returns>
        /// true if an element was removed and returned from the beginning of the
        /// <see cref="ConcurrentQueue{T}"/> successfully; otherwise, false.
        /// </returns>
        public bool TryDequeue(out T result) =>
            _head.TryDequeue(out result) || // fast-path that operates just on the head segment
            TryDequeueSlow(out result); // slow path that needs to fix up segments

        /// <summary>Tries to dequeue an item, removing empty segments as needed.</summary>
        private bool TryDequeueSlow(out T item)
        {
            while (true)
            {
                // Get the current head
                Segment head = _head;

                // Try to take.  If we're successful, we're done.
                if (head.TryDequeue(out item))
                {
                    return true;
                }

                // Check to see whether this segment is the last. If it is, we can consider
                // this to be a moment-in-time empty condition (even though between the TryDequeue
                // check and this check, another item could have arrived).
                if (head._nextSegment == null)
                {
                    item = default(T);
                    return false;
                }

                // At this point we know that head.Next != null, which means
                // this segment has been frozen for additional enqueues. But between
                // the time that we ran TryDequeue and checked for a next segment,
                // another item could have been added.  Try to dequeue one more time
                // to confirm that the segment is indeed empty.
                Debug.Assert(head._frozenForEnqueues);
                if (head.TryDequeue(out item))
                {
                    return true;
                }

                // This segment is frozen (nothing more can be added) and empty (nothing is in it).
                // Update head to point to the next segment in the list, assuming no one's beat us to it.
                lock (_crossSegmentLock)
                {
                    if (head == _head)
                    {
                        _head = head._nextSegment;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to return an object from the beginning of the <see cref="ConcurrentQueue{T}"/>
        /// without removing it.
        /// </summary>
        /// <param name="result">
        /// When this method returns, <paramref name="result"/> contains an object from
        /// the beginning of the <see cref="Concurrent.ConcurrentQueue{T}"/> or default(T)
        /// if the operation failed.
        /// </param>
        /// <returns>true if and object was returned successfully; otherwise, false.</returns>
        /// <remarks>
        /// For determining whether the collection contains any items, use of the <see cref="IsEmpty"/>
        /// property is recommended rather than peeking.
        /// </remarks>
        public bool TryPeek(out T result) => TryPeek(out result, resultUsed: true);

        /// <summary>Attempts to retrieve the value for the first element in the queue.</summary>
        /// <param name="result">The value of the first element, if found.</param>
        /// <param name="resultUsed">true if the result is neede; otherwise false if only the true/false outcome is needed.</param>
        /// <returns>true if an element was found; otherwise, false.</returns>
        private bool TryPeek(out T result, bool resultUsed)
        {
            // Starting with the head segment, look through all of the segments
            // for the first one we can find that's not empty.
            Segment s = _head;
            while (true)
            {
                // Grab the next segment from this one, before we peek.
                // This is to be able to see whether the value has changed
                // during the peek operation.
                Thread.MemoryBarrier();
                Segment next = s._nextSegment;

                // Peek at the segment.  If we find an element, we're done.
                if (s.TryPeek(out result, resultUsed))
                {
                    return true;
                }

                // The current segment was empty at the moment we checked.

                if (next != null)
                {
                    // If prior to the peek there was already a next segment, then
                    // during the peek no additional items could have been enqueued
                    // to it and we can just move on to check the next segment.
                    Debug.Assert(next == s._nextSegment);
                    s = next;
                }
                else
                {
                    Thread.MemoryBarrier();
                    if (s._nextSegment == null)
                    {
                        // The next segment is null.  Nothing more to peek at.
                        break;
                    }
                }

                // The next segment was null before we peeked but non-null after.
                // That means either when we peeked the first segment had
                // already been frozen but the new segment not yet added,
                // or that the first segment was empty and between the time
                // that we peeked and then checked _nextSegment, so many items
                // were enqueued that we filled the first segment and went
                // into the next.  Since we need to peek in order, we simply
                // loop around again to peek on the same segment.  The next
                // time around on this segment we'll then either successfully
                // peek or we'll find that next was non-null before peeking,
                // and we'll traverse to that segment.
            }

            result = default(T);
            return false;
        }

        /// <summary>
        /// Provides a multi-producer, multi-consumer thread-safe bounded segment.  When the queue is full,
        /// enqueues fail and return false.  When the queue is empty, dequeues fail and return null.
        /// These segments are linked together to form the unbounded <see cref="ConcurrentQueue{T}"/>. 
        /// </summary>
        [DebuggerDisplay("Capacity = {Capacity}")]
        private sealed class Segment
        {
            // Segment design is inspired by the algorithm outlined at:
            // http://www.1024cores.net/home/lock-free-algorithms/queues/bounded-mpmc-queue

            /// <summary>The array of items in this queue.  Each slot contains the item in that slot and its "sequence number".</summary>
            internal readonly Slot[] _slots;
            /// <summary>Mask for quickly accessing a position within the queue's array.</summary>
            internal readonly int _slotsMask;
            /// <summary>The head and tail positions, with padding to help avoid false sharing contention.</summary>
            /// <remarks>Dequeuing happens from the head, enqueuing happens at the tail.</remarks>
            internal PaddedHeadAndTail _headAndTail; // mutable struct: do not make this readonly

            /// <summary>Indicates whether the segment has been marked such that dequeues don't overwrite the removed data.</summary>
            internal byte _preservedForObservation;
            /// <summary>Indicates whether the segment has been marked such that no additional items may be enqueued.</summary>
            internal bool _frozenForEnqueues;
            /// <summary>The segment following this one in the queue, or null if this segment is the last in the queue.</summary>
            internal Segment _nextSegment;

            /// <summary>Creates the segment.</summary>
            /// <param name="boundedLength">
            /// The maximum number of elements the segment can contain.  Must be a power of 2.
            /// </param>
            public Segment(int boundedLength)
            {
                // Validate the length
                Debug.Assert(boundedLength >= 2, $"Must be >= 2, got {boundedLength}");
                Debug.Assert((boundedLength & (boundedLength - 1)) == 0, $"Must be a power of 2, got {boundedLength}");

                // Initialize the slots and the mask.  The mask is used as a way of quickly doing "% _slots.Length",
                // instead letting us do "& _slotsMask".
                _slots = new Slot[boundedLength];
                _slotsMask = boundedLength - 1;

                // Initialize the sequence number for each slot.  The sequence number provides a ticket that
                // allows dequeuers to know whether they can dequeue and enqueuers to know whether they can
                // enqueue.  An enqueuer at position N can enqueue when the sequence number is N, and a dequeuer
                // for position N can dequeue when the sequence number is N + 1.  When an enqueuer is done writing
                // at position N, it sets the sequence number to N + 1 so that a dequeuer will be able to dequeue,
                // and when a dequeuer is done dequeueing at position N, it sets the sequence number to N + _slots.Length,
                // so that when an enqueuer loops around the slots, it'll find that the sequence number at
                // position N is N.  This also means that when an enqueuer finds that at position N the sequence
                // number is < N, there is still a value in that slot, i.e. the segment is full, and when a
                // dequeuer finds that the value in a slot is < N + 1, there is nothing currently available to
                // dequeue. (It is possible for multiple enqueuers to enqueue concurrently, writing into
                // subsequent slots, and to have the first enqueuer take longer, so that the slots for 1, 2, 3, etc.
                // may have values, but the 0th slot may still be being filled... in that case, TryDequeue will
                // return false.)
                for (int i = 0; i < _slots.Length; i++)
                {
                    _slots[i].SequenceNumber = i;
                }
            }

            /// <summary>Gets the number of elements this segment can store.</summary>
            internal int Capacity => _slots.Length;

            /// <summary>Gets the "freeze offset" for this segment.</summary>
            internal int FreezeOffset => _slots.Length * 2;

            /// <summary>
            /// Ensures that the segment will not accept any subsequent enqueues that aren't already underway.
            /// </summary>
            /// <remarks>
            /// When we mark a segment as being frozen for additional enqueues,
            /// we set the <see cref="_frozenForEnqueues"/> bool, but that's mostly
            /// as a small helper to avoid marking it twice.  The real marking comes
            /// by modifying the Tail for the segment, increasing it by this
            /// <see cref="FreezeOffset"/>.  This effectively knocks it off the
            /// sequence expected by future enqueuers, such that any additional enqueuer
            /// will be unable to enqueue due to it not lining up with the expected
            /// sequence numbers.  This value is chosen specially so that Tail will grow
            /// to a value that maps to the same slot but that won't be confused with
            /// any other enqueue/dequeue sequence number.
            /// </remarks>
            internal void EnsureFrozenForEnqueues() // must only be called while queue's segment lock is held
            {
                if (!_frozenForEnqueues) // flag used to ensure we don't increase the Tail more than once if frozen more than once
                {
                    _frozenForEnqueues = true;

                    // Increase the tail by FreezeOffset, spinning until we're successful in doing so.
                    while (true)
                    {
                        int tail = Thread.VolatileRead(ref _headAndTail.Tail);
                        if (Interlocked.CompareExchange(ref _headAndTail.Tail, tail + FreezeOffset, tail) == tail)
                        {
                            break;
                        }
                        Thread.SpinWait(1);
                    }
                }
            }

            /// <summary>Tries to dequeue an element from the queue.</summary>
            public bool TryDequeue(out T item)
            {
                // Loop in case of contention...
                while (true)
                {
                    // Get the head at which to try to dequeue.
                    int currentHead = Thread.VolatileRead(ref _headAndTail.Head);
                    int slotsIndex = currentHead & _slotsMask;

                    // Read the sequence number for the head position.
                    int sequenceNumber = Thread.VolatileRead(ref _slots[slotsIndex].SequenceNumber);

                    // We can dequeue from this slot if it's been filled by an enqueuer, which
                    // would have left the sequence number at pos+1.
                    int diff = sequenceNumber - (currentHead + 1);
                    if (diff == 0)
                    {
                        // We may be racing with other dequeuers.  Try to reserve the slot by incrementing
                        // the head.  Once we've done that, no one else will be able to read from this slot,
                        // and no enqueuer will be able to read from this slot until we've written the new
                        // sequence number. WARNING: The next few lines are not reliable on a runtime that
                        // supports thread aborts. If a thread abort were to sneak in after the CompareExchange
                        // but before the Volatile.Write, enqueuers trying to enqueue into this slot would
                        // spin indefinitely.  If this implementation is ever used on such a platform, this
                        // if block should be wrapped in a finally / prepared region.
                        if (Interlocked.CompareExchange(ref _headAndTail.Head, currentHead + 1, currentHead) == currentHead)
                        {
                            // Successfully reserved the slot.  Note that after the above CompareExchange, other threads
                            // trying to dequeue from this slot will end up spinning until we do the subsequent Write.
                            item = _slots[slotsIndex].Item;
                            if (Thread.VolatileRead(ref _preservedForObservation) == 0)
                            {
                                // If we're preserving, though, we don't zero out the slot, as we need it for
                                // enumerations, peeking, ToArray, etc.  And we don't update the sequence number,
                                // so that an enqueuer will see it as full and be forced to move to a new segment.
                                _slots[slotsIndex].Item = default(T);
                                Thread.VolatileWrite(ref _slots[slotsIndex].SequenceNumber, currentHead + _slots.Length);
                            }
                            return true;
                        }
                    }
                    else if (diff < 0)
                    {
                        // The sequence number was less than what we needed, which means this slot doesn't
                        // yet contain a value we can dequeue, i.e. the segment is empty.  Technically it's
                        // possible that multiple enqueuers could have written concurrently, with those
                        // getting later slots actually finishing first, so there could be elements after
                        // this one that are available, but we need to dequeue in order.  So before declaring
                        // failure and that the segment is empty, we check the tail to see if we're actually
                        // empty or if we're just waiting for items in flight or after this one to become available.
                        bool frozen = _frozenForEnqueues;
                        int currentTail = Thread.VolatileRead(ref _headAndTail.Tail);
                        if (currentTail - currentHead <= 0 || (frozen && (currentTail - FreezeOffset - currentHead <= 0)))
                        {
                            item = default(T);
                            return false;
                        }

                        // It's possible it could have become frozen after we checked _frozenForEnqueues
                        // and before reading the tail.  That's ok: in that rare race condition, we just
                        // loop around again.
                    }

                    // Lost a race. Spin a bit, then try again.
                    Thread.SpinWait(1);
                }
            }

            /// <summary>Tries to peek at an element from the queue, without removing it.</summary>
            public bool TryPeek(out T result, bool resultUsed)
            {
                if (resultUsed)
                {
                    // In order to ensure we don't get a torn read on the value, we mark the segment
                    // as preserving for observation.  Additional items can still be enqueued to this
                    // segment, but no space will be freed during dequeues, such that the segment will
                    // no longer be reusable.
                    _preservedForObservation = 1;
                    Thread.MemoryBarrier();
                }

                // Loop in case of contention...
                while (true)
                {
                    // Get the head at which to try to peek.
                    int currentHead = Thread.VolatileRead(ref _headAndTail.Head);
                    int slotsIndex = currentHead & _slotsMask;

                    // Read the sequence number for the head position.
                    int sequenceNumber = Thread.VolatileRead(ref _slots[slotsIndex].SequenceNumber);

                    // We can peek from this slot if it's been filled by an enqueuer, which
                    // would have left the sequence number at pos+1.
                    int diff = sequenceNumber - (currentHead + 1);
                    if (diff == 0)
                    {
                        result = resultUsed ? _slots[slotsIndex].Item : default(T);
                        return true;
                    }
                    else if (diff < 0)
                    {
                        // The sequence number was less than what we needed, which means this slot doesn't
                        // yet contain a value we can peek, i.e. the segment is empty.  Technically it's
                        // possible that multiple enqueuers could have written concurrently, with those
                        // getting later slots actually finishing first, so there could be elements after
                        // this one that are available, but we need to peek in order.  So before declaring
                        // failure and that the segment is empty, we check the tail to see if we're actually
                        // empty or if we're just waiting for items in flight or after this one to become available.
                        bool frozen = _frozenForEnqueues;
                        int currentTail = Thread.VolatileRead(ref _headAndTail.Tail);
                        if (currentTail - currentHead <= 0 || (frozen && (currentTail - FreezeOffset - currentHead <= 0)))
                        {
                            result = default(T);
                            return false;
                        }

                        // It's possible it could have become frozen after we checked _frozenForEnqueues
                        // and before reading the tail.  That's ok: in that rare race condition, we just
                        // loop around again.
                    }

                    // Lost a race. Spin a bit, then try again.
                    Thread.SpinWait(1);
                }
            }

            /// <summary>
            /// Attempts to enqueue the item.  If successful, the item will be stored
            /// in the queue and true will be returned; otherwise, the item won't be stored, and false
            /// will be returned.
            /// </summary>
            public bool TryEnqueue(T item)
            {
                // Loop in case of contention...
                while (true)
                {
                    // Get the tail at which to try to return.
                    int currentTail = Thread.VolatileRead(ref _headAndTail.Tail);
                    int slotsIndex = currentTail & _slotsMask;

                    // Read the sequence number for the tail position.
                    int sequenceNumber = Thread.VolatileRead(ref _slots[slotsIndex].SequenceNumber);

                    // The slot is empty and ready for us to enqueue into it if its sequence
                    // number matches the slot.
                    int diff = sequenceNumber - currentTail;
                    if (diff == 0)
                    {
                        // We may be racing with other enqueuers.  Try to reserve the slot by incrementing
                        // the tail.  Once we've done that, no one else will be able to write to this slot,
                        // and no dequeuer will be able to read from this slot until we've written the new
                        // sequence number. WARNING: The next few lines are not reliable on a runtime that
                        // supports thread aborts. If a thread abort were to sneak in after the CompareExchange
                        // but before the Volatile.Write, other threads will spin trying to access this slot.
                        // If this implementation is ever used on such a platform, this if block should be
                        // wrapped in a finally / prepared region.
                        if (Interlocked.CompareExchange(ref _headAndTail.Tail, currentTail + 1, currentTail) == currentTail)
                        {
                            // Successfully reserved the slot.  Note that after the above CompareExchange, other threads
                            // trying to return will end up spinning until we do the subsequent Write.
                            _slots[slotsIndex].Item = item;
                            Thread.VolatileWrite(ref _slots[slotsIndex].SequenceNumber, currentTail + 1);
                            return true;
                        }
                    }
                    else if (diff < 0)
                    {
                        // The sequence number was less than what we needed, which means this slot still
                        // contains a value, i.e. the segment is full.  Technically it's possible that multiple
                        // dequeuers could have read concurrently, with those getting later slots actually
                        // finishing first, so there could be spaces after this one that are available, but
                        // we need to enqueue in order.
                        return false;
                    }

                    // Lost a race. Spin a bit, then try again.
                    Thread.SpinWait(1);
                }
            }

            /// <summary>Represents a slot in the queue.</summary>
            [StructLayout(LayoutKind.Auto)]
            [DebuggerDisplay("Item = {Item}, SequenceNumber = {SequenceNumber}")]
            internal struct Slot
            {
                /// <summary>The item.</summary>
                public T Item;
                /// <summary>The sequence number for this slot, used to synchronize between enqueuers and dequeuers.</summary>
                public int SequenceNumber;
            }
        }
    }
    /// <summary>Padded head and tail indices, to avoid false sharing between producers and consumers.</summary>
    [DebuggerDisplay("Head = {Head}, Tail = {Tail}")]
    [StructLayout(LayoutKind.Explicit, Size = 192)] // padding before/between/after fields based on typical cache line size of 64
    internal struct PaddedHeadAndTail
    {
        [FieldOffset(64)] public int Head;
        [FieldOffset(128)] public int Tail;
    }
}
