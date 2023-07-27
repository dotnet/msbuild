// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Flags defining formatting options for system messages. 
    /// See https://docs.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-formatmessage 
    /// </summary>
    [Flags]
    public enum FormatMessage
    {
        /// <summary>
        /// Allocate a buffer large enough to hold the formatted message.
        /// </summary>
        AllocateBuffer = 0x00000100,
        /// <summary>
        /// Insert sequences in the message definition such as %1 are to be ignored and passed through.
        /// </summary>
        IgnoreInserts = 0x00000200,
        /// <summary>
        /// The source parameter is a pointer to a null-terminated string that contains a message definition.
        /// </summary>
        FromString = 0x00000400,
        /// <summary>
        /// The source parameter is module handle containing the message table resources to search.
        /// </summary>
        FromHModule = 0x00000800,
        /// <summary>
        /// The function should search the system message-table resource(s) for the requested message. When used with 
        /// <see cref="FromHModule"/>, the system table is searched if the message is not found in the specified module.
        /// </summary>
        FromSystem = 0x00001000,
        /// <summary>
        /// The Arguments parameter is not a va_list structure, but is a pointer to an array of values that represent the arguments.
        /// </summary>
        ArgumentArray = 0x00002000,
    }
}
