// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal sealed class CachingMetadataReference : PortableExecutableReference
    {
        private static readonly MetadataCache _metadataCache = new();

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
