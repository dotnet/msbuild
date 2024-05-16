// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.Deployment.Bootstrapper;

namespace Microsoft.Build.Tasks
{
    public sealed class NETMessage : TaskExtension
    {
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
                        return false;

                    case BuildMessageSeverity.Warning:
                        Log.LogWarningWithCodeFromResources(ResourceName, FormatArguments);
                        return true;

                    case BuildMessageSeverity.Info:
                        MessageImportance importance = (MessageImportance)Enum.Parse(typeof(MessageImportance), MessageImportance, true);
                        Log.LogMessageFromResources(importance, ResourceName, FormatArguments);
                        return true;

                    default:
                        return false;
                }
            }

            Log.LogMessageFromResources("CommonTarget.SpecifiedSeverityDoesNotExist", Severity);

            return true;
        }
    }
}
