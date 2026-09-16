// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Threading;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.BackEnd.Components.Caching
{
    internal sealed class TaskResultCacheStatistics : IBuildComponent
    {
        private long _hits;
        private long _misses;
        private long _hitDurationTicks;
        private long _missDurationTicks;

        public void InitializeComponent(IBuildComponentHost host)
        {
        }

        public void ShutdownComponent()
        {
        }

        internal void Record(TaskResultCacheOpenResult result, long elapsedStopwatchTicks)
        {
            switch (result)
            {
                case TaskResultCacheOpenResult.Hit:
                    Interlocked.Increment(ref _hits);
                    Interlocked.Add(ref _hitDurationTicks, elapsedStopwatchTicks);
                    break;

                case TaskResultCacheOpenResult.Miss:
                    Interlocked.Increment(ref _misses);
                    Interlocked.Add(ref _missDurationTicks, elapsedStopwatchTicks);
                    break;
            }
        }

        internal void LogAndReset(ILoggingService loggingService, BuildEventContext buildEventContext)
        {
            long hits = Interlocked.Exchange(ref _hits, 0);
            long misses = Interlocked.Exchange(ref _misses, 0);
            long hitDurationTicks = Interlocked.Exchange(ref _hitDurationTicks, 0);
            long missDurationTicks = Interlocked.Exchange(ref _missDurationTicks, 0);
            long requests = hits + misses;
            if (requests == 0)
            {
                return;
            }

            loggingService.LogComment(
                buildEventContext,
                MessageImportance.Low,
                "TaskResultCacheStatistics",
                hits,
                misses,
                (double)hits / requests,
                ToAverageMilliseconds(hitDurationTicks, hits),
                ToAverageMilliseconds(missDurationTicks, misses));
        }

        internal static IBuildComponent CreateComponent(BuildComponentType type)
        {
            Assumed.Equal(
                type,
                BuildComponentType.TaskResultCacheStatistics,
                $"Cannot create components of type {type}");
            return new TaskResultCacheStatistics();
        }

        private static double ToAverageMilliseconds(long elapsedStopwatchTicks, long count) =>
            count == 0
                ? 0
                : elapsedStopwatchTicks * (1000.0 / Stopwatch.Frequency) / count;
    }
}
