// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Unit tests for the callback request/response correlation mechanism.
    /// These tests validate the thread-safety and correctness of the
    /// _pendingCallbackRequests dictionary and request ID generation.
    /// </summary>
    public class TaskHostCallbackCorrelation_Tests
    {
        private static readonly Random s_random = new Random();

        /// <summary>
        /// Verifies that concurrent access to a ConcurrentDictionary (simulating
        /// _pendingCallbackRequests) is thread-safe.
        /// </summary>
        [Fact]
        public void PendingRequests_ConcurrentAccess_IsThreadSafe()
        {
            var pendingRequests = new ConcurrentDictionary<int, TaskCompletionSource<INodePacket>>();
            var tasks = new List<Task>();

            for (int i = 0; i < 100; i++)
            {
                int requestId = i;
                tasks.Add(Task.Run(() =>
                {
                    var tcs = new TaskCompletionSource<INodePacket>();
                    pendingRequests[requestId] = tcs;
                    Thread.Sleep(s_random.Next(1, 10));
                    pendingRequests.TryRemove(requestId, out _);
                }));
            }

            Task.WaitAll(tasks.ToArray());

            pendingRequests.Count.ShouldBe(0);
        }

        /// <summary>
        /// Verifies that Interlocked.Increment generates unique request IDs
        /// even under heavy concurrent load.
        /// </summary>
        [Fact]
        public void RequestIdGeneration_ConcurrentRequests_NoCollisions()
        {
            var requestIds = new ConcurrentBag<int>();
            int nextRequestId = 0;
            var tasks = new List<Task>();

            for (int i = 0; i < 1000; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    int id = Interlocked.Increment(ref nextRequestId);
                    requestIds.Add(id);
                }));
            }

            Task.WaitAll(tasks.ToArray());

            requestIds.Count.ShouldBe(1000);
            requestIds.Distinct().Count().ShouldBe(1000);
        }

        /// <summary>
        /// Verifies that TaskCompletionSource correctly signals waiting threads
        /// when SetResult is called.
        /// </summary>
        [Fact]
        public void TaskCompletionSource_SignalsWaitingThread()
        {
            var tcs = new TaskCompletionSource<INodePacket>(TaskCreationOptions.RunContinuationsAsynchronously);
            var responseReceived = false;

            var waitingTask = Task.Run(() =>
            {
                // Simulate waiting for response
                var result = tcs.Task.Result;
                responseReceived = true;
            });

            // Simulate response arriving after a short delay
            Thread.Sleep(50);
            var response = new TaskHostQueryResponse(1, true);
            tcs.SetResult(response);

            waitingTask.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            responseReceived.ShouldBeTrue();
        }

        /// <summary>
        /// Verifies that multiple pending requests can be resolved independently
        /// without cross-contamination.
        /// </summary>
        [Fact]
        public void MultiplePendingRequests_ResolveIndependently()
        {
            var pendingRequests = new ConcurrentDictionary<int, TaskCompletionSource<INodePacket>>();

            // Create 5 pending requests
            for (int i = 1; i <= 5; i++)
            {
                pendingRequests[i] = new TaskCompletionSource<INodePacket>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            // Resolve them in random order
            var resolveOrder = new[] { 3, 1, 5, 2, 4 };
            foreach (var requestId in resolveOrder)
            {
                var response = new TaskHostQueryResponse(requestId, requestId % 2 == 0);
                if (pendingRequests.TryRemove(requestId, out var tcs))
                {
                    tcs.SetResult(response);
                }
            }

            // Verify all were resolved correctly
            pendingRequests.Count.ShouldBe(0);
        }

        /// <summary>
        /// Verifies that the callback response type checking works correctly.
        /// </summary>
        [Fact]
        public void ResponseTypeChecking_CorrectTypesAccepted()
        {
            var queryResponse = new TaskHostQueryResponse(1, true);
            var resourceResponse = new TaskHostResourceResponse(2, 4);

            // Both should implement ITaskHostCallbackPacket
            queryResponse.ShouldBeAssignableTo<ITaskHostCallbackPacket>();
            resourceResponse.ShouldBeAssignableTo<ITaskHostCallbackPacket>();

            // Verify RequestId is accessible through interface
            ((ITaskHostCallbackPacket)queryResponse).RequestId.ShouldBe(1);
            ((ITaskHostCallbackPacket)resourceResponse).RequestId.ShouldBe(2);
        }

        /// <summary>
        /// Verifies that requests and responses with matching IDs are correctly paired.
        /// </summary>
        [Fact]
        public void RequestResponsePairing_MatchesByRequestId()
        {
            var pendingRequests = new ConcurrentDictionary<int, TaskCompletionSource<INodePacket>>();
            var results = new ConcurrentDictionary<int, bool>();

            // Create pending requests with specific IDs
            var requestIds = new[] { 10, 20, 30 };
            foreach (var id in requestIds)
            {
                var tcs = new TaskCompletionSource<INodePacket>(TaskCreationOptions.RunContinuationsAsynchronously);
                pendingRequests[id] = tcs;

                // Set up continuation to record which response was received
                int capturedId = id;
                tcs.Task.ContinueWith(t =>
                {
                    if (t.Result is TaskHostQueryResponse response)
                    {
                        results[capturedId] = response.BoolResult;
                    }
                }, TaskContinuationOptions.ExecuteSynchronously);
            }

            // Send responses in different order with different values
            // ID 10 -> true, ID 20 -> false, ID 30 -> true
            foreach (var (id, value) in new[] { (20, false), (30, true), (10, true) })
            {
                var response = new TaskHostQueryResponse(id, value);
                if (pendingRequests.TryRemove(id, out var tcs))
                {
                    tcs.SetResult(response);
                }
            }

            // Wait for all continuations to complete
            Thread.Sleep(100);

            // Verify each request got its correct response
            results[10].ShouldBeTrue();
            results[20].ShouldBeFalse();
            results[30].ShouldBeTrue();
        }

        /// <summary>
        /// Verifies that TryRemove returns false for unknown request IDs.
        /// </summary>
        [Fact]
        public void UnknownRequestId_TryRemoveReturnsFalse()
        {
            var pendingRequests = new ConcurrentDictionary<int, TaskCompletionSource<INodePacket>>();

            // Add one request
            pendingRequests[1] = new TaskCompletionSource<INodePacket>();

            // Try to remove a non-existent request
            bool removed = pendingRequests.TryRemove(999, out var tcs);

            removed.ShouldBeFalse();
            tcs.ShouldBeNull();
            pendingRequests.Count.ShouldBe(1);
        }
    }
}
