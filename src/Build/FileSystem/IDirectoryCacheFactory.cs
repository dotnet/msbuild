// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;

namespace Microsoft.Build.FileSystem
{
    /// <summary>
    /// A provider of <see cref="IDirectoryCache"/> instances. To be implemented by MSBuild hosts that wish to intercept
    /// file existence checks and file enumerations performed during project evaluation.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="MSBuildFileSystemBase"/>, file enumeration returns file/directory names, not full paths.
    /// </remarks>
    public interface IDirectoryCacheFactory
    {
        /// <summary>
        /// Returns an <see cref="IDirectoryCache"/> to be used when evaluating the given <see cref="Project"/>.
        /// </summary>
        /// <param name="project">The project being evaluated.</param>
        IDirectoryCache GetDirectoryCacheForProject(Project project);
    }
}
