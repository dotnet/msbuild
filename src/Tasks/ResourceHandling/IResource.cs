// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Resources;

namespace Microsoft.Build.Tasks.ResourceHandling
{
    internal interface IResource
    {
        /// <summary>
        /// Name of the resource, as specified in the source.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Adds the resource represented by this object to the specified writer.
        /// </summary>
        void AddTo(IResourceWriter writer);
    }
}
