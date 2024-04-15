// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the project evaluation started event.
    /// </summary>
    [Serializable]
    public class ProjectEvaluationStartedEventArgs : BuildStatusEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the ProjectEvaluationStartedEventArgs class.
        /// </summary>
        public ProjectEvaluationStartedEventArgs()
        {
        }

        /// <summary>
        /// Initializes a new instance of the ProjectEvaluationStartedEventArgs class.
        /// </summary>
        public ProjectEvaluationStartedEventArgs(string? message, params object[]? messageArgs)
            : base(message, helpKeyword: null, senderName: null, DateTime.UtcNow, messageArgs)
        {
        }

        public ProjectEvaluationStartedEventArgs(bool isRestore, string? message, params object[]? messageArgs)
            : base(message, helpKeyword: null, senderName: null, DateTime.UtcNow, messageArgs)
        {
            IsRestore = isRestore;
        }

        /// <summary>
        /// Gets or sets the full path of the project that started evaluation.
        /// </summary>
        public string? ProjectFile { get; set; }

        /// <summary>
        /// Gets the set of global properties to be used to evaluate this project.
        /// </summary>
        public bool IsRestore { get; internal set; }
    }
}
