// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.CodeAnalysis;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal sealed class CachingMetadataReference : PortableExecutableReference
    {
        private static readonly MetadataCache _metadataCache = new MetadataCache();

        public CachingMetadataReference(string fullPath, MetadataReferenceProperties properties)
            : base(properties, fullPath)
        {
        }

        protected override DocumentationProvider CreateDocumentationProvider()
        {
            return DocumentationProvider.Default;
        }

        protected override Metadata GetMetadataImpl()
        {
            return _metadataCache.GetMetadata(FilePath);
        }

        protected override PortableExecutableReference WithPropertiesImpl(MetadataReferenceProperties properties)
        {
            return new CachingMetadataReference(FilePath, properties);
        }
    }
}
