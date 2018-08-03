// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
