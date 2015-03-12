// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.Deployment.Bootstrapper
{
    /// <summary>
    /// Represents messages that occur during the BootstrapperBuilder's Build operation.
    /// </summary>
    public class BuildMessage : IBuildMessage
    {
        private BuildMessageSeverity _severity;
        private string _message;
        private string _helpKeyword;
        private string _helpCode;
        private int _helpId;

        private static readonly Regex s_msbuildMessageCodePattern = new Regex(@"(\d+)$");

        private BuildMessage(BuildMessageSeverity severity, string message, string helpKeyword, string helpCode)
        {
            _severity = severity;
            _message = message;
            _helpKeyword = helpKeyword;
            _helpCode = helpCode;
            if (!String.IsNullOrEmpty(_helpCode))
            {
                Match match = s_msbuildMessageCodePattern.Match(_helpCode);
                if (match.Success)
                {
                    _helpId = int.Parse(match.Value, CultureInfo.InvariantCulture);
                }
            }
        }

        internal static BuildMessage CreateMessage(BuildMessageSeverity severity, string resourceName, params object[] args)
        {
            string helpCode;
            string helpKeyword;
            string message = ResourceUtilities.FormatResourceString(out helpCode, out helpKeyword, resourceName, args);

            return new BuildMessage(severity, message, helpKeyword, helpCode);
        }

        /// <summary>
        /// This severity of this build message
        /// </summary>
        public BuildMessageSeverity Severity
        {
            get { return _severity; }
        }

        /// <summary>
        /// A text string describing the details of the build message
        /// </summary>
        public string Message
        {
            get { return _message; }
        }

        /// <summary>
        /// The MSBuild F1-help keyword for the host IDE, or null
        /// </summary>
        public string HelpKeyword
        {
            get { return _helpKeyword; }
        }

        /// <summary>
        /// The MSBuild help id for the host IDE
        /// </summary>
        public int HelpId
        {
            get { return _helpId; }
        }

        internal string HelpCode
        {
            get { return _helpCode; }
        }
    }
}
