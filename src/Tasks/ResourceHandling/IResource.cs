// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Resources;

#nullable disable

namespace Microsoft.Build.Tasks.ResourceHandling
{
    internal interface IResource
    {
        /// <summary>
        /// Name of the resource, as specified in the source.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The resource's type's assembly-qualified name. May be null when the type is not knowable from the source.
        /// </summary>
        string TypeAssemblyQualifiedName { get; }

        /// <summary>
        /// The resource's type's full name. May be null when the type is not knowable from the source.
        /// </summary>
        string TypeFullName { get; }

        /// <summary>
        /// Adds the resource represented by this object to the specified writer.
        /// </summary>
        void AddTo(IResourceWriter writer);
    }
}
