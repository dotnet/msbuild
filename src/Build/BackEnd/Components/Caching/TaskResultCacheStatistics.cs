// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;

#nullable disable

namespace Microsoft.Build.BackEnd.Components.Caching
{
    internal sealed class TaskResultCacheStatistics : IBuildComponent
    {
        internal const string ShowCommandLineSwitch = "taskcachestats";
        internal const string ResetCommandLineSwitch = "resettaskcachestats";

        private const string StatisticsHeader = "MSBuild task result cache statistics";

        private static readonly StatisticsStore s_statistics = new();

        public void InitializeComponent(IBuildComponentHost host)
        {
        }

        public void ShutdownComponent()
        {
        }

        internal TaskResultCacheStatisticsRequest BeginRequest()
        {
            s_statistics.RecordRequest();
            return new TaskResultCacheStatisticsRequest(s_statistics);
        }

        internal static IBuildComponent CreateComponent(BuildComponentType type)
        {
            Assumed.Equal(
                type,
                BuildComponentType.TaskResultCacheStatistics,
                $"Cannot create components of type {type}");
            return new TaskResultCacheStatistics();
        }

        internal static TaskResultCacheStatisticsSnapshot GetSnapshot() => s_statistics.GetSnapshot();

        internal static TaskResultCacheStatisticsSnapshot GetAndResetSnapshot() => s_statistics.GetAndResetSnapshot();

        internal static void Merge(TaskResultCacheStatisticsSnapshot snapshot) => s_statistics.Merge(snapshot);

        internal static void Reset() => s_statistics.GetAndResetSnapshot();

        internal static string FormatSnapshot(TaskResultCacheStatisticsSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.AppendLine(StatisticsHeader);
            AppendCounter(builder, "requests", snapshot.Requests);
            AppendCounter(builder, "cache_requests", snapshot.CacheRequests);
            AppendCounter(builder, "hits", snapshot.Hits);
            AppendCounter(builder, "misses", snapshot.Misses);
            AppendCounter(builder, "ineligible", snapshot.Ineligible);
            AppendCounter(builder, "errors", snapshot.Errors);
            AppendCounter(builder, "stores", snapshot.Stores);
            AppendCounter(builder, "store_errors", snapshot.StoreErrors);
            builder.Append("hit_rate\t");
            builder.AppendLine(snapshot.HitRate.ToString("P2", CultureInfo.InvariantCulture));
            AppendAverageDuration(builder, "average_hit_time", snapshot.AverageHitTime);
            AppendAverageDuration(builder, "average_miss_time", snapshot.AverageMissTime);
            return builder.ToString();
        }

        internal static bool IsStatisticsCommand(string[] commandLine, out bool reset)
        {
            reset = false;
            if (commandLine?.Length != 2)
            {
                return false;
            }

            ReadOnlySpan<char> argument = commandLine[1].AsSpan();
            while (!argument.IsEmpty && argument[0] is '-' or '/')
            {
                argument = argument.Slice(1);
            }

            if (argument.Equals(ShowCommandLineSwitch, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (argument.Equals(ResetCommandLineSwitch, StringComparison.OrdinalIgnoreCase))
            {
                reset = true;
                return true;
            }

            return false;
        }

        private static void AppendCounter(StringBuilder builder, string name, long value)
        {
            builder.Append(name);
            builder.Append('\t');
            builder.AppendLine(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendAverageDuration(StringBuilder builder, string name, TimeSpan value)
        {
            builder.Append(name);
            builder.Append('\t');
            builder.Append(value.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
            builder.AppendLine(" ms");
        }

        internal sealed class StatisticsStore
        {
            private long _requests;
            private long _hits;
            private long _misses;
            private long _ineligible;
            private long _errors;
            private long _stores;
            private long _storeErrors;
            private long _hitDurationTicks;
            private long _missDurationTicks;

            internal void RecordRequest() => Interlocked.Increment(ref _requests);

            internal void RecordHit() => Interlocked.Increment(ref _hits);

            internal void RecordMiss() => Interlocked.Increment(ref _misses);

            internal void RecordIneligible() => Interlocked.Increment(ref _ineligible);

            internal void RecordError() => Interlocked.Increment(ref _errors);

            internal void RecordStore() => Interlocked.Increment(ref _stores);

            internal void RecordStoreError() => Interlocked.Increment(ref _storeErrors);

            internal void RecordHitDuration(long elapsedStopwatchTicks) =>
                Interlocked.Add(ref _hitDurationTicks, ToTimeSpanTicks(elapsedStopwatchTicks));

            internal void RecordMissDuration(long elapsedStopwatchTicks) =>
                Interlocked.Add(ref _missDurationTicks, ToTimeSpanTicks(elapsedStopwatchTicks));

            internal TaskResultCacheStatisticsSnapshot GetSnapshot()
            {
                return new TaskResultCacheStatisticsSnapshot(
                    Interlocked.Read(ref _requests),
                    Interlocked.Read(ref _hits),
                    Interlocked.Read(ref _misses),
                    Interlocked.Read(ref _ineligible),
                    Interlocked.Read(ref _errors),
                    Interlocked.Read(ref _stores),
                    Interlocked.Read(ref _storeErrors),
                    Interlocked.Read(ref _hitDurationTicks),
                    Interlocked.Read(ref _missDurationTicks));
            }

            internal TaskResultCacheStatisticsSnapshot GetAndResetSnapshot()
            {
                return new TaskResultCacheStatisticsSnapshot(
                    Interlocked.Exchange(ref _requests, 0),
                    Interlocked.Exchange(ref _hits, 0),
                    Interlocked.Exchange(ref _misses, 0),
                    Interlocked.Exchange(ref _ineligible, 0),
                    Interlocked.Exchange(ref _errors, 0),
                    Interlocked.Exchange(ref _stores, 0),
                    Interlocked.Exchange(ref _storeErrors, 0),
                    Interlocked.Exchange(ref _hitDurationTicks, 0),
                    Interlocked.Exchange(ref _missDurationTicks, 0));
            }

            internal void Merge(TaskResultCacheStatisticsSnapshot snapshot)
            {
                Interlocked.Add(ref _requests, snapshot.Requests);
                Interlocked.Add(ref _hits, snapshot.Hits);
                Interlocked.Add(ref _misses, snapshot.Misses);
                Interlocked.Add(ref _ineligible, snapshot.Ineligible);
                Interlocked.Add(ref _errors, snapshot.Errors);
                Interlocked.Add(ref _stores, snapshot.Stores);
                Interlocked.Add(ref _storeErrors, snapshot.StoreErrors);
                Interlocked.Add(ref _hitDurationTicks, snapshot.HitDurationTicks);
                Interlocked.Add(ref _missDurationTicks, snapshot.MissDurationTicks);
            }

            private static long ToTimeSpanTicks(long elapsedStopwatchTicks)
            {
                return (long)(elapsedStopwatchTicks *
                    ((double)TimeSpan.TicksPerSecond / Stopwatch.Frequency));
            }
        }
    }

    internal sealed class TaskResultCacheStatisticsRequest
    {
        private readonly TaskResultCacheStatistics.StatisticsStore _statistics;

        internal TaskResultCacheStatisticsRequest(TaskResultCacheStatistics.StatisticsStore statistics)
        {
            _statistics = statistics;
        }

        internal void RecordHit() => _statistics.RecordHit();

        internal void RecordMiss() => _statistics.RecordMiss();

        internal void RecordIneligible() => _statistics.RecordIneligible();

        internal void RecordError() => _statistics.RecordError();

        internal void RecordStore() => _statistics.RecordStore();

        internal void RecordStoreError() => _statistics.RecordStoreError();

        internal void RecordHitDuration(long elapsedStopwatchTicks) =>
            _statistics.RecordHitDuration(elapsedStopwatchTicks);

        internal void RecordMissDuration(long elapsedStopwatchTicks) =>
            _statistics.RecordMissDuration(elapsedStopwatchTicks);
    }

    internal readonly struct TaskResultCacheStatisticsSnapshot
    {
        internal TaskResultCacheStatisticsSnapshot(
            long requests,
            long hits,
            long misses,
            long ineligible,
            long errors,
            long stores,
            long storeErrors,
            long hitDurationTicks,
            long missDurationTicks)
        {
            Requests = requests;
            Hits = hits;
            Misses = misses;
            Ineligible = ineligible;
            Errors = errors;
            Stores = stores;
            StoreErrors = storeErrors;
            HitDurationTicks = hitDurationTicks;
            MissDurationTicks = missDurationTicks;
        }

        internal long Requests { get; }

        internal long CacheRequests => Hits + Misses;

        internal long Hits { get; }

        internal long Misses { get; }

        internal long Ineligible { get; }

        internal long Errors { get; }

        internal long Stores { get; }

        internal long StoreErrors { get; }

        internal long HitDurationTicks { get; }

        internal long MissDurationTicks { get; }

        internal TimeSpan AverageHitTime =>
            Hits == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(HitDurationTicks / Hits);

        internal TimeSpan AverageMissTime =>
            Misses == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(MissDurationTicks / Misses);

        internal double HitRate => CacheRequests == 0 ? 0 : (double)Hits / CacheRequests;

        internal bool IsEmpty =>
            Requests == 0 &&
            Hits == 0 &&
            Misses == 0 &&
            Ineligible == 0 &&
            Errors == 0 &&
            Stores == 0 &&
            StoreErrors == 0 &&
            HitDurationTicks == 0 &&
            MissDurationTicks == 0;
    }
}
