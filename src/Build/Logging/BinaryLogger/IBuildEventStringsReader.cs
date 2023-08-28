// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// An interface for notifications about reading strings from the binary log.
    /// </summary>
    public interface IBuildEventStringsReader
    {
        /// <summary>
        /// An event that allows the subscriber to be notified when a string is read from the binary log.
        /// Subscriber may adjust the string by setting <see cref="StringReadEventArgs.StringToBeUsed"/> property.
        /// The passed event arg can be reused and should not be stored.
        /// </summary>
        public event Action<StringReadEventArgs>? StringReadDone;

        /// <summary>
        /// An event that allows the caller to be notified when a string is encountered in the binary log.
        /// BinaryReader passed in ctor is at the beginning of the string at this point.
        /// </summary>
        public event Action? StringEncountered;
    }
}
