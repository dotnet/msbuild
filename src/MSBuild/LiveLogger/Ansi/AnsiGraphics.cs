// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging.Ansi
{
    internal sealed class AnsiGraphics
    {
        private static int s_spinnerCounter = 0;

        public string Spinner()
        {
            return Spinner(s_spinnerCounter++);
        }

        public string Spinner(int n)
        {
            char[] chars = { '\\', '|', '/', '-' };
            return chars[n % (chars.Length - 1)].ToString();
        }

        public string ProgressBar(float percentage, int width = 10, char completedChar = '█', char remainingChar = '░')
        {
            string result = string.Empty;
            for (int i = 0; i < (int)Math.Floor(width * percentage); i++)
            {
                result += completedChar;
            }

            for (int i = (int)Math.Floor(width * percentage); i < width; i++)
            {
                result += remainingChar;
            }

            return result;
        }

        public string Bell() => string.Format("\x07");
    }
}
