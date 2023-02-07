// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the file used event
    /// </summary>
    [Serializable]
    public class FileUsedEventArgs : BuildMessageEventArgs
    {
        public FileUsedEventArgs()
        {
        }
        /// <summary>
        /// Initialize a new instance of the FileUsedEventArgs class.
        /// </summary>
        public FileUsedEventArgs(string filePath) : base()
        {
            FilePath = filePath;
        }
        public string? FilePath { set; get; }
    }
}
