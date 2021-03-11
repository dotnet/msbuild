// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework
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

        public static async Task<T> ExecuteWithRetry<T>(Func<T> action,
            Func<T, bool> shouldStopRetry,
            int maxRetryCount,
            Func<IEnumerable<Task>> timer,
            string taskDescription = "",
            ITestOutputHelper log = null)
        {
            var count = 0;
            foreach (var t in timer())
            {
                await t;
                var result = action();
                if (shouldStopRetry(result))
                {
                    return result;
                }

                log?.WriteLine($"Operation failed. Retry count: {count}");
                count++;
                if (count == maxRetryCount)
                {
                    throw new RetryFailedException(
                        $"Retry failed for {taskDescription} after {count} times with result: {result}");
                }
            }
            throw new Exception("Timer should not be exhausted");
        }

        public static IEnumerable<Task> Timer(IEnumerable<TimeSpan> interval)
        {
            return interval.Select(Task.Delay);
        }
    }
}
