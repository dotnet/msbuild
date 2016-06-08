// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;

namespace Microsoft.DotNet.Archive
{
    public class ConsoleProgressReport : IProgress<ProgressReport>
    {
        string currentPhase;
        int lastLineLength = 0;
        double lastProgress = -1;
        Stopwatch stopwatch;

        public void Report(ProgressReport value)
        {
            long progress = (long)(100 * ((double)value.Ticks / value.Total));

            if (progress == lastProgress && value.Phase == currentPhase)
            {
                return;
            }
            lastProgress = progress;

            lock (this)
            {
                string line = $"{value.Phase} {progress}%";
                if (value.Phase == currentPhase)
                {
                    Console.Write(new string('\b', lastLineLength));

                    Console.Write(line);
                    lastLineLength = line.Length;

                    if (progress == 100)
                    {
                        Console.WriteLine($" {stopwatch.ElapsedMilliseconds} ms");
                    }
                }
                else
                {
                    Console.Write(line);
                    currentPhase = value.Phase;
                    lastLineLength = line.Length;
                    stopwatch = Stopwatch.StartNew();
                }
            }
        }
    }
}