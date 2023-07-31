// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.TemplateEngine.Cli
{
    internal static class IReporterExtensions
    {
        private const int IndentSpaceCount = 3;

        /// <summary>
        /// Writes string formatted as command example. For now, just indentation is applied.
        /// The extension forces the same format for all command examples.
        /// </summary>
        /// <param name="reporter"></param>
        /// <param name="command"></param>
        /// <param name="indentLevel"></param>
        internal static void WriteCommand(this IReporter reporter, string command, int indentLevel = 0)
        {
            reporter.WriteLine(command.Indent(indentLevel + 1));
        }

        /// <summary>
        /// Writes formatted command output from <paramref name="process"/>.
        /// </summary>
        internal static void WriteCommandOutput(this IReporter reporter, Process process)
        {
            if (process.StartInfo.RedirectStandardOutput || process.StartInfo.RedirectStandardError)
            {
                reporter.WriteLine(LocalizableStrings.CommandOutput);
            }
            if (process.StartInfo.RedirectStandardOutput)
            {
                reporter.WriteStdOut(process.StandardOutput.ReadToEnd());
            }
            if (process.StartInfo.RedirectStandardError)
            {
                reporter.WriteStdErr(process.StandardError.ReadToEnd());
            }
        }

        /// <summary>
        /// Writes string <paramref name="output"/> formatted as standard output.
        /// </summary>
        internal static void WriteStdOut(this IReporter reporter, string output)
        {
            reporter.WriteLine("StdOut:");
            reporter.WriteLine(string.IsNullOrWhiteSpace(output) ? LocalizableStrings.Generic_Empty : output);
        }

        /// <summary>
        /// Writes string <paramref name="output"/> formatted as standard error.
        /// </summary>
        internal static void WriteStdErr(this IReporter reporter, string output)
        {
            reporter.WriteLine("StdErr:");
            reporter.WriteLine(string.IsNullOrWhiteSpace(output) ? LocalizableStrings.Generic_Empty : output);
        }

        /// <summary>
        /// Indents string, use this method to unify indents in the output.
        /// </summary>
        /// <param name="s">string to indent.</param>
        /// <param name="level">indent level.</param>
        /// <returns></returns>
        internal static string Indent(this string s, int level = 1)
        {
            return new string(' ', IndentSpaceCount * level) + s;
        }

        /// <summary>
        /// Indent all lines in the string, use this method to unify indents in the output.
        /// </summary>
        /// <param name="s">string to indent.</param>
        /// <param name="level">indent level.</param>
        /// <returns></returns>
        internal static string IndentLines(this string s, int level = 1)
        {
            if (!s.Contains('\n'))
            {
                return s.Indent(level);
            }
            StringBuilder builder = new();
            using StringReader sr = new(s);
            bool firstLine = true;
            string? line = sr.ReadLine();
            while (line != null)
            {
                if (!firstLine)
                {
                    builder.AppendLine();
                }
                else
                {
                    firstLine = false;
                }
                builder.Indent(level).Append(line);
                line = sr.ReadLine();
            }
            return builder.ToString();
        }

        /// <summary>
        /// Indents stringbuilder, use this method to unify indents in the output.
        /// </summary>
        /// <param name="s">string to indent.</param>
        /// <param name="level">indent level.</param>
        /// <returns></returns>
        internal static StringBuilder Indent(this StringBuilder s, int level = 1)
        {
            return s.Append(' ', IndentSpaceCount * level);
        }
    }
}
