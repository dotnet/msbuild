// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;

#nullable disable

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
            ErrorUtilities.VerifyThrowArgumentNull(project, nameof(project));

            Project = project;
        }

        /// <summary>
        /// Gets the project that was marked dirty.
        /// </summary>
        /// <value>Never null.</value>
        public Project Project { get; private set; }
    }
}
