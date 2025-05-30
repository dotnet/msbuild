// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Represents an object that is immutable and has an Instance, e.g. a <see cref="ProjectPropertyInstance"/>.
    /// </summary>
    /// <typeparam name="T">The Instance type.</typeparam>
    internal interface IImmutableInstanceProvider<T>
    {
        /// <summary>
        /// Gets the Immutable Instance.
        /// </summary>
        T ImmutableInstance { get; }

        /// <summary>
        /// If the ImmutableInstance has not already been set, then this
        /// method sets the ImmutableInstance to the requested value.
        /// An already set ImmutableInstance is never replaced.
        /// </summary>
        /// <param name="instance">An instance that will be set as the immutable instance, provided that
        /// the immutable instance has not already been set.</param>
        /// <returns>The immutable instance, which may or may not be the supplied <paramref name="instance"/>.</returns>
        T GetOrSetImmutableInstance(T instance);
    }
}
