// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.Cli.Utils
{
    public class PerfTraceOutput
    {
        private static TimeSpan _minDuration = TimeSpan.FromSeconds(0.001);

        public static void Print(Reporter reporter, IEnumerable<PerfTraceThreadContext> contexts)
        {
            foreach (var threadContext in contexts)
            {
                Print(reporter, new[] { threadContext.Root }, threadContext.Root, null);
            }
        }

        private static void Print(Reporter reporter, IEnumerable<PerfTraceEvent> events, PerfTraceEvent root, PerfTraceEvent parent, int padding = 0)
        {
            foreach (var e in events)
            {
                if (e.Duration < _minDuration)
                {
                    continue;
                }
                reporter.Write(new string(' ', padding));
                reporter.WriteLine(FormatEvent(e, root, parent));
                Print(reporter, e.Children, root, e, padding + 2);
            }
        }

        private static string FormatEvent(PerfTraceEvent e, PerfTraceEvent root, PerfTraceEvent parent)
        {
            var builder = new StringBuilder();
            FormatEventTimeStat(builder, e, root, parent);
            builder.Append($" {e.Type.Bold()} {e.Instance}");
            return builder.ToString();
        }

        private static void FormatEventTimeStat(StringBuilder builder, PerfTraceEvent e, PerfTraceEvent root, PerfTraceEvent parent)
        {
            builder.Append("[");
            if (root != e)
            {
                AppendTime(builder, e.Duration.TotalSeconds / root.Duration.TotalSeconds, 0.2);
            }
            AppendTime(builder, e.Duration.TotalSeconds / parent?.Duration.TotalSeconds, 0.5);
            builder.Append($"{e.Duration.ToString("ss\\.fff\\s").Blue()}]");
        }

        private static void AppendTime(StringBuilder builder, double? percent, double threshold)
        {
            if (percent != null)
            {
                var formattedPercent = $"{percent*100:00\\.00%}";
                if (percent > threshold)
                {
                    builder.Append(formattedPercent.Red());
                }
                else if (percent > threshold / 2)
                {
                    builder.Append(formattedPercent.Yellow());
                }
                else if (percent > threshold / 5)
                {
                    builder.Append(formattedPercent.Green());
                }
                else
                {
                    builder.Append(formattedPercent);
                }
                builder.Append(" ");
            }
        }
    }
}
