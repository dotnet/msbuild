// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//Microsoft.NET.Build.Extensions.Tasks (net7.0) has nullables disabled
#pragma warning disable IDE0240 // Remove redundant nullable directive
#nullable disable
#pragma warning restore IDE0240 // Remove redundant nullable directive

namespace Microsoft.NET.Build.Tasks.ConflictResolution
{
    static class MetadataNames
    {
        public const string Aliases = "Aliases";
        public const string DestinationSubPath = "DestinationSubPath";
        public const string Extension = "Extension";
        public const string FileName = "FileName";
        public const string HintPath = "HintPath";
        public const string NuGetPackageId = "NuGetPackageId";
        public const string NuGetPackageVersion = "NuGetPackageVersion";
        public const string Path = "Path";
        public const string Private = "Private";
        public const string TargetPath = "TargetPath";
    }
}
