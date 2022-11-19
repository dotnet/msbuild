// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;

namespace Microsoft.Build.Logging.FancyLogger
{
    public enum ANSIForegroundColor
    {
        None = 0,
        Red = 31,
        Green = 32,
        Yellow = 33,
        Blue = 34,
        Magenta = 35,
        Cyan = 36,
        White = 37
    }

    public enum ANSIBackgroundColor
    {
        None = 0,
        Red = 41,
        Green = 42,
        Yellow = 43,
        Blue = 44,
        Magenta = 45,
        Cyan = 46,
        White = 47
    }

    public static class ANSIBuilder
    {
        public static class Formatting
        {
            public static string Bold(string text)
            {
                return String.Format("\x1b[1m{0}\x1b[22m", text);
            }
            public static string Dim(string text)
            {
                return String.Format("\x1b[2m{0}\x1b[22m", text);
            }
            public static string Italics(string text)
            {
                return String.Format("\x1b[3m{0}\x1b[23m", text);
            }
            public static string Underlined(string text)
            {
                return String.Format("\x1b[4m{0}\x1b[24m", text);
            }
            public static string Blinking(string text)
            {
                return String.Format("\x1b[5m{0}\x1b[25m", text);
            }
            public static string Strikethrough(string text)
            {
                return String.Format("\x1b[9m{0}\x1b[29m", text);
            }
            public static string Color(string text, ANSIBackgroundColor color) {
                return String.Format("\x1b[{0}m{1}\x1b[0m", (int)color, text);
            }
            public static string Color(string text, ANSIForegroundColor color)
            {
                return String.Format("\x1b[{0}m{1}\x1b[0m", (int)color, text);
            }
        }
        public static class Cursor
        {
            public static string GoToPosition(int row, int column)
            {
                return String.Format("\x1b[{0};{1}H", row, column);
            }
        }
        public static class Eraser
        {
            public static string EraseCurrentLine()
            {
                return "\x1b[2K";
            }
        }
        public static class Graphics
        {
            public static int loadingCounter = 0;
            public static string ProgressBar(float percentage, int width = 10, char completedChar = '█', char remainingChar = '░')
            {
                string result = "";
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

            public static string Loading()
            {
                char[] chars = { '⠄', '⠆', '⠇', '⠋', '⠙', '⠸', '⠰', '⠠', '⠰', '⠸', '⠙', '⠋', '⠇', '⠆' };
                loadingCounter += (loadingCounter++) % (chars.Length - 1);

                return chars[loadingCounter].ToString();
            }
        }
    }
}
