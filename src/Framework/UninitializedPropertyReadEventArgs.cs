// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// The arguments for an uninitialized property read event.
    /// </summary>
    [Serializable]
    public class UninitializedPropertyReadEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// UninitializedPropertyReadEventArgs
        /// </summary>
        public UninitializedPropertyReadEventArgs()
        {
        }

        /// <summary>
        /// Creates an instance of the UninitializedPropertyReadEventArgs class
        /// </summary>
        /// <param name="propertyName">The name of the uninitialized property that was read.</param>
        public UninitializedPropertyReadEventArgs(
            string propertyName,
            string message,
            string helpKeyword = null,
            string senderName = null,
            MessageImportance importance = MessageImportance.Low) : base(message, helpKeyword, senderName, importance)
        {
            this.PropertyName = propertyName;
        }

        /// <summary>
        /// The name of the uninitialized property that was read.
        /// </summary>
        public string PropertyName { get; set; }
    }
}
