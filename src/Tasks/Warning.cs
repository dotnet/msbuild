﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Task that simply emits a warning. Engine will add the project path because
    /// we do not specify a filename.
    /// </summary>
    public sealed class Warning : TaskExtension
    {
        /// <summary>
        /// Error message
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Warning code
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Relevant file if any.
        /// If none is provided, the file containing the Warning
        /// task will be used.
        /// </summary>
        public string File { get; set; }

        /// <summary>
        /// Warning help keyword
        /// </summary>
        public string HelpKeyword { get; set; }

        /// <summary>
        /// A link pointing to more information about the warning
        /// </summary>
        public string HelpLink { get; set; }

        /// <summary>
        /// Main task method
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            Log.LogWarning(null, Code, HelpKeyword, HelpLink, File, 0, 0, 0, 0, Text ?? TaskResources.GetString("ErrorAndWarning.EmptyMessage"));

            return true;
        }
    }
}
