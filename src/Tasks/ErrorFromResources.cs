// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Task that emits an error given a resource string. Engine will add project file path and line/column
    /// information.
    /// </summary>
    public sealed class ErrorFromResources : TaskExtension
    {
        /// <summary>
        /// Resource from which error message is extracted
        /// </summary>
        [Required]
        public string Resource { get; set; }

        /// <summary>
        /// Optional arguments to use when formatting the error message
        /// </summary>
        public string[] Arguments { get; set; }

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
        /// Log the requested error message.
        /// </summary>
        public override bool Execute()
        {
            try
            {
                string message = ResourceUtilities.ExtractMessageCode(false /* all codes */, Log.FormatResourceString(Resource, Arguments), out string errorCode);

                // If the user specifies a code, that should override.
                Code ??= errorCode;

                Log.LogError(null, Code, HelpKeyword, File, 0, 0, 0, 0, message);
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                Log.LogErrorWithCodeFromResources("ErrorFromResources.LogErrorFailure", Resource, e.Message);
            }

            // Effectively 'false', since by every codepath, some sort of error is getting logged.
            return !Log.HasLoggedErrors;
        }
    }
}
