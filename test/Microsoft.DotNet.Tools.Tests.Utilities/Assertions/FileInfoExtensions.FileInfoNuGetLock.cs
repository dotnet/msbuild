// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public static partial class FileInfoExtensions
    {
        private class FileInfoNuGetLock : IDisposable
        {
            private FileStream _fileStream;

            private CancellationTokenSource _cancellationTokenSource;

            private Task _task;

            public FileInfoNuGetLock(FileInfo fileInfo)
            {
                _cancellationTokenSource = new CancellationTokenSource();

                _task = ConcurrencyUtilities.ExecuteWithFileLockedAsync<int>(
                    fileInfo.FullName,
                    lockedToken =>
                    {
                        Task.Delay(60000, _cancellationTokenSource.Token).Wait();

                        return Task.FromResult(0);   
                    },
                    _cancellationTokenSource.Token);
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
