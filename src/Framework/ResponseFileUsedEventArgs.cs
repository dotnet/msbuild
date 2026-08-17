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
        /// Initialize a new instance of the ResponseFileUsedEventArgs class.
        /// </summary>
        public ResponseFileUsedEventArgs(string? responseFilePath) : base()
        {
            ResponseFilePath = responseFilePath;
        }
        public string? ResponseFilePath { set; get; }
    }
}
