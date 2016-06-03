// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Linq;

namespace NuGet
{
    internal static class ManifestSchemaUtility
    {
        /// <summary>
        /// Baseline schema 
        /// </summary>
        internal const string SchemaVersionV1 = "http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd";

        /// <summary>
        /// Added copyrights, references and release notes
        /// </summary>
        internal const string SchemaVersionV2 = "http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd";

        /// <summary>
        /// Used if the version is a semantic version.
        /// </summary>
        internal const string SchemaVersionV3 = "http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd";

        /// <summary>
        /// Added 'targetFramework' attribute for 'dependency' elements.
        /// Allow framework folders under 'content' and 'tools' folders. 
        /// </summary>
        internal const string SchemaVersionV4 = "http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd";

        /// <summary>
        /// Added 'targetFramework' attribute for 'references' elements.
        /// Added 'minClientVersion' attribute
        /// </summary>
        internal const string SchemaVersionV5 = "http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd";

        /// <summary>
        /// Allows XDT transformation
        /// </summary>
        internal const string SchemaVersionV6 = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";

        /// <summary>
        /// Added serviceble element under metadata.
        /// </summary>
        internal const string SchemaVersionV8 = "http://schemas.microsoft.com/packaging/2016/06/nuspec.xsd";

        private static readonly string[] VersionToSchemaMappings = new[] {
            SchemaVersionV1,
            SchemaVersionV2,
            SchemaVersionV3,
            SchemaVersionV4,
            SchemaVersionV5,
            SchemaVersionV6,
            SchemaVersionV8
        };

        public static int GetVersionFromNamespace(string @namespace)
        {
            int index = Math.Max(0, Array.IndexOf(VersionToSchemaMappings, @namespace));

            // we count version from 1 instead of 0
            return index + 1;
        }

        public static string GetSchemaNamespace(int version)
        {
            // Versions are internally 0-indexed but stored with a 1 index so decrement it by 1
            if (version <= 0 || version > VersionToSchemaMappings.Length)
            {
                // TODO: Resources
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, "NuGetResources.UnknownSchemaVersion", version));
            }
            return VersionToSchemaMappings[version - 1];
        }

        public static bool IsKnownSchema(string schemaNamespace)
        {
            return VersionToSchemaMappings.Contains(schemaNamespace, StringComparer.OrdinalIgnoreCase);
        }
    }
}
