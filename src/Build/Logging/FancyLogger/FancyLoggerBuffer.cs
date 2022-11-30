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
    internal static class FancyLoggerBuffer
    {
        private static int Height = 0;
        public static void Initialize()
        {
            // Setup event listeners
            var arrowsPressTask = Task.Run(() =>
            {
                while (true)
                {
                    switch (Console.ReadKey().Key)
                    {
                        case ConsoleKey.UpArrow:
                            ScrollUp();
                            break;
                        case ConsoleKey.DownArrow:
                            ScrollDown();
                            break;
                    }
                }
            });
            // Switch to alternate buffer
            Console.Write(ANSIBuilder.Buffer.UseAlternateBuffer());
            // Update dimensions
            Height = Console.BufferHeight;
            // Write body
            Console.Write(""
                + ANSIBuilder.Cursor.Position(2, 0)
                + ANSIBuilder.Formatting.Bold("FancyLogger") + " will be shown here..."
                + "\n"
                + ANSIBuilder.Formatting.Dim("5s sleep for demo purposes")
            );
            // Write "title"
            Console.Write(""
                + ANSIBuilder.Cursor.Home()
                + ANSIBuilder.Formatting.Inverse("                         MSBuild                         ")
            );

            // Write "footer"
            Console.Write(""
                + ANSIBuilder.Cursor.Position(Height - 1, 0)
                + "---------------------------------------------------------"
                + "\n"
                + "Build: 13%"
            );
        }

        private static void ScrollUp()
        {
            Console.WriteLine("Scroll up");
        }

        private static void ScrollDown()
        {
            Console.WriteLine("Scroll down");
        }
    }
}
