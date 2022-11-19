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
        public string ResponseFilePath = "";
        /// <summary>
        /// Initialize a new instance of the ResponseFileUsedEventArgs class.
        /// </summary>
        public ResponseFileUsedEventArgs(string responseFilePath) : base()
        {
            ResponseFilePath = responseFilePath;
        }
    }
}
