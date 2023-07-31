// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
