// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class FileAccessRetrier
    {
        public static async Task<T> RetryOnFileAccessFailure<T>(
            Func<T> func,
            string errorMessage,
            int maxRetries = 3000,
            TimeSpan sleepDuration = default(TimeSpan))
        {
            var attemptsLeft = maxRetries;

            if (sleepDuration == default(TimeSpan))
            {
                sleepDuration = TimeSpan.FromMilliseconds(10);
            }

            while (true)
            {
                if (attemptsLeft < 1)
                {
                    throw new InvalidOperationException(errorMessage);
                }

                attemptsLeft--;

                try
                {
                    return func();
                }
                catch (UnauthorizedAccessException)
                {
                    // This can occur when the file is being deleted
                    // Or when an admin user has locked the file
                    await Task.Delay(sleepDuration);

                    continue;
                }
                catch (IOException)
                {
                    await Task.Delay(sleepDuration);

                    continue;
                }
            }
        }

        /// <summary>
        /// Run Directory.Move and File.Move in Windows has a chance to get IOException with
        /// HResult 0x80070005 due to Indexer. But this error is transient.
        /// </summary>
        internal static void RetryOnMoveAccessFailure(Action action)
        {
            const int ERROR_HRESULT_ACCESS_DENIED = unchecked((int)0x80070005);
            int nextWaitTime = 10;
            int remainRetry = 10;

            while (true)
            {
                try
                {
                    action();
                    break;
                }
                catch (IOException e) when (e.HResult == ERROR_HRESULT_ACCESS_DENIED)
                {
                    Thread.Sleep(nextWaitTime);
                    nextWaitTime *= 2;
                    remainRetry--;
                    if (remainRetry == 0)
                    {
                        throw;
                    }
                }
            }
        }

        internal static void RetryOnIOException(Action action)
        {
            int nextWaitTime = 10;
            int remainRetry = 10;

            while (true)
            {
                try
                {
                    action();
                    break;
                }
                catch (IOException)
                {
                    Thread.Sleep(nextWaitTime);
                    nextWaitTime *= 2;
                    remainRetry--;
                    if (remainRetry == 0)
                    {
                        throw;
                    }
                }
            }
        }
    }
}
