// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.NETCore.Build.Tasks
{
    public static class MetadataKeys
    {
        // General Metadata
        public const string Name = "Name";
        public const string Type = "Type";
        public const string Version = "Version";
        public const string FileGroup = "FileGroup";
        public const string Path = "Path";
        public const string ResolvedPath = "ResolvedPath";

        // Target Metadata
        public const string RuntimeIdentifier = "RuntimeIdentifier";
        public const string TargetFrameworkMoniker = "TargetFrameworkMoniker";
        public const string FrameworkName = "FrameworkName";
        public const string FrameworkVersion = "FrameworkVersion";

        // Foreign Keys
        public const string ParentTarget = "ParentTarget";
        public const string ParentTargetLibrary = "ParentTargetLibrary";
        public const string ParentPackage = "ParentPackage";

        // Tags
        public const string Analyzer = "Analyzer";
        public const string AnalyzerLanguage = "AnalyzerLanguage";
    }
}
