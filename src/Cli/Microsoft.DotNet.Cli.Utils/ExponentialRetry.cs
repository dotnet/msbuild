// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils
{
    public static class ExponentialRetry
    {
        public static IEnumerable<TimeSpan> Intervals
        {
            get
            {
                yield return TimeSpan.FromSeconds(0); // first retry immediately
                var seconds = 5;
                while (true)
                {
                    yield return TimeSpan.FromSeconds(seconds);
                    seconds *= 10;
                }
            }
        }

        public static IEnumerable<TimeSpan> TestingIntervals
        {
            get
            {
                while (true)
                {
                    yield return TimeSpan.FromSeconds(0);
                }
            }
        }

        public static async Task<T> ExecuteAsyncWithRetry<T>(Func<Task<T>> action,
            Func<T, bool> shouldStopRetry,
            int maxRetryCount,
            Func<IEnumerable<Task>> timer,
            string taskDescription = "")
        {
            var count = 0;
            foreach (var t in timer())
            {
                await t;
                T result = default;
                count++;

                result = await action();

                if (shouldStopRetry(result))
                {
                    return result;
                }

                if (count == maxRetryCount)
                {
                    return result;
                }
            }
            throw new Exception("Timer should not be exhausted");
        }

        public static async Task<T> ExecuteWithRetry<T>(Func<T> action,
            Func<T, bool> shouldStopRetry,
            int maxRetryCount,
            Func<IEnumerable<Task>> timer,
            string taskDescription = "")
        {
            Func<Task<T>> asyncAction = () => Task.FromResult(action());
            return await ExecuteAsyncWithRetry(asyncAction, shouldStopRetry, maxRetryCount, timer, taskDescription);
        }

        public static async Task<T> ExecuteWithRetryOnFailure<T>(Func<Task<T>> action,
            int maxRetryCount = 3,
            Func<IEnumerable<Task>> timer = null)
        {
            timer = timer == null ? () => Timer(Intervals) : timer;
            return await ExecuteAsyncWithRetry(action, result => result != null && !result.Equals(default), maxRetryCount, timer);
        }

        public static IEnumerable<Task> Timer(IEnumerable<TimeSpan> interval)
        {
            return interval.Select(Task.Delay);
        }
    }
}
