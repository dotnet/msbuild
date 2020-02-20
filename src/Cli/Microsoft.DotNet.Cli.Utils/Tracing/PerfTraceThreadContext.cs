// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.DotNet.Cli.Utils
{
    public class PerfTraceThreadContext
    {
        private readonly int _threadId;

        private TimerDisposable _activeEvent;

        public PerfTraceEvent Root => _activeEvent.CreateEvent();

        public PerfTraceThreadContext(int threadId)
        {
            _activeEvent = new TimerDisposable(this, "Thread", $"{threadId.ToString()}");
            _threadId = threadId;
        }

        public IDisposable CaptureTiming(string instance = "", [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "")
        {
            if(!PerfTrace.Enabled)
            {
                return null;
            }

            var newTimer = new TimerDisposable(this, $"{Path.GetFileNameWithoutExtension(filePath)}:{memberName}", instance);
            var previousTimer = Interlocked.Exchange(ref _activeEvent, newTimer);
            newTimer.Parent = previousTimer;
            return newTimer;
        }

        private void RecordTiming(PerfTraceEvent newEvent, TimerDisposable parent)
        {
            Interlocked.Exchange(ref _activeEvent, parent);
            _activeEvent.Children.Add(newEvent);
        }

        private class TimerDisposable : IDisposable
        {
            private readonly PerfTraceThreadContext _context;
            private string _eventType;
            private string _instance;
            private DateTime _startUtc;
            private Stopwatch _stopwatch = Stopwatch.StartNew();

            public TimerDisposable Parent { get; set; }

            public ConcurrentBag<PerfTraceEvent> Children { get; set; } = new ConcurrentBag<PerfTraceEvent>();

            public TimerDisposable(PerfTraceThreadContext context, string eventType, string instance)
            {
                _context = context;
                _eventType = eventType;
                _instance = instance;
                _startUtc = DateTime.UtcNow;
            }

            public void Dispose()
            {
                _stopwatch.Stop();

                _context.RecordTiming(CreateEvent(), Parent);
            }

            public PerfTraceEvent CreateEvent() => new PerfTraceEvent(_eventType, _instance, Children, _startUtc, _stopwatch.Elapsed);
        }
    }
}
