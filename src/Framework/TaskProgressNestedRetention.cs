// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Specifies what a live display does with a nested operation after the nested operation ends.
    /// </summary>
    /// <remarks>
    /// The parent operation chooses the value when it creates the nested reporter. The value affects only
    /// live displays, such as the Terminal Logger. Logs that record history, such as binary logs and file
    /// loggers, keep every operation.
    /// </remarks>
    public enum TaskProgressNestedRetention
    {
        /// <summary>
        /// The display removes the nested operation when the nested operation ends.
        /// </summary>
        Remove = 0,

        /// <summary>
        /// The display keeps the final state of the nested operation until the parent operation ends.
        /// </summary>
        Persist = 1,
    }
}
