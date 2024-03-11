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
        T ImmutableInstance { get; set; }
    }
}
