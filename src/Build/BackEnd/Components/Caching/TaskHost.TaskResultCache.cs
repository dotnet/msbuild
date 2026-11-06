// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using Microsoft.Build.BackEnd.Components.Caching;
using Microsoft.Build.Framework;

#nullable enable

namespace Microsoft.Build.BackEnd
{
    internal partial class TaskHost
    {
        private TaskResultCacheEventCollector? _taskResultCacheEventCollector;

        internal IDisposable BeginTaskResultCacheEventCapture(
            TaskResultCacheEventCollector eventCollector)
        {
            lock (_callbackMonitor)
            {
                Assumed.Null(_taskResultCacheEventCollector);
                _taskResultCacheEventCollector = eventCollector;
            }

            return new TaskResultCacheEventCapture(this, eventCollector);
        }

        private void CaptureTaskResultCacheEvent(BuildEventArgs buildEvent)
        {
            TaskResultCacheEventCollector? eventCollector =
                _taskResultCacheEventCollector;
            if (eventCollector is null)
            {
                return;
            }

            switch (buildEvent)
            {
                case BuildMessageEventArgs message:
                    eventCollector.Record(message);
                    break;

                case BuildWarningEventArgs warning:
                    eventCollector.Record(warning);
                    break;

                default:
                    eventCollector.MarkUnsupported();
                    break;
            }
        }

        private void MarkTaskResultCacheEventCaptureUnsupported()
        {
            _taskResultCacheEventCollector?.MarkUnsupported();
        }

        private void EndTaskResultCacheEventCapture(
            TaskResultCacheEventCollector eventCollector)
        {
            lock (_callbackMonitor)
            {
                Assumed.True(ReferenceEquals(
                    eventCollector,
                    _taskResultCacheEventCollector));
                _taskResultCacheEventCollector = null;
            }
        }

        private sealed class TaskResultCacheEventCapture(
            TaskHost taskHost,
            TaskResultCacheEventCollector eventCollector) : IDisposable
        {
            private TaskHost? _taskHost = taskHost;

            public void Dispose()
            {
                TaskHost? currentTaskHost =
                    Interlocked.Exchange(ref _taskHost, null);
                currentTaskHost?.EndTaskResultCacheEventCapture(eventCollector);
            }
        }
    }
}
