// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Testing.Abstractions;
using System;

namespace Microsoft.DotNet.Tools.Test
{
    public class TestMessagesCollection : ITestMessagesCollection
    {
        private readonly ManualResetEventSlim _terminateWaitHandle;
        private readonly BlockingCollection<Message> _readQueue;

        public TestMessagesCollection()
        {
            _readQueue = new BlockingCollection<Message>();
            _terminateWaitHandle = new ManualResetEventSlim();
        }

        public void Drain()
        {
            _terminateWaitHandle.Set();
            _readQueue.CompleteAdding();
            DrainQueue();
        }

        public void Add(Message message)
        {
            _readQueue.Add(message);
        }

        public bool TryTake(out Message message)
        {
            message = null;
            try
            {
                message = _readQueue.Take();
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            if (_terminateWaitHandle.Wait(TimeSpan.FromSeconds(10)))
            {
                TestHostTracing.Source.TraceInformation("[ReportingChannel]: Received TestSession:Terminate from test host");
            }
            else
            {
                TestHostTracing.Source.TraceEvent(
                    TraceEventType.Error,
                    0,
                    "[ReportingChannel]: Timed out waiting for aTestSession:Terminate from test host");
            }
        }

        private void DrainQueue()
        {
            Message message;
            while (_readQueue.TryTake(out message, millisecondsTimeout: 1))
            {
            }
        }
    }
}
