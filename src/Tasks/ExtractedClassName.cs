// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Extracted class name from the source file.
    /// </summary>
    public struct ExtractedClassName
    {
        /// <summary>
        /// Whether or not we found the name inside a block of conditionally compiled code
        /// </summary>
        public bool IsInsideConditionalBlock { get; set; }

        /// <summary>
        /// Extracted class name
        /// </summary>
        public string Name { get; set; }
    }
}
