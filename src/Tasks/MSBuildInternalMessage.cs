// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Represents a task that produces localized messages based on the specified resource name.
    /// This task is intended to be called from internal targets only.
    /// </summary>
    public sealed class MSBuildInternalMessage : TaskExtension
    {
        private enum BuildMessageSeverity
        {
            /// <summary>
            /// Indicates that the message corresponds to build information.
            /// </summary>
            Message,

            /// <summary>
            /// Indicates that the message corresponds to a build warning.
            /// </summary>
            Warning,

            /// <summary>
            /// Indicates that the message corresponds to a build error.
            /// </summary>
            Error,
        }

        /// <summary>
        /// The name of the resource in Strings.resx that contains the desired error message.
        /// </summary>
        [Required]
        public string ResourceName { get; set; } = string.Empty;

        /// <summary>
        /// Resource arguments to be used in the format string.
        /// </summary>
        public string[] FormatArguments { get; set; } = [];

        /// <summary>
        /// <see cref="BuildMessageSeverity"/>.
        /// </summary>
        [Required]
        public string Severity { set; get; } = string.Empty;

        /// <summary>
        /// Configurable message importance.
        /// </summary>
        public string MessageImportance { get; set; } = "Normal";

        public override bool Execute()
        {
            if (Enum.TryParse(Severity, ignoreCase: true, out BuildMessageSeverity severity))
            {
                switch (severity)
                {
                    case BuildMessageSeverity.Error:
                        Log.LogErrorWithCodeFromResources(ResourceName, FormatArguments);
                        return !Log.HasLoggedErrors;

                    case BuildMessageSeverity.Warning:
                        Log.LogWarningWithCodeFromResources(ResourceName, FormatArguments);
                        return !Log.HasLoggedErrors;

                    case BuildMessageSeverity.Message:
                        MessageImportance importance = (MessageImportance)Enum.Parse(typeof(MessageImportance), MessageImportance, true);
                        Log.LogMessageFromResources(importance, ResourceName, FormatArguments);
                        return !Log.HasLoggedErrors;

                    default:
                        return !Log.HasLoggedErrors;
                }
            }

            Log.LogErrorFromResources("CommonTarget.SpecifiedSeverityDoesNotExist", Severity);

            return !Log.HasLoggedErrors;
        }
    }
}
