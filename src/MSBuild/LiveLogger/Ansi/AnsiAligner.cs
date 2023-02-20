// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging.Ansi
{
    internal sealed class AnsiAligner
    {
        private readonly Func<string, string> _remover;

        internal AnsiAligner(Func<string, string> func) => _remover = func;

        internal string Center(string text)
        {
            string result = string.Empty;
            string noFormatString = _remover(text);
            if (noFormatString.Length > Console.BufferWidth)
            {
                return text;
            }

            int space = (Console.BufferWidth - noFormatString.Length) / 2;
            result += new string(' ', space);
            result += text;
            result += new string(' ', space);
            return result;
        }

        internal string Right(string text)
        {
            string result = String.Empty;
            string noFormatString = _remover(text);
            if (noFormatString.Length > Console.BufferWidth)
            {
                return text;
            }

            int space = Console.BufferWidth - noFormatString.Length;
            result += new string(' ', space);
            result += text;
            return result;
        }

        internal string Left(string text)
        {
            string result = string.Empty;
            string noFormatString = _remover(text);
            if (noFormatString.Length > Console.BufferWidth)
            {
                return text;
            }

            int space = Console.BufferWidth - noFormatString.Length;
            result += text;
            result += new string(' ', space);
            return result;
        }

        internal string SpaceBetween(string leftText, string rightText, int width)
        {
            string result = String.Empty;
            string leftNoFormatString = _remover(leftText);
            string rightNoFormatString = _remover(rightText);
            if (leftNoFormatString.Length + rightNoFormatString.Length >= width)
            {
                return leftText + rightText;
            }

            int space = width - (leftNoFormatString.Length + rightNoFormatString.Length);
            result += leftText;
            result += new string(' ', space - 1);
            result += rightText;
            return result;
        }
    }
}
