// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of a logged item group entry.
    /// </summary>
    internal class ItemGroup : TaskParameter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ItemGroup"/> class.
        /// </summary>
        /// <param name="message">The message from the logger.</param>
        /// <param name="prefix">The prefix string (e.g. 'Added item(s): ').</param>
        /// <param name="itemAttributeName">Name of the item attribute ('Include' or 'Remove').</param>
        public ItemGroup(string message, string prefix, string itemAttributeName) :
            base(message, prefix, false, itemAttributeName)
        {
        }
    }
}
