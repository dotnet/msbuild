// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace NuGet
{
    public static class Constants
    {
        /// <summary>
        /// Represents the ".nupkg" extension.
        /// </summary>
        public static readonly string PackageExtension = ".nupkg";

        /// <summary>
        /// Represents the ".nuspec" extension.
        /// </summary>
        public static readonly string ManifestExtension = ".nuspec";

        /// <summary>
        /// Represents the ".nupkg.sha512" extension.
        /// </summary>
        public static readonly string HashFileExtension = ".nupkg.sha512";

        /// <summary>
        /// Represents the content directory in the package.
        /// </summary>
        public static readonly string ContentDirectory = "content";

        /// <summary>
        /// Represents the lib directory in the package.
        /// </summary>
        public static readonly string LibDirectory = "lib";

        /// <summary>
        /// Represents the tools directory in the package.
        /// </summary>
        public static readonly string ToolsDirectory = "tools";

        /// <summary>
        /// Represents the build directory in the package.
        /// </summary>
        public static readonly string BuildDirectory = "build";
        
        public static readonly string BinDirectory = "bin";
        public static readonly string PackageReferenceFile = "packages.config";

        public static readonly string BeginIgnoreMarker = "NUGET: BEGIN LICENSE TEXT";
        public static readonly string EndIgnoreMarker = "NUGET: END LICENSE TEXT";

        internal const string PackageRelationshipNamespace = "http://schemas.microsoft.com/packaging/2010/07/";

        // Starting from nuget 2.0, we use a file with the special name '_._' to represent an empty folder.
        public const string PackageEmptyFileName = "_._";

        // This is temporary until we fix the gallery to have proper first class support for this.
        // The magic unpublished date is 1900-01-01T00:00:00
        public static readonly DateTimeOffset Unpublished = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.FromHours(-8));

        public static readonly IReadOnlyList<string> AssemblyReferencesExtensions
            = new string[] { ".dll", ".exe", ".winmd" };
    }
}