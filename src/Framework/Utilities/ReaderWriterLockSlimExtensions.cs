// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;

namespace Microsoft.Build.Internal;

// This type inspired by https://github.com/dotnet/roslyn/blob/ec6da663c592238cca8e145044e7410c4ca9213a/src/Compilers/Core/Portable/InternalUtilities/SemaphoreSlimExtensions.cs

internal static class ReaderWriterLockSlimExtensions
{
    public static DisposableReadLock EnterDisposableReadLock(this ReaderWriterLockSlim rwLock)
    {
        rwLock.EnterReadLock();
        return new DisposableReadLock(rwLock);
    }

    public static DisposableWriteLock EnterDisposableWriteLock(this ReaderWriterLockSlim rwLock)
    {
        rwLock.EnterWriteLock();
        return new DisposableWriteLock(rwLock);
    }

    internal readonly struct DisposableReadLock : IDisposable
    {
        private readonly ReaderWriterLockSlim _rwLock;

        public DisposableReadLock(ReaderWriterLockSlim rwLock) => _rwLock = rwLock;

        public void Dispose() => _rwLock.ExitReadLock();
    }

    internal readonly struct DisposableWriteLock : IDisposable
    {
        private readonly ReaderWriterLockSlim _rwLock;

        public DisposableWriteLock(ReaderWriterLockSlim rwLock) => _rwLock = rwLock;

        public void Dispose() => _rwLock.ExitWriteLock();
    }
}
