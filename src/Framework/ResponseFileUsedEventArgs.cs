// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the response file used event
    /// </summary>
    [Serializable]
    public class ResponseFileUsedEventArgs : BuildMessageEventArgs
    {
        public ResponseFileUsedEventArgs()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResponseFileUsedEventArgs"/> class.
        /// Initialize a new instance of the ResponseFileUsedEventArgs class.
        /// </summary>
        public ResponseFileUsedEventArgs(string? responseFilePath)
            : base(null, null, null, MessageImportance.Low)
        {
            ResponseFilePath = responseFilePath;
        }

        public string? ResponseFilePath { set; get; }
    }
}
