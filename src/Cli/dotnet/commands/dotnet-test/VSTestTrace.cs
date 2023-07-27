// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Test
{
    internal class VSTestTrace
    {
        public static bool TraceEnabled { get; private set; }
        private static readonly string s_traceFilePath;

        static VSTestTrace()
        {
            TraceEnabled = int.TryParse(Environment.GetEnvironmentVariable("DOTNET_CLI_VSTEST_TRACE"), out int enabled) && enabled == 1;
            s_traceFilePath = Environment.GetEnvironmentVariable("DOTNET_CLI_VSTEST_TRACEFILE");
            if (TraceEnabled)
            {
                Console.WriteLine($"[dotnet test - {DateTime.UtcNow}]Logging to {(!string.IsNullOrEmpty(s_traceFilePath) ? s_traceFilePath : "console")}");
            }
        }

        public static void SafeWriteTrace(Func<string> messageLog)
        {
            if (!TraceEnabled)
            {
                return;
            }

            try
            {
                string message = $"[dotnet test - {DateTimeOffset.UtcNow}]{messageLog()}";
                if (!string.IsNullOrEmpty(s_traceFilePath))
                {
                    lock (s_traceFilePath)
                    {
                        using StreamWriter logFile = File.AppendText(s_traceFilePath);
                        logFile.WriteLine(message);
                    }
                }
                else
                {
                    Console.WriteLine(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[dotnet test - {DateTimeOffset.UtcNow}]{ex}");
            }
        }
    }
}
