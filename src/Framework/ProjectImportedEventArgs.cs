// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the project imported event.
    /// </summary>
    [Serializable]
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
        /// Gets or sets the full path to the project file that was imported. Will be <code>null</code>
        /// if the import statement was a glob and no files matched, or the condition (if any) evaluated
        /// to false.
        /// </summary>
        public string ImportedProjectFile { get; set; }

        /// <summary>
        /// Gets or sets if this import was ignored. Ignoring imports is controlled by
        /// <code>ProjectLoadSettings</code>. This is only set when an import would have been included
        /// but was ignored to due being invalid. This does not include when a globbed import returned
        /// no matches, or a conditioned import that evaluated to false.
        /// </summary>
        public bool ImportIgnored { get; set; }
    }
}
