// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Struct representing a reference to a project import path with property fall-back
    /// </summary>
    internal struct ProjectImportPathMatch
    {
        /// <summary>
        /// ProjectImportPathMatch instance representing no fall-back
        /// </summary>
        public static readonly ProjectImportPathMatch None = new ProjectImportPathMatch(string.Empty, new List<string>());

        internal ProjectImportPathMatch(string propertyName, IEnumerable<string> searchPaths)
        {
            PropertyName = propertyName;
            SearchPaths = searchPaths;
            MsBuildPropertyFormat = $"$({PropertyName})";
        }

        /// <summary>
        /// String representation of the property reference - eg. "MSBuildExtensionsPath32"
        /// </summary>
        public string PropertyName { get; }

        /// <summary>
        /// Returns the corresponding property name - eg. "$(MSBuildExtensionsPath32)"
        /// </summary>
        public string MsBuildPropertyFormat { get; }

        /// <summary>
        /// Enumeration of the search paths for the property.
        /// </summary>
        public IEnumerable<string> SearchPaths { get; }
    }
}