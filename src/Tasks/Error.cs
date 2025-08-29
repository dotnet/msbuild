﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Task that simply emits an error. Engine will add project file path and line/column
    /// information.
    /// </summary>
    public sealed class Error : TaskExtension
    {
        /// <summary>
        /// Error message
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Error code
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Relevant file if any.
        /// If none is provided, the file containing the Error
        /// task will be used.
        /// </summary>
        public string File { get; set; }

        /// <summary>
        /// Error help keyword
        /// </summary>
        public string HelpKeyword { get; set; }

        /// <summary>
        /// A link pointing to more information about the error
        /// </summary>
        public string HelpLink { get; set; }

        /// <summary>
        /// Main task method
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            Log.LogError(null, Code, HelpKeyword, HelpLink, File, 0, 0, 0, 0, Text ?? TaskResources.GetString("ErrorAndWarning.EmptyMessage"));

            // careful to return false. Otherwise the build would continue.
            return false;
        }
    }
}
