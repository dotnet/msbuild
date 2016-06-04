using System;

namespace Microsoft.DotNet.Archive
{
    public struct ProgressReport
    {
        public string Phase;
        public long Ticks;
        public long Total;
    }

    public static class ProgressReportExtensions
    {
        public static void Report(this IProgress<ProgressReport> progress, string phase, long ticks, long total)
        {
            progress.Report(new ProgressReport()
            {
                Phase = phase,
                Ticks = ticks,
                Total = total
            });
        }
    }

}
