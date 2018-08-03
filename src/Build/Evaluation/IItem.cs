// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;

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
        /// Returns null if it does not exist.
        /// Attempting to get built-in metadata on a value that is not a valid path throws InvalidOperationException.
        /// Metadata value is unescaped.
        /// </summary>
        string GetMetadataValue(string name);

        /// <summary>
        /// Returns the metadata with the specified key.
        /// Returns null if it does not exist.
        /// Attempting to get built-in metadata on a value that is not a valid path throws InvalidOperationException.
        /// Metadata value is the escaped value initially set.
        /// </summary>
        string GetMetadataValueEscaped(string name);
    }
}
