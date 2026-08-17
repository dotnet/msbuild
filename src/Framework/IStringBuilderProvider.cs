// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable
using System.Text;

namespace Microsoft.Build.Framework;

/// <summary>
/// Provider of <see cref="StringBuilder"/> instances.
/// Main design goal is for reusable String Builders and string builder pools.
/// </summary>
/// <remarks>
/// It is up to particular implementations to decide how to handle unbalanced releases.
/// </remarks>
internal interface IStringBuilderProvider
{
    /// <summary>
    /// Get a <see cref="StringBuilder"/> of at least the specified capacity.
    /// </summary>
    /// <param name="capacity">The suggested starting size of this instance.</param>
    /// <returns>A <see cref="StringBuilder"/> that may or may not be reused.</returns>
    /// <remarks>
    /// It can be called any number of times; if a <see cref="StringBuilder"/> is in the cache then
    /// it will be returned and the cache emptied. Subsequent calls will return a new <see cref="StringBuilder"/>.
    /// </remarks>
    StringBuilder Acquire(int capacity);

    /// <summary>
    /// Get a string and return its builder to the cache.
    /// </summary>
    /// <param name="builder">Builder to cache (if it's not too big).</param>
    /// <returns>The <see langword="string"/> equivalent to <paramref name="builder"/>'s contents.</returns>
    /// <remarks>
    /// The StringBuilder should not be used after it has been released.
    /// </remarks>
    string GetStringAndRelease(StringBuilder builder);
}
