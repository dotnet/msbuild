// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// An interface representing an object which can act as a property.
    /// </summary>
    internal interface IProperty : IKeyed
    {
        /// <summary>
        /// Name of the property.
        /// </summary>
        string Name
        {
            get;
        }

        /// <summary>
        /// Returns the evaluated, unescaped value for the property.
        /// </summary>
        string EvaluatedValue
        {
            get;
        }

        /// <summary>
        /// Returns the evaluated, escaped value for the property.
        /// </summary>
        string EvaluatedValueEscaped
        {
            get;
        }

        /// <summary>
        /// Returns the evaluated, escaped value for the property and sets up the property location if it is available.
        /// </summary>
        string GetEvaluatedValueEscaped(IElementLocation location);

        /// <summary>
        /// Returns property location in xml file. Can be empty.
        /// </summary>
        (string File, int Line, int Column) Location
        {
            get;
        }
    }
}
