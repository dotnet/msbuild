// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Class representation of a task input parameter.
    /// </summary>
    internal class InputParameter : TaskParameter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InputParameter"/> class.
        /// </summary>
        /// <param name="message">The message from the logger.</param>
        /// <param name="prefix">The prefix string (e.g. 'Output Property: ').</param>
        public InputParameter(string message, string prefix)
            : base(message, prefix)
        {
        }
    }
}
