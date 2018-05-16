// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Class representing a reference to a project import path with property fall-back
    /// </summary>
    internal class ProjectImportPathMatch : INodePacketTranslatable
    {
        /// <summary>
        /// Character used to separate search paths specified for MSBuildExtensionsPath* in
        /// the config file
        /// </summary>
        private static char s_separatorForExtensionsPathSearchPaths = ';';

        /// <summary>
        /// ProjectImportPathMatch instance representing no fall-back
        /// </summary>
        public static readonly ProjectImportPathMatch None = new ProjectImportPathMatch(string.Empty, string.Empty);

        internal ProjectImportPathMatch(string propertyName, string propertyValue)
        {
            ErrorUtilities.VerifyThrowArgumentNull(propertyName, nameof(propertyName));
            ErrorUtilities.VerifyThrowArgumentNull(propertyValue, nameof(propertyValue));

            PropertyName = propertyName;
            PropertyValue = propertyValue;
            MsBuildPropertyFormat = $"$({PropertyName})";

            // cache the result for @propertyValue="" case also
            if (!ProjectImportPathMatch.HasPropertyReference(propertyValue) || string.IsNullOrEmpty(propertyValue))
            {
                SearchPathsWithNoExpansionRequired = ProjectImportPathMatch.SplitSearchPaths(propertyValue);
            }
        }

        public ProjectImportPathMatch(INodePacketTranslator translator)
        {
            ((INodePacketTranslatable)this).Translate(translator);
        }

        /// <summary>
        /// String representation of the property reference - eg. "MSBuildExtensionsPath32"
        /// </summary>
        public string PropertyName;

        /// <summary>
        /// String representation of the property value
        /// </summary>
        public string PropertyValue;

        /// <summary>
        /// Returns the corresponding property name - eg. "$(MSBuildExtensionsPath32)"
        /// </summary>
        public string MsBuildPropertyFormat;

        /// <summary>
        /// Enumeration of the search paths for the property.
        /// </summary>
        private List<string> SearchPathsWithNoExpansionRequired;

        public void Translate(INodePacketTranslator translator)
        {
            translator.Translate(ref PropertyName);
            translator.Translate(ref PropertyValue);
            translator.Translate(ref MsBuildPropertyFormat);
            translator.Translate(ref SearchPathsWithNoExpansionRequired);
        }

        /// <summary>
        /// Gets the list of search paths with any property references expanded using @expandPropertyReferences
        /// <param name="expandPropertyReferences">Func that expands properties in a string</param>
        /// <returns>List of expanded search paths</returns>
        /// </summary>
        public IList<string> GetExpandedSearchPaths(Func<string, string> expandPropertyReferences)
        {
            ErrorUtilities.VerifyThrowArgumentNull(expandPropertyReferences, nameof(expandPropertyReferences));

            /// If SearchPathsWithNoExpansionRequired is not-null, then it means that the PropertyValue
            /// did not have any property reference and so we just return the list we prepared during
            /// construction.
            return SearchPathsWithNoExpansionRequired ?? ProjectImportPathMatch.SplitSearchPaths(expandPropertyReferences(PropertyValue));
        }

        /// <summary>
        /// Splits @fullString on @s_separatorForExtensionsPathSearchPaths
        /// </summary>
        /// FIXME: handle ; in path on Unix
        private static List<string> SplitSearchPaths(string fullString) => fullString
                                                                        .Split(new[] {s_separatorForExtensionsPathSearchPaths}, StringSplitOptions.RemoveEmptyEntries)
                                                                        .Distinct().ToList();

        /// <summary>
        /// Returns true if @value might have a property reference
        /// </summary>
        private static bool HasPropertyReference(string value) => value.Contains("$(");

        /// <summary>
        /// Factory for serialization.
        /// </summary>
        internal static ProjectImportPathMatch FactoryForDeserialization(INodePacketTranslator translator)
        {
            return new ProjectImportPathMatch(translator);
        }
    }
}
