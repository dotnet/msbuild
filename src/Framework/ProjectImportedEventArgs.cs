// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the project imported event.
    /// </summary>
#if FEATURE_BINARY_SERIALIZATION
    [Serializable]
#endif
    public class ProjectImportedEventArgs : BuildMessageEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the ProjectImportedEventArgs class.
        /// </summary>
        public ProjectImportedEventArgs()
        {
        }

        /// <summary>
        /// Initializes a new instance of the ProjectImportedEventArgs class.
        /// </summary>
        public ProjectImportedEventArgs
        (
            int lineNumber,
            int columnNumber,
            string message,
            params object[] messageArgs
        )
            : base(null, null, null, lineNumber, columnNumber, 0, 0, message, null, null, MessageImportance.Low, DateTime.UtcNow, messageArgs)
        {
        }

        /// <summary>
        /// Gets or sets the original value of the Project attribute.
        /// </summary>
        public string UnexpandedProject { get; set; }

        /// <summary>
        /// Gets or sets the full path to the project file that was imported.  If a project was not imported, the value is <code>null</code>.
        /// </summary>
        public string ImportedProjectFile { get; set; }
    }
}