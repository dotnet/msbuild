// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Tools.Resgen
{
    internal struct ResourceSource
    {
        public ResourceSource(ResourceFile resource, string metadataName)
        {
            Resource = resource;
            MetadataName = metadataName;
        }

        public ResourceFile Resource { get; }

        public string MetadataName { get; }
    }
}