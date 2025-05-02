// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Represents a xml node in a project file that defines a property.
    /// </summary>
    internal interface IPropertyElementWithLocation
    {
        /// <summary>
        /// Name of the property.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Unevaluated value of the property.
        /// </summary>
        string Value { get; }

        /// <summary>
        /// Location of the property element within build scripts.
        /// </summary>
        ElementLocation Location { get; }
    }
}
