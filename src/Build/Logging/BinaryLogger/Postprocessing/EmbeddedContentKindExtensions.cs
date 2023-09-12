// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Logging
{
    internal static class EmbeddedContentKindExtensions
    {
        internal static EmbeddedContentKind ToEmbeddedContentKind(this BinaryLogRecordKind kind)
        {
            return kind == BinaryLogRecordKind.ProjectImportArchive
                ? EmbeddedContentKind.ProjectImportArchive
                : EmbeddedContentKind.Unknown;
        }

        internal static BinaryLogRecordKind ToBinaryLogRecordKind(this EmbeddedContentKind kind)
        {
            return kind == EmbeddedContentKind.ProjectImportArchive
                ? BinaryLogRecordKind.ProjectImportArchive
                : (BinaryLogRecordKind)EmbeddedContentKind.Unknown;
        }
    }
}
