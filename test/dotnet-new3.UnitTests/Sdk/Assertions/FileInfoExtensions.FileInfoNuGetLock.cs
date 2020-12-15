// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Microsoft.NET.TestFramework.Assertions
{
    public static partial class FileInfoExtensions
    {
        private class FileInfoNuGetLock : IDisposable
        {
            private CancellationTokenSource _cancellationTokenSource;

            private Task _task;

            public FileInfoNuGetLock(FileInfo fileInfo)
            {
                var taskCompletionSource = new TaskCompletionSource<string>();

                _cancellationTokenSource = new CancellationTokenSource();

                _task = Task.Run(async () => await ConcurrencyUtilities.ExecuteWithFileLockedAsync<int>(
                    fileInfo.FullName,
                    cancellationToken =>
                    {
                        taskCompletionSource.SetResult("Lock is taken so test can continue");

                        Task.Delay(60000, cancellationToken).Wait();

                        return Task.FromResult(0);   
                    },
                    _cancellationTokenSource.Token));

                    taskCompletionSource.Task.Wait();
            }

            public void Dispose()
            {
                _cancellationTokenSource.Cancel();

                try
                {
                    _task.Wait();
                }
                catch (AggregateException)
                {
                }
                finally
                {
                    _cancellationTokenSource.Dispose();
                }
            }
        }
    }
}
