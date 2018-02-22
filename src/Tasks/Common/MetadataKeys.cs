// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.NET.Build.Tasks
{
    internal static class MetadataKeys
    {
        // General Metadata
        public const string Name = "Name";
        public const string Type = "Type";
        public const string Version = "Version";
        public const string FileGroup = "FileGroup";
        public const string Path = "Path";
        public const string ResolvedPath = "ResolvedPath";
        public const string PackageName = "PackageName";
        public const string PackageVersion = "PackageVersion";
        public const string IsImplicitlyDefined = "IsImplicitlyDefined";
        public const string IsTopLevelDependency = "IsTopLevelDependency";

        // Target Metadata
        public const string RuntimeIdentifier = "RuntimeIdentifier";
        public const string TargetFrameworkMoniker = "TargetFrameworkMoniker";
        public const string FrameworkName = "FrameworkName";
        public const string FrameworkVersion = "FrameworkVersion";

        // SDK Metadata
        public const string SDKPackageItemSpec = "SDKPackageItemSpec";
        public const string OriginalItemSpec = "OriginalItemSpec";
        public const string SDKRootFolder = "SDKRootFolder";

        // Foreign Keys
        public const string ParentTarget = "ParentTarget";
        public const string ParentTargetLibrary = "ParentTargetLibrary";
        public const string ParentPackage = "ParentPackage";

        // Tags
        public const string Analyzer = "Analyzer";
        public const string AnalyzerLanguage = "AnalyzerLanguage";
        public const string TransitiveProjectReference = "TransitiveProjectReference";

        // Diagnostics
        public const string DiagnosticCode = "DiagnosticCode";
        public const string Message = "Message";
        public const string FilePath = "FilePath";
        public const string Severity = "Severity";
        public const string StartLine = "StartLine";
        public const string StartColumn = "StartColumn";
        public const string EndLine = "EndLine";
        public const string EndColumn = "EndColumn";

        // Publish Target Manifest
        public const string RuntimeStoreManifestNames = "RuntimeStoreManifestNames";

        // Conflict Resolution
        public const string OverriddenPackages = "OverriddenPackages";
    }
}
