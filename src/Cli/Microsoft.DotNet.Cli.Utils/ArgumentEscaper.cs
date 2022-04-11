// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class ArgumentEscaper
    {
        /// <summary>
        /// Undo the processing which took place to create string[] args in Main,
        /// so that the next process will receive the same string[] args
        /// 
        /// See here for more info:
        /// https://docs.microsoft.com/archive/blogs/twistylittlepassagesallalike/everyone-quotes-command-line-arguments-the-wrong-way
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string EscapeAndConcatenateArgArrayForProcessStart(IEnumerable<string> args)
        { 
            var escaped = EscapeArgArray(args);
#if NET35
            return string.Join(" ", escaped.ToArray());
#else
            return string.Join(" ", escaped);
#endif
        }

        /// <summary>
        /// Undo the processing which took place to create string[] args in Main,
        /// so that the next process will receive the same string[] args
        /// 
        /// See here for more info:
        /// https://docs.microsoft.com/archive/blogs/twistylittlepassagesallalike/everyone-quotes-command-line-arguments-the-wrong-way
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static string EscapeAndConcatenateArgArrayForCmdProcessStart(IEnumerable<string> args)
        {
            var escaped = EscapeArgArrayForCmd(args);
#if NET35
            return string.Join(" ", escaped.ToArray());
#else
            return string.Join(" ", escaped);
#endif
        }

        /// <summary>
        /// Undo the processing which took place to create string[] args in Main,
        /// so that the next process will receive the same string[] args
        /// 
        /// See here for more info:
        /// https://docs.microsoft.com/archive/blogs/twistylittlepassagesallalike/everyone-quotes-command-line-arguments-the-wrong-way
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static IEnumerable<string> EscapeArgArray(IEnumerable<string> args)
        {
            var escapedArgs = new List<string>();

            foreach (var arg in args)
            {
                escapedArgs.Add(EscapeSingleArg(arg));
            }

            return escapedArgs;
        }

        /// <summary>
        /// This prefixes every character with the '^' character to force cmd to
        /// interpret the argument string literally. An alternative option would 
        /// be to do this only for cmd metacharacters.
        /// 
        /// See here for more info:
        /// https://docs.microsoft.com/archive/blogs/twistylittlepassagesallalike/everyone-quotes-command-line-arguments-the-wrong-way
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static IEnumerable<string> EscapeArgArrayForCmd(IEnumerable<string> arguments)
        {
            var escapedArgs = new List<string>();

            foreach (var arg in arguments)
            {
                escapedArgs.Add(EscapeArgForCmd(arg));
            }

            return escapedArgs;
        }

        public static string EscapeSingleArg(string arg)
        {
            var sb = new StringBuilder();

            var length = arg.Length;
            var needsQuotes = length == 0 || ShouldSurroundWithQuotes(arg);
            var isQuoted = needsQuotes || IsSurroundedWithQuotes(arg);

            if (needsQuotes) sb.Append("\"");

            for (int i = 0; i < length; ++i)
            {
                var backslashCount = 0;

                // Consume All Backslashes
                while (i < arg.Length && arg[i] == '\\')
                {
                    backslashCount++;
                    i++;
                }

                // Escape any backslashes at the end of the arg
                // when the argument is also quoted.
                // This ensures the outside quote is interpreted as
                // an argument delimiter
                if (i == arg.Length && isQuoted)
                {
                    sb.Append('\\', 2 * backslashCount);
                }

                // At then end of the arg, which isn't quoted,
                // just add the backslashes, no need to escape
                else if (i == arg.Length)
                {
                    sb.Append('\\', backslashCount);
                }

                // Escape any preceding backslashes and the quote
                else if (arg[i] == '"')
                {
                    sb.Append('\\', (2 * backslashCount) + 1);
                    sb.Append('"');
                }

                // Output any consumed backslashes and the character
                else
                {
                    sb.Append('\\', backslashCount);
                    sb.Append(arg[i]);
                }
            }
            
            if (needsQuotes) sb.Append("\"");

            return sb.ToString();
        }

        /// <summary>
        /// Prepare as single argument to 
        /// roundtrip properly through cmd.
        /// 
        /// This prefixes every character with the '^' character to force cmd to
        /// interpret the argument string literally. An alternative option would 
        /// be to do this only for cmd metacharacters.
        /// 
        /// See here for more info:
        /// https://docs.microsoft.com/archive/blogs/twistylittlepassagesallalike/everyone-quotes-command-line-arguments-the-wrong-way
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static string EscapeArgForCmd(string argument)
        {
            var sb = new StringBuilder();

            var quoted = ShouldSurroundWithQuotes(argument);

            if (quoted) sb.Append("^\"");

            // Prepend every character with ^
            // This is harmless when passing through cmd
            // and ensures cmd metacharacters are not interpreted
            // as such
            foreach (var character in argument)
            {
                sb.Append("^");
                sb.Append(character);
            }

            if (quoted) sb.Append("^\"");

            return sb.ToString();
        }

        internal static bool ShouldSurroundWithQuotes(string argument)
        {
            // Only quote if whitespace exists in the string
            return ArgumentContainsWhitespace(argument);
        }

        internal static bool IsSurroundedWithQuotes(string argument)
        {
            return argument.StartsWith("\"", StringComparison.Ordinal) &&
                   argument.EndsWith("\"", StringComparison.Ordinal);
        }

        internal static bool ArgumentContainsWhitespace(string argument)
        {
            return argument.Contains(" ") || argument.Contains("\t") || argument.Contains("\n");
        }
    }
}
