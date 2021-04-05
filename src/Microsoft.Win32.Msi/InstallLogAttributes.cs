// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
namespace Microsoft.Win32.Msi
{
    /// <summary>
    /// Defines additional attributes to modify logging behavior.
    /// </summary>
    public enum InstallLogAttributes
    {
        /// <summary>
        /// Append to the existing log when set, otherwise the existing log is overwritten.
        /// </summary>
        APPEND = 0x0001,

        /// <summary>
        /// Forces the log buffer to be flushed after each line when set, otherwise the buffer
        /// is flushed after 20 lines.
        /// </summary>
        FLUSHEACHLINE = 0x0002
    }
}
