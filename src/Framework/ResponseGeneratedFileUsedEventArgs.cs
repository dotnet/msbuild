// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the response file used event
    /// </summary>
    [Serializable]
    public class ResponseGeneratedFileUsedEventArgs : BuildMessageEventArgs
    {
        public ResponseGeneratedFileUsedEventArgs()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResponseGeneratedFileUsedEventArgs"/> class.
        /// </summary>
        /// 
        public ResponseGeneratedFileUsedEventArgs(string filePath, string content)
            : base("", null, null, MessageImportance.Low)
        {
            ResponseFilePath = filePath;
            ResponseFileContent = content;
        }

        public string? ResponseFilePath { set; get; }

        public string? ResponseFileContent { set; get; }
    }
}
