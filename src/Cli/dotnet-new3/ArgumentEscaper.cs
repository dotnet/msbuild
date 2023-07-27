// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Dotnet_new3
{
    internal static class ArgumentEscaper
    {
        /// <summary>
        /// Undo the processing which took place to create string[] args in Main,
        /// so that the next process will receive the same string[] args
        ///
        /// See here for more info:
        /// http://blogs.msdn.com/b/twistylittlepassagesallalike/archive/2011/04/23/everyone-quotes-arguments-the-wrong-way.aspx.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        internal static string EscapeAndConcatenateArgArrayForProcessStart(IEnumerable<string> args)
        {
            return string.Join(" ", EscapeArgArray(args));
        }

        /// <summary>
        /// Undo the processing which took place to create string[] args in Main,
        /// so that the next process will receive the same string[] args
        ///
        /// See here for more info:
        /// http://blogs.msdn.com/b/twistylittlepassagesallalike/archive/2011/04/23/everyone-quotes-arguments-the-wrong-way.aspx.
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

        private static string EscapeSingleArg(string arg)
        {
            var sb = new StringBuilder();

            var needsQuotes = ShouldSurroundWithQuotes(arg);
            var isQuoted = needsQuotes || IsSurroundedWithQuotes(arg);

            if (needsQuotes)
            {
                sb.Append('"');
            }

            for (int i = 0; i < arg.Length; ++i)
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

            if (needsQuotes)
            {
                sb.Append('"');
            }

            return sb.ToString();
        }

        private static bool ShouldSurroundWithQuotes(string argument)
        {
            // Don't quote already quoted strings
            if (IsSurroundedWithQuotes(argument))
            {
                return false;
            }

            // Only quote if whitespace exists in the string
            return ArgumentContainsWhitespace(argument);
        }

        private static bool IsSurroundedWithQuotes(string argument)
        {
            return argument.StartsWith("\"", StringComparison.Ordinal) &&
                   argument.EndsWith("\"", StringComparison.Ordinal);
        }

        private static bool ArgumentContainsWhitespace(string argument)
        {
            return argument.IndexOfAny(new[] { ' ', '\t', '\n' }) > -1;
        }
    }
}
