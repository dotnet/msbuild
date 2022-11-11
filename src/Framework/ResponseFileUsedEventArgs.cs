// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the response file used event
    /// </summary>
    [Serializable]
    public class ResponseFileUsedEventArgs : CustomBuildEventArgs
    {
        /// <summary>
        /// Initialize a new instance of the ResponseFileUsedEventArgs class.
        /// </summary>
        public ResponseFileUsedEventArgs() : base() {
            ResponseFilePath = "";
        }

        public ResponseFileUsedEventArgs(string responseFilePath) : base()
        {
            ResponseFilePath = responseFilePath;
        }

        public string ResponseFilePath { get; set; }
    }
}
