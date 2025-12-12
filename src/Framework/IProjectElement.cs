// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Interface for exposing a ProjectElement to the appropriate loggers
    /// </summary>
    public interface IProjectElement
    {
        /// <summary>
        /// Gets the name of the associated element.
        /// Useful for display in some circumstances.
        /// </summary>
        string ElementName { get; }


        /// <summary>
        /// The outer markup associated with this project element
        /// </summary>
        string OuterElement { get; }
    }
}
