// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Tasks.ResourceHandling
{
    /// <summary>
    /// An <see cref="IResource"/> that is backed by a linked file (originating
    /// from a <c>ResXFileRef</c> entry) and exposes the path to that file.
    /// </summary>
    internal interface ILinkedFileResource : IResource
    {
        /// <summary>
        /// The path of the file this resource was read from. This may be an absolute
        /// or relative path depending on how the resource was resolved (for example,
        /// whether paths were made relative to a base path).
        /// </summary>
        string LinkedFilePath { get; }
    }
}
