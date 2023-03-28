// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of a task output item group.
    /// </summary>
    internal sealed class OutputItem : TaskParameter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OutputItem"/> class.
        /// </summary>
        /// <param name="message">The message from the logger..</param>
        /// <param name="prefix">The prefix string (e.g. 'Output Item(s): ').</param>
        public OutputItem(string message, string prefix)
            : base(message, prefix)
        {
        }
    }
}
