// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Task that emits an error given a resource string. Engine will add project file path and line/column
    /// information.
    /// </summary>
    [MSBuildMultiThreadableTask]
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
                string message = Log.FormatResourceString(Resource, Arguments);

                if (MessageParser.TryParseAnyCode(message, out string errorCode, out string strippedMessage))
                {
                    message = strippedMessage;
                }

                // If the user specified a code, that should override.
                Code ??= errorCode;

                Log.LogError(
                    subcategory: null,
                    Code,
                    HelpKeyword,
                    File,
                    lineNumber: 0,
                    columnNumber: 0,
                    endLineNumber: 0,
                    endColumnNumber: 0,
                    message);
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
