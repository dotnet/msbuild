﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// This interface represents an item without exposing its type.
    /// </summary>
    internal interface IItem : IKeyed
    {
        /// <summary>
        /// Gets the evaluated include value for this item, unescaped.
        /// </summary>
        string EvaluatedInclude
        {
            get;
        }

        /// <summary>
        /// Gets the evaluated include value for this item, escaped as necessary.
        /// </summary>
        string EvaluatedIncludeEscaped
        {
            get;
        }

        /// <summary>
        /// The directory of the project being built
        /// If there is no project filename defined, returns null.
        /// </summary>
        string ProjectDirectory
        {
            get;
        }

        /// <summary>
        /// Returns the metadata with the specified key.
        /// Returns an empty string if it does not exist.
        /// Attempting to get built-in metadata on a value that is not a valid path throws InvalidOperationException.
        /// Metadata value is unescaped.
        /// </summary>
        string GetMetadataValue(string name);

        /// <summary>
        /// Returns the metadata with the specified key.
        /// Returns an empty string if it does not exist.
        /// Attempting to get built-in metadata on a value that is not a valid path throws InvalidOperationException.
        /// Metadata value is the escaped value initially set.
        /// </summary>
        string GetMetadataValueEscaped(string name);
    }
}
