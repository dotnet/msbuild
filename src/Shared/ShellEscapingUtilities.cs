// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
#else
using System.Text;
#endif

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class implements static methods to assist with escaping arguments for shell invocation.
    /// Provides platform-specific escaping for Windows cmd.exe and Unix sh.
    /// </summary>
    internal static class ShellEscapingUtilities
    {
#if NET
        // Characters that require quoting in Windows cmd.exe
        private static readonly char[] s_windowsSpecialChars = [' ', '\t', '"', '&', '|', '<', '>', '^', '%', '(', ')', '!', '=', ';', ','];
#endif

        /// <summary>
        /// Escapes an argument for Windows cmd.exe.
        /// </summary>
        /// <param name="argument">The argument to escape.</param>
        /// <returns>The escaped argument ready for cmd.exe invocation.</returns>
        internal static string EscapeArgumentForWindows(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return "\"\"";
            }

#if NET
            return EscapeArgumentForWindowsCore(argument);
#else
            return EscapeArgumentForWindowsFramework(argument);
#endif
        }

#if NET
        /// <summary>
        /// Modern .NET span-based implementation for Windows escaping.
        /// </summary>
        private static string EscapeArgumentForWindowsCore(string argument)
        {
            ReadOnlySpan<char> argSpan = argument.AsSpan();
            
            // Fast path: check if any special characters exist
            bool needsQuoting = argSpan.IndexOfAny(s_windowsSpecialChars.AsSpan()) >= 0;
            
            if (!needsQuoting)
            {
                return argument;
            }

            // Find all indexes of characters that need escaping (doubled)
            int doubleQuoteCount = argSpan.Count('"');
            int percentCount = argSpan.Count('%');
            
            // Calculate required capacity: original length + 2 quotes + extra chars for doubling
            int extraChars = 2 + doubleQuoteCount + percentCount;

            // Use string.Create for efficient string building
            return string.Create(argument.Length + extraChars, argument, (span, arg) =>
            {
                int pos = 0;
                span[pos++] = '"';

                ReadOnlySpan<char> remaining = arg.AsSpan();
                int nextQuote = remaining.IndexOfAny('"', '%');
                
                while (nextQuote >= 0)
                {
                    // Copy everything before the special char
                    if (nextQuote > 0)
                    {
                        remaining.Slice(0, nextQuote).CopyTo(span.Slice(pos));
                        pos += nextQuote;
                    }
                    
                    // Double the special character
                    char specialChar = remaining[nextQuote];
                    span[pos++] = specialChar;
                    span[pos++] = specialChar;
                    
                    // Move to next segment
                    remaining = remaining.Slice(nextQuote + 1);
                    nextQuote = remaining.IndexOfAny('"', '%');
                }
                
                // Copy remaining characters
                if (remaining.Length > 0)
                {
                    remaining.CopyTo(span.Slice(pos));
                    pos += remaining.Length;
                }

                span[pos] = '"';
            });
        }
#endif

#if !NET
        /// <summary>
        /// .NET Framework implementation for Windows escaping.
        /// </summary>
        private static string EscapeArgumentForWindowsFramework(string argument)
        {
            // Characters that require quoting in Windows cmd.exe
            bool needsQuoting = false;
            
            // Check if the argument contains special characters that need quoting
            foreach (char c in argument)
            {
                if (c == ' ' || c == '\t' || c == '"' || c == '&' || c == '|' || 
                    c == '<' || c == '>' || c == '^' || c == '%' || c == '(' || 
                    c == ')' || c == '!' || c == '=' || c == ';' || c == ',')
                {
                    needsQuoting = true;
                    break;
                }
            }

            if (!needsQuoting)
            {
                return argument;
            }

            // Pre-calculate capacity to minimize allocations
            int capacity = argument.Length + 2; // quotes
            foreach (char c in argument)
            {
                if (c == '"' || c == '%')
                {
                    capacity++; // Need to double these
                }
            }

            StringBuilder escaped = new StringBuilder(capacity);
            escaped.Append('"');

            foreach (char c in argument)
            {
                if (c == '"')
                {
                    escaped.Append("\"\"");
                }
                else if (c == '%')
                {
                    escaped.Append("%%");
                }
                else
                {
                    escaped.Append(c);
                }
            }

            escaped.Append('"');
            return escaped.ToString();
        }
#endif

        /// <summary>
        /// Escapes an argument for Unix sh.
        /// </summary>
        /// <param name="argument">The argument to escape.</param>
        /// <returns>The escaped argument ready for sh invocation.</returns>
        internal static string EscapeArgumentForUnix(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return "''";
            }

#if NET
            return EscapeArgumentForUnixCore(argument);
#else
            return EscapeArgumentForUnixFramework(argument);
#endif
        }

#if NET
        /// <summary>
        /// Modern .NET span-based implementation for Unix escaping.
        /// </summary>
        private static string EscapeArgumentForUnixCore(string argument)
        {
            ReadOnlySpan<char> argSpan = argument.AsSpan();
            
            // Fast path: no single quotes
            int singleQuoteIndex = argSpan.IndexOf('\'');
            if (singleQuoteIndex < 0)
            {
                // Use string.Create for efficient concatenation
                return string.Create(argument.Length + 2, argument, (span, arg) =>
                {
                    span[0] = '\'';
                    arg.AsSpan().CopyTo(span.Slice(1));
                    span[^1] = '\'';
                });
            }

            // Count single quotes using Count for efficiency
            int singleQuoteCount = argSpan.Count('\'');

            // Each single quote becomes '\'' (4 chars), plus opening and closing quotes
            int capacity = argument.Length + 2 + (singleQuoteCount * 3);

            return string.Create(capacity, argument, (span, arg) =>
            {
                int pos = 0;
                span[pos++] = '\'';

                ReadOnlySpan<char> remaining = arg.AsSpan();
                int nextQuote = remaining.IndexOf('\'');
                
                while (nextQuote >= 0)
                {
                    // Copy everything before the quote
                    if (nextQuote > 0)
                    {
                        remaining.Slice(0, nextQuote).CopyTo(span.Slice(pos));
                        pos += nextQuote;
                    }
                    
                    // Replace ' with '\''
                    span[pos++] = '\'';
                    span[pos++] = '\\';
                    span[pos++] = '\'';
                    span[pos++] = '\'';
                    
                    // Move to next segment
                    remaining = remaining.Slice(nextQuote + 1);
                    nextQuote = remaining.IndexOf('\'');
                }
                
                // Copy remaining characters
                if (remaining.Length > 0)
                {
                    remaining.CopyTo(span.Slice(pos));
                    pos += remaining.Length;
                }

                span[pos] = '\'';
            });
        }
#endif

#if !NET
        /// <summary>
        /// .NET Framework implementation for Unix escaping.
        /// </summary>
        private static string EscapeArgumentForUnixFramework(string argument)
        {
            // Fast path: no single quotes
            if (argument.IndexOf('\'') == -1)
            {
                return "'" + argument + "'";
            }

            // Count single quotes for capacity calculation
            int singleQuoteCount = 0;
            foreach (char c in argument)
            {
                if (c == '\'')
                {
                    singleQuoteCount++;
                }
            }

            // Pre-calculate capacity: each ' becomes '\'' (4 chars total, 3 extra)
            int capacity = argument.Length + 2 + (singleQuoteCount * 3);
            StringBuilder escaped = new StringBuilder(capacity);
            escaped.Append('\'');

            foreach (char c in argument)
            {
                if (c == '\'')
                {
                    // End current quote, add escaped quote, start new quote
                    escaped.Append("'\\''");
                }
                else
                {
                    escaped.Append(c);
                }
            }

            escaped.Append('\'');
            return escaped.ToString();
        }
#endif
    }
}
