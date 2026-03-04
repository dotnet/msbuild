// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Tasks.ResourceHandling
{
    /// <summary>
    /// An <see cref="IResource"/> that originated from a ResXFileRef entry
    /// and carries the path to the linked file.
    /// </summary>
    internal interface ILinkedFileResource : IResource
    {
        /// <summary>
        /// The absolute path of the file this resource was read from.
        /// </summary>
        string? LinkedFilePath { get; }
    }
}
