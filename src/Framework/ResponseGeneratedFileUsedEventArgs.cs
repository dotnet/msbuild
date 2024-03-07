// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the response generated file used event.
    /// </summary>
    [Serializable]
    public class ResponseGeneratedFileUsedEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResponseGeneratedFileUsedEventArgs"/> class.
        /// Initialize a new instance of the ResponseFileUsedEventArgs class.
        /// </summary>
        public ResponseGeneratedFileUsedEventArgs(string responseFilePath, string responseFileCode)
            : base(null, null, null, MessageImportance.Low)
        {
            ResponseFilePath = responseFilePath;
            ResponseFileContent = responseFileCode;
        }

        /// <summary>
        /// The file path.
        /// </summary>
        public string ResponseFilePath { set; get; }

        /// <summary>
        /// The file content.
        /// </summary>
        public string ResponseFileContent { set; get; }
    }
}
