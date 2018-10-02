// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;

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
