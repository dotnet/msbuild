// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Predefined XML attributes of a ComReference Item.
    /// </summary>
    internal static class ComReferenceItemMetadataNames
    {
        internal const string guid = "Guid";
        internal const string versionMinor = "VersionMinor";
        internal const string versionMajor = "VersionMajor";
        internal const string lcid = "Lcid";
        internal const string privatized = "Private";
        internal const string wrapperTool = "WrapperTool";
        internal const string tlbReferenceName = "TlbReferenceName";
    }
}
