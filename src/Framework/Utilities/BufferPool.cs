// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;

namespace Microsoft.Build.Utilities;

/// <summary>
///  Provides access to the shared array pool and creates new array pools.
/// </summary>
/// <typeparam name="T">The type of elements in the buffers.</typeparam>
/// <remarks>
///  On .NET Framework, <see cref="Shared"/> caches one buffer per power-of-two size from 16 through 256
///  elements per thread. These buffers are retained for the lifetime of the thread. On other runtimes,
///  <see cref="Shared"/> is <see cref="ArrayPool{T}.Shared"/>. The <see cref="Create()"/> and
///  <see cref="Create(int, int)"/> methods delegate to the corresponding <see cref="ArrayPool{T}.Create()"/>
///  overloads.
/// </remarks>
internal static partial class BufferPool<T>
{
    /// <summary>
    ///  Gets a shared <see cref="ArrayPool{T}"/> instance, which adds per-thread size buckets on .NET Framework.
    /// </summary>
    /// <returns>
    ///  A <see cref="ArrayPool{T}"/> instance.
    /// </returns>
#if NETFRAMEWORK
    public static ArrayPool<T> Shared => field ??= new SharedArrayPool();
#else
    public static ArrayPool<T> Shared => ArrayPool<T>.Shared;
#endif

    /// <inheritdoc cref="ArrayPool{T}.Create()"/>
    public static ArrayPool<T> Create()
        => ArrayPool<T>.Create();

    /// <inheritdoc cref="ArrayPool{T}.Create(int, int)"/>
    public static ArrayPool<T> Create(int maxArrayLength, int maxArraysPerBucket)
        => ArrayPool<T>.Create(maxArrayLength, maxArraysPerBucket);
}
