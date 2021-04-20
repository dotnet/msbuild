// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Cli
{
    internal static class AnsiColorExtensions
    {
        internal static string Black(this string text)
        {
            return "\x1B[30m" + text + "\x1B[39m";
        }

        internal static string Red(this string text)
        {
            return "\x1B[31m" + text + "\x1B[39m";
        }
        internal static string Green(this string text)
        {
            return "\x1B[32m" + text + "\x1B[39m";
        }

        internal static string Yellow(this string text)
        {
            return "\x1B[33m" + text + "\x1B[39m";
        }

        internal static string Blue(this string text)
        {
            return "\x1B[34m" + text + "\x1B[39m";
        }

        internal static string Magenta(this string text)
        {
            return "\x1B[35m" + text + "\x1B[39m";
        }

        internal static string Cyan(this string text)
        {
            return "\x1B[36m" + text + "\x1B[39m";
        }

        internal static string White(this string text)
        {
            return "\x1B[37m" + text + "\x1B[39m";
        }

        internal static string Bold(this string text)
        {
            return "\x1B[1m" + text + "\x1B[22m";
        }
    }
}
