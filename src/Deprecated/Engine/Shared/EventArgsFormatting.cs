// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Collections.Generic;
using System.Text;

using Microsoft.Build.Framework;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// Offers a default string format for Error and Warning events
    /// </summary>
    internal static class EventArgsFormatting
    {
        /// <summary>
        /// Escape the carriage Return from a string
        /// </summary>
        /// <param name="stringWithCarriageReturn"></param>
        /// <returns>String with carriage returns escaped as \\r </returns>
        internal static string EscapeCarriageReturn(string stringWithCarriageReturn)
        {
            if (!string.IsNullOrEmpty(stringWithCarriageReturn))
            {
                return stringWithCarriageReturn.Replace("\r", "\\r");
            }
            // If the string is null or empty or then we just return the string
            return stringWithCarriageReturn;
        }

        /// <summary>
        /// Format the error event message and all the other event data into
        /// a single string.
        /// </summary>
        /// <owner>t-jeffv</owner>
        /// <param name="e">Error to format</param>
        /// <returns>The formatted message string.</returns>
        internal static string FormatEventMessage(BuildErrorEventArgs e)
        {
            return FormatEventMessage(e, false);
        }

        /// <summary>
        /// Format the error event message and all the other event data into
        /// a single string.
        /// </summary>
        /// <owner>t-jeffv</owner>
        /// <param name="e">Error to format</param>
        /// <returns>The formatted message string.</returns>
        internal static string FormatEventMessage(BuildErrorEventArgs e, bool removeCarriageReturn)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e, nameof(e));

            // "error" should not be localized
            return FormatEventMessage("error", e.Subcategory, removeCarriageReturn ? EscapeCarriageReturn(e.Message) : e.Message,
                            e.Code, e.File, e.LineNumber, e.EndLineNumber,
                            e.ColumnNumber, e.EndColumnNumber, e.ThreadId);
        }

        /// <summary>
        /// Format the warning message and all the other event data into a
        /// single string.
        /// </summary>
        /// <owner>t-jeffv, sumedhk</owner>
        /// <param name="e">Warning to format</param>
        /// <returns>The formatted message string.</returns>
        internal static string FormatEventMessage(BuildWarningEventArgs e)
        {
            return FormatEventMessage(e, false);
        }

        /// <summary>
        /// Format the warning message and all the other event data into a
        /// single string.
        /// </summary>
        /// <owner>t-jeffv, sumedhk</owner>
        /// <param name="e">Warning to format</param>
        /// <returns>The formatted message string.</returns>
        internal static string FormatEventMessage(BuildWarningEventArgs e, bool removeCarriageReturn)
        {
            ErrorUtilities.VerifyThrowArgumentNull(e, nameof(e));

            // "warning" should not be localized
            return FormatEventMessage("warning", e.Subcategory, removeCarriageReturn ? EscapeCarriageReturn(e.Message) : e.Message,
                           e.Code, e.File, e.LineNumber, e.EndLineNumber,
                           e.ColumnNumber, e.EndColumnNumber, e.ThreadId);
        }

        /// <summary>
        /// Format the event message and all the other event data into a
        /// single string.
        /// Internal for unit testing only.
        /// </summary>
        /// <owner>t-jeffv, sumedhK</owner>
        /// <param name="category">category ("error" or "warning")</param>
        /// <param name="subcategory">subcategory</param>
        /// <param name="message">event message</param>
        /// <param name="code">error or warning code number</param>
        /// <param name="file">file name</param>
        /// <param name="lineNumber">line number (0 if n/a)</param>
        /// <param name="endLineNumber">end line number (0 if n/a)</param>
        /// <param name="columnNumber">column number (0 if n/a)</param>
        /// <param name="endColumnNumber">end column number (0 if n/a)</param>
        /// <param name="threadId">thread id</param>
        /// <returns>The formatted message string.</returns>
        internal static string FormatEventMessage
        (
            string category,
            string subcategory,
            string message,
            string code,
            string file,
            int lineNumber,
            int endLineNumber,
            int columnNumber,
            int endColumnNumber,
            int threadId
        )
        {
            StringBuilder format = new StringBuilder();

            // Uncomment these lines to show show the processor, if present.
            /*
            if (threadId != 0)
            {
                format.Append("{0}>");
            }
            */

            if (string.IsNullOrEmpty(file))
            {
                format.Append("MSBUILD : ");    // Should not be localized.
            }
            else
            {
                format.Append("{1}");

                if (lineNumber == 0)
                {
                    format.Append(" : ");
                }
                else
                {
                    if (columnNumber == 0)
                    {
                        if (endLineNumber == 0)
                        {
                            format.Append("({2}): ");
                        }
                        else
                        {
                            format.Append("({2}-{7}): ");
                        }
                    }
                    else
                    {
                        if (endLineNumber == 0)
                        {
                            if (endColumnNumber == 0)
                            {
                                format.Append("({2},{3}): ");
                            }
                            else
                            {
                                format.Append("({2},{3}-{8}): ");
                            }
                        }
                        else
                        {
                            if (endColumnNumber == 0)
                            {
                                format.Append("({2}-{7},{3}): ");
                            }
                            else
                            {
                                format.Append("({2},{3},{7},{8}): ");
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(subcategory))
            {
                format.Append("{9} ");
            }

            // The category as a string (should not be localized)
            format.Append("{4} ");

            // Put a code in, if available and necessary.
            if (code == null)
            {
                format.Append(": ");
            }
            else
            {
                format.Append("{5}: ");
            }

            // Put the message in, if available.
            if (message != null)
            {
                format.Append("{6}");
            }

            // A null message is allowed and is to be treated as a blank line.
            if (message == null)
            {
                message = String.Empty;
            }

            string finalFormat = format.ToString();

            // If there are multiple lines, show each line as a separate message.
            string[] lines = SplitStringOnNewLines(message);
            StringBuilder formattedMessage = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                formattedMessage.AppendFormat(
                        CultureInfo.CurrentCulture, finalFormat,
                        threadId, file,
                        lineNumber, columnNumber, category, code,
                        lines[i], endLineNumber, endColumnNumber,
                        subcategory);

                if (i < (lines.Length - 1))
                {
                    formattedMessage.AppendLine();
                }
            }

            return formattedMessage.ToString();
        }

        /// <summary>
        /// Splits strings on 'newLines' with tolerance for Everett and Dogfood builds.
        /// </summary>
        /// <param name="s">String to split.</param>
        /// <owner>t-jeffv, sumedhk</owner>
        private static string[] SplitStringOnNewLines(string s)
        {
            string[] subStrings = s.Split(newLines, StringSplitOptions.None);
            return subStrings;
        }

        /// <summary>
        /// The kinds of newline breaks we expect.
        /// </summary>
        /// <remarks>Currently we're not supporting "\r".</remarks>
        private static readonly string[] newLines = { "\r\n", "\n" };
    }
}
