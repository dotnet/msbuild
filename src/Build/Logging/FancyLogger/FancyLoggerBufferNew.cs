// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Logging.FancyLogger
{
    internal class FancyLoggerBufferNew
    {
        public class FancyLoggerBufferLineNew
        {
            private static int Counter = 0;
            public int Id;
            public string Text;
            public FancyLoggerBufferLineNew? NextLine;

            private string _fullText;
            public string FullText
            {
                get => _fullText;
                set
                {
                    // Assign value
                    _fullText = value;
                    // If next line(s) exists, delete
                    if (NextLine is not null)
                    {
                        NextLine.DeleteNextLines();
                        NextLine = null;
                    }
                    // If text overflows
                    // TODO: Can be simplified
                    if (value.Length > Console.WindowWidth)
                    {
                        // Get text breakpoint
                        // TODO: Fix ANSI bugs
                        int breakpoint = ANSIBuilder.ANSIBreakpoint(value, Console.BufferWidth);
                        Text = value.Substring(0, breakpoint);
                        if (breakpoint + 1 < value.Length) NextLine = new FancyLoggerBufferLineNew(value.Substring(breakpoint + 1));
                    }
                    else
                    {
                        Text = value;
                    }
                }
            }

            public FancyLoggerBufferLineNew()
            {
                Id = Counter++;
                Text = string.Empty;
                _fullText = string.Empty;
            }

            public FancyLoggerBufferLineNew(string text) : this()
            {
                FullText = text;
            }

            public int EndId()
            {
                if (NextLine is null) return Id;
                return NextLine.EndId();
            }

            public void WriteAfterId(int id)
            {
            }
            public void Update()
            {
            }

            public void DeleteNextLines()
            {
            }
        }
    }
}
