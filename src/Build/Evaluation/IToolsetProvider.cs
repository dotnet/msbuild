// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Interface for an object which can provide toolsets for evaulation.
    /// </summary>
    internal interface IToolsetProvider
    {
        /// <summary>
        /// Gets an enumeration of all toolsets in the provider.
        /// </summary>
        ICollection<Toolset> Toolsets
        {
            get;
        }

        /// <summary>
        /// Retrieves a specific toolset.
        /// </summary>
        /// <param name="toolsVersion">The tools version for the toolset.</param>
        /// <returns>The requested toolset.</returns>
        Toolset GetToolset(string toolsVersion);
    }
}
