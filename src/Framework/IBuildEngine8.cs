// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface extends <see cref="IBuildEngine7" /> to let tasks know if a warning
    /// they are about to log will be converted into an error.
    /// </summary>
    public interface IBuildEngine8 : IBuildEngine7
    {
        /// <summary>
        /// Determines whether the logging service will convert the specified
        /// warning code into an error.
        /// </summary>
        /// <param name="warningCode">The warning code to check.</param>
        /// <returns>A boolean to determine whether the warning should be treated as an error.</returns>
        public bool ShouldTreatWarningAsError(string warningCode);
    }
}
