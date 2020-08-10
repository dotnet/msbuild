// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Event arguments for the <see cref="ProjectCollection.ProjectXmlChanged"/> event.
    /// </summary>
    public class ProjectXmlChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The unformatted reason for dirtying the project collection.
        /// </summary>
        private readonly string _unformattedReason;

        /// <summary>
        /// The formatting parameter.
        /// </summary>
        private readonly string _formattingParameter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectXmlChangedEventArgs"/> class
        /// that represents a change to a specific project root element.
        /// </summary>
        /// <param name="projectXml">The ProjectRootElement whose content was actually changed.</param>
        /// <param name="unformattedReason">The unformatted (may contain {0}) reason for the dirty event.</param>
        /// <param name="formattingParameter">The formatting parameter to use with <paramref name="unformattedReason"/>.</param>
        internal ProjectXmlChangedEventArgs(ProjectRootElement projectXml, string unformattedReason, string formattingParameter)
        {
            ErrorUtilities.VerifyThrowArgumentNull(projectXml, nameof(projectXml));

            this.ProjectXml = projectXml;
            _unformattedReason = unformattedReason;
            _formattingParameter = formattingParameter;
        }

        /// <summary>
        /// Gets the project root element which was just changed..
        /// </summary>
        /// <value>Never null.</value>
        public ProjectRootElement ProjectXml { get; private set; }

        /// <summary>
        /// Gets the reason for the change.
        /// </summary>
        /// <value>May be null.</value>
        public string Reason
        {
            get { return _unformattedReason != null ? String.Format(CultureInfo.CurrentCulture, _unformattedReason, _formattingParameter) : null; }
        }
    }
}
