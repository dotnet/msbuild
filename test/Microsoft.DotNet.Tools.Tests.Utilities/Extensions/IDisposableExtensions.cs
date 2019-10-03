// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public static class IDisposableExtensions
    {
        public static IDisposable DisposeAfter(this IDisposable subject, TimeSpan timeout)
        {
            return new IDisposableWithTimeout(subject, timeout);
        }
        
        private class IDisposableWithTimeout :IDisposable
        {
            private CancellationTokenSource _cancellationTokenSource;

            private Task _timeoutTask;

            private bool _isDisposed;

            private IDisposable _subject;

            public IDisposableWithTimeout(IDisposable subject, TimeSpan timeout)
            {
                _subject = subject;

                _cancellationTokenSource = new CancellationTokenSource();

                _timeoutTask = Task.Run(() => 
                {
                    Task.Delay(timeout, _cancellationTokenSource.Token).Wait();

                    DisposeInternal();
                }, 
                _cancellationTokenSource.Token);
            }

            public void Dispose()
            {
                DisposeInternal();

                CancelTimeout();
            }

            private void DisposeInternal()
            {
                lock(this)
                {
                    if (!_isDisposed)
                    {
                        _subject.Dispose();

                        _isDisposed = true;
                    }
                }
            }

            private void CancelTimeout()
            {
                _cancellationTokenSource.Cancel();

                try
                {
                    _timeoutTask.Wait();
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
