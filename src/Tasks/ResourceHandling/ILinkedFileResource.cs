// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Tasks.ResourceHandling
{
    /// <summary>
    /// An <see cref="IResource"/> that may be backed by a linked file and can expose
    /// the path to that file when applicable (for example, resources originating
    /// from a <c>ResXFileRef</c> entry). Non-linked resources will have
    /// <see cref="LinkedFilePath"/> set to <see langword="null"/>.
    /// </summary>
    internal interface ILinkedFileResource : IResource
    {
        /// <summary>
        /// The path of the file this resource was read from. This may be an absolute
        /// or relative path depending on how the resource was resolved (for example,
        /// whether paths were made relative to a base path). A value of
        /// <see langword="null"/> indicates that this resource is not linked
        /// to an external file.
        /// </summary>
        string? LinkedFilePath { get; }
    }
}
