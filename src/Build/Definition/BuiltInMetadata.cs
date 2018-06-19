// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Container for built-in metadata.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Microsoft.Build.Shared;
using Microsoft.Build.Internal;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// This class encapsulates the behavior and collection of built-in metadata.  These metadatum
    /// are inferred from the content of the include and sometimes the context of the project or
    /// current directory.
    /// </summary>
    internal static class BuiltInMetadata
    {
        /// <summary>
        /// Retrieves the count of built-in metadata.
        /// </summary>
        static internal int MetadataCount
        {
            [DebuggerStepThrough]
            get
            { return FileUtilities.ItemSpecModifiers.All.Length; }
        }

        /// <summary>
        /// Retrieves the list of metadata names.
        /// </summary>
        static internal ICollection<string> MetadataNames
        {
            [DebuggerStepThrough]
            get
            { return FileUtilities.ItemSpecModifiers.All; }
        }

        /// <summary>
        /// Retrieves a built-in metadata value and caches it.
        /// Never returns null.
        /// </summary>
        /// <param name="currentDirectory">
        /// The current directory for evaluation.  Null if this is being called from a task, otherwise
        /// it should be the project's directory.
        /// </param>
        /// <param name="evaluatedIncludeBeforeWildcardExpansionEscaped">The evaluated include prior to wildcard expansion.</param>
        /// <param name="evaluatedIncludeEscaped">The evaluated include for the item.</param>
        /// <param name="definingProjectEscaped">The path to the project that defined this item</param>
        /// <param name="name">The name of the metadata.</param>
        /// <param name="fullPath">The generated full path, for caching</param>
        /// <returns>The unescaped metadata value.</returns>
        internal static string GetMetadataValue(string currentDirectory, string evaluatedIncludeBeforeWildcardExpansionEscaped, string evaluatedIncludeEscaped, string definingProjectEscaped, string name, ref string fullPath)
        {
            return EscapingUtilities.UnescapeAll(GetMetadataValueEscaped(currentDirectory, evaluatedIncludeBeforeWildcardExpansionEscaped, evaluatedIncludeEscaped, definingProjectEscaped, name, ref fullPath));
        }

        /// <summary>
        /// Retrieves a built-in metadata value and caches it.
        /// If value is not available, returns empty string.
        /// </summary>
        /// <param name="currentDirectory">
        /// The current directory for evaluation.  Null if this is being called from a task, otherwise
        /// it should be the project's directory.
        /// </param>
        /// <param name="evaluatedIncludeBeforeWildcardExpansionEscaped">The evaluated include prior to wildcard expansion.</param>
        /// <param name="evaluatedIncludeEscaped">The evaluated include for the item.</param>
        /// <param name="definingProjectEscaped">The path to the project that defined this item</param>
        /// <param name="name">The name of the metadata.</param>
        /// <param name="fullPath">The generated full path, for caching</param>
        /// <returns>The escaped as necessary metadata value.</returns>
        internal static string GetMetadataValueEscaped(string currentDirectory, string evaluatedIncludeBeforeWildcardExpansionEscaped, string evaluatedIncludeEscaped, string definingProjectEscaped, string name, ref string fullPath)
        {
            // This is an assert, not a VerifyThrow, because the caller should already have done this check, and it's slow/hot.
            Debug.Assert(FileUtilities.ItemSpecModifiers.IsItemSpecModifier(name));

            string value = null;

            if (String.Equals(name, FileUtilities.ItemSpecModifiers.RecursiveDir, StringComparison.OrdinalIgnoreCase))
            {
                value = GetRecursiveDirValue(evaluatedIncludeBeforeWildcardExpansionEscaped, evaluatedIncludeEscaped);
            }
            else
            {
                value = FileUtilities.ItemSpecModifiers.GetItemSpecModifier(currentDirectory, evaluatedIncludeEscaped, definingProjectEscaped, name, ref fullPath);
            }

            return value;
        }

        /// <summary>
        /// Extract the value for "RecursiveDir", if any, from the Include.
        /// If there is none, returns an empty string.
        /// </summary>
        /// <remarks>
        /// Inputs to and outputs of this function are all escaped.
        /// </remarks>
        private static string GetRecursiveDirValue(string evaluatedIncludeBeforeWildcardExpansionEscaped, string evaluatedIncludeEscaped)
        {
            // If there were no wildcards, the two strings will be the same, and there is no recursivedir part.
            if (String.Equals(evaluatedIncludeBeforeWildcardExpansionEscaped, evaluatedIncludeEscaped, StringComparison.OrdinalIgnoreCase))
            {
                return String.Empty;
            }

            // we're going to the file system, so unescape the include value here:
            string evaluatedIncludeBeforeWildcardExpansion = EscapingUtilities.UnescapeAll(evaluatedIncludeBeforeWildcardExpansionEscaped);
            string evaluatedInclude = EscapingUtilities.UnescapeAll(evaluatedIncludeEscaped);

            FileMatcher.Result match = FileMatcher.Default.FileMatch(evaluatedIncludeBeforeWildcardExpansion, evaluatedInclude);

            if (match.isLegalFileSpec && match.isMatch)
            {
                return EscapingUtilities.Escape(match.wildcardDirectoryPart);
            }

            return String.Empty;
        }
    }
}
