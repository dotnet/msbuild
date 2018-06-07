// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
