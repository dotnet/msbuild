// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    /// <summary>
    /// Represents messages that occur during the BootstrapperBuilder's Build operation.
    /// </summary>
    public partial class BuildMessage : IBuildMessage
    {
#if NET
        [GeneratedRegex(@"\d+$")]
        private static partial Regex MsbuildMessageCodePattern { get; }
#else
        private static Regex MsbuildMessageCodePattern { get; } = new Regex(@"\d+$");
#endif

        private BuildMessage(BuildMessageSeverity severity, string message, string helpKeyword, string helpCode)
        {
            Severity = severity;
            Message = message;
            HelpKeyword = helpKeyword;
            HelpCode = helpCode;
            if (!String.IsNullOrEmpty(HelpCode))
            {
                Match match = MsbuildMessageCodePattern.Match(HelpCode);
                if (match.Success)
                {
                    HelpId = int.Parse(match.Value, CultureInfo.InvariantCulture);
                }
            }
        }

        internal static BuildMessage CreateMessage(BuildMessageSeverity severity, string resourceName, params object[] args)
        {
            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out string helpCode, out string helpKeyword, resourceName, args);

            return new BuildMessage(severity, message, helpKeyword, helpCode);
        }

        /// <summary>
        /// This severity of this build message
        /// </summary>
        public BuildMessageSeverity Severity { get; }

        /// <summary>
        /// A text string describing the details of the build message
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// The MSBuild F1-help keyword for the host IDE, or null
        /// </summary>
        public string HelpKeyword { get; }

        /// <summary>
        /// The MSBuild help id for the host IDE
        /// </summary>
        public int HelpId { get; }

        internal string HelpCode { get; }
    }
}
