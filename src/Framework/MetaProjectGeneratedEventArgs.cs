// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the metaproject generated event.
    /// </summary>
    [Serializable]
    public class MetaProjectGeneratedEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Path associated with metaproject.
        /// </summary>
        public string metaProjectPath;

        /// <summary>
        /// Initializes a new instance of the MetaProjectGeneratedEventArgs class.
        /// </summary>
        public MetaProjectGeneratedEventArgs(string metaProjectPath, string message)
            : base(message, null, null, MessageImportance.Low, DateTime.UtcNow, metaProjectPath)
        {
            this.metaProjectPath = metaProjectPath;
        }
    }
}
