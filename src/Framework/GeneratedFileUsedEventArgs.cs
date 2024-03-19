// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the generated file used event
    /// </summary>
    [Serializable]
    public class GeneratedFileUsedEventArgs : BuildMessageEventArgs
    {
        public GeneratedFileUsedEventArgs()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneratedFileUsedEventArgs"/> class.
        /// </summary>
        /// 
        public GeneratedFileUsedEventArgs(string filePath, string content)
            : base("", null, null, MessageImportance.Low)
        {
            FilePath = filePath;
            Content = content;
        }

        public string? FilePath { set; get; }

        public string? Content { set; get; }
    }
}
