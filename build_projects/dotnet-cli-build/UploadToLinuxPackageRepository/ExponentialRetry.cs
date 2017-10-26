// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli.Build.UploadToLinuxPackageRepository
{
    public static class ExponentialRetry
    {
        public static IEnumerable<TimeSpan> Intervals
        {
            get
            {
                var seconds = 5;
                while (true)
                {
                    yield return TimeSpan.FromSeconds(seconds);
                    seconds *= 2;
                }
            }
        }

        public static async Task ExecuteWithRetry(Func<Task<string>> action,
            Func<string, bool> isSuccess,
            int maxRetryCount,
            Func<IEnumerable<Task>> timer,
            string taskDescription = "")
        {
            var count = 0;
            foreach (var t in timer())
            {
                await t;
                var result = await action();
                if (isSuccess(result))
                    return;
                count++;
                if (count == maxRetryCount)
                    throw new RetryFailedException(
                        $"Retry failed for {taskDescription} after {count} times with result: {result}");
            }
            throw new Exception("Timer should not be exhausted");
        }

        public static IEnumerable<Task> Timer(IEnumerable<TimeSpan> interval)
        {
            return interval.Select(Task.Delay);
        }
    }
}
