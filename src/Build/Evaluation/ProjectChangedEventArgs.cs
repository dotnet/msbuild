// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Event arguments for the <see cref="ProjectCollection.ProjectChanged"/> event.
    /// </summary>
    public class ProjectChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectChangedEventArgs"/> class.
        /// </summary>
        /// <param name="project">The changed project.</param>
        internal ProjectChangedEventArgs(Project project)
        {
            ErrorUtilities.VerifyThrowArgumentNull(project, "project");

            Project = project;
        }

        /// <summary>
        /// Gets the project that was marked dirty.
        /// </summary>
        /// <value>Never null.</value>
        public Project Project { get; private set; }
    }
}
