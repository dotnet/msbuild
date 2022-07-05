// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;

namespace Microsoft.Build.Internal;

// This type inspired by https://github.com/dotnet/roslyn/blob/ec6da663c592238cca8e145044e7410c4ca9213a/src/Compilers/Core/Portable/InternalUtilities/SemaphoreSlimExtensions.cs

internal static class ReaderWriterLockSlimExtensions
{
    public static UpgradeableReadLockDisposer EnterDisposableUpgradeableReadLock(this ReaderWriterLockSlim rwLock)
    {
        rwLock.EnterUpgradeableReadLock();
        return new UpgradeableReadLockDisposer(rwLock);
    }

    public static DisposableWriteLock EnterDisposableWriteLock(this ReaderWriterLockSlim rwLock)
    {
        rwLock.EnterWriteLock();
        return new DisposableWriteLock(rwLock);
    }

    // Officially, Dispose() being called more than once is allowable, but in this case if that were to happen
    // that means something is very, very wrong. Since it's an internal type, better to be strict.

    internal struct UpgradeableReadLockDisposer : IDisposable
    {
        private ReaderWriterLockSlim? _rwLock;

        public UpgradeableReadLockDisposer(ReaderWriterLockSlim rwLock) => _rwLock = rwLock;

        public void Dispose()
        {
            var rwLockToDispose = Interlocked.Exchange(ref _rwLock, null);

            if (rwLockToDispose is null)
            {
                throw new ObjectDisposedException($"Somehow a {nameof(UpgradeableReadLockDisposer)} is being disposed twice.");
            }

            rwLockToDispose.ExitUpgradeableReadLock();
        }
    }

    internal struct DisposableWriteLock : IDisposable
    {
        private ReaderWriterLockSlim? _rwLock;

        public DisposableWriteLock(ReaderWriterLockSlim rwLock) => _rwLock = rwLock;

        public void Dispose()
        {
            var rwLockToDispose = Interlocked.Exchange(ref _rwLock, null);

            if (rwLockToDispose is null)
            {
                throw new ObjectDisposedException($"Somehow a {nameof(DisposableWriteLock)} is being disposed twice.");
            }

            rwLockToDispose.ExitWriteLock();
        }
    }
}
