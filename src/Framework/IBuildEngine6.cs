// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface extends <see cref="IBuildEngine5" /> to allow tasks to get the current project's global properties.
    /// </summary>
    public interface IBuildEngine6 : IBuildEngine5
    {
        /// <summary>
        /// Gets the global properties for the current project.
        /// </summary>
        /// <returns>An <see cref="IReadOnlyDictionary{String, String}" /> containing the global properties of the current project.</returns>
        IReadOnlyDictionary<string, string> GetGlobalProperties();
    }
}
