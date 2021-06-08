// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.TemplateEngine.Cli
{
    internal static class ReporterExtensions
    {
        private const int IndentSpaceCount = 3;

        /// <summary>
        /// Writes string formatted as command example. For now, just indentation is applied.
        /// The extension forces the same format for all command examples.
        /// </summary>
        /// <param name="reporter"></param>
        /// <param name="command"></param>
        /// <param name="indentLevel"></param>
        internal static void WriteCommand(this Reporter reporter, string command, int indentLevel = 0)
        {
            reporter.WriteLine(command.Indent(indentLevel + 1));
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
    }
}
