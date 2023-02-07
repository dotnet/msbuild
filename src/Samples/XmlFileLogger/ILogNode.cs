// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml.Linq;

#nullable disable

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Interface class for an execution MSBuild log node to be represented in XML
    /// </summary>
    internal interface ILogNode
    {
        /// <summary>
        /// Writes the node to XML XElement representation.
        /// </summary>
        /// <param name="parentElement">The parent element.</param>
        void SaveToElement(XElement parentElement);
    }
}
