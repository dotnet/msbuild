// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Extracted class name from the source file.
    /// </summary>
    public struct ExtractedClassName
    {
        // whether or not we found the name inside a block of conditionally compiled code
        private bool _isInsideConditionalBlock;
        // the extracted class name
        private string _name;

        /// <summary>
        /// Whether or not we found the name inside a block of conditionally compiled code
        /// </summary>
        public bool IsInsideConditionalBlock
        {
            get { return _isInsideConditionalBlock; }
            set { _isInsideConditionalBlock = value; }
        }

        /// <summary>
        /// Extracted class name
        /// </summary>
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }
    }
}