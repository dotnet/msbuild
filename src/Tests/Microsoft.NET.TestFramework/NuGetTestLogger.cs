// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using NuGet.Common;

namespace Microsoft.NET.TestFramework
{
    public class NuGetTestLogger : ILogger
    {
        private readonly ITestOutputHelper _output;

        public NuGetTestLogger()
        {
        }

        public NuGetTestLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Logged messages
        /// </summary>
        public ConcurrentQueue<string> Messages { get; } = new ConcurrentQueue<string>();
        public ConcurrentQueue<string> DebugMessages { get; } = new ConcurrentQueue<string>();
        public ConcurrentQueue<string> VerboseMessages { get; } = new ConcurrentQueue<string>();
        public ConcurrentQueue<string> MinimalMessages { get; } = new ConcurrentQueue<string>();
        public ConcurrentQueue<string> InformationMessages { get; } = new ConcurrentQueue<string>();
        public ConcurrentQueue<string> ErrorMessages { get; } = new ConcurrentQueue<string>();
        public ConcurrentQueue<string> WarningMessages { get; } = new ConcurrentQueue<string>();

        public ConcurrentQueue<ILogMessage> LogMessages { get; } = new ConcurrentQueue<ILogMessage>();

        public int Errors { get; set; }

        public int Warnings { get; set; }

        public void LogDebug(string data)
        {
            Messages.Enqueue(data);
            DebugMessages.Enqueue(data);
            DumpMessage("DEBUG", data);
        }

        public void LogError(string data)
        {
            Errors++;
            Messages.Enqueue(data);
            ErrorMessages.Enqueue(data);
            DumpMessage("ERROR", data);
        }

        public void LogInformation(string data)
        {
            Messages.Enqueue(data);
            InformationMessages.Enqueue(data);
            DumpMessage("INFO ", data);
        }

        public void LogMinimal(string data)
        {
            Messages.Enqueue(data);
            MinimalMessages.Enqueue(data);
            DumpMessage("LOG  ", data);
        }

        public void LogVerbose(string data)
        {
            Messages.Enqueue(data);
            VerboseMessages.Enqueue(data);
            DumpMessage("TRACE", data);
        }

        public void LogWarning(string data)
        {
            Warnings++;
            Messages.Enqueue(data);
            WarningMessages.Enqueue(data);
            DumpMessage("WARN ", data);
        }

        public void LogInformationSummary(string data)
        {
            Messages.Enqueue(data);
            DumpMessage("ISMRY", data);
        }

        private void DumpMessage(string level, string data)
        {
            // NOTE(anurse): Uncomment this to help when debugging tests
            //Console.WriteLine($"{level}: {data}");
            _output?.WriteLine($"{level}: {data}");
        }

        public void Clear()
        {
            string msg;
            while (Messages.TryDequeue(out msg))
            {
                // do nothing
            }
        }

        public string ShowErrors()
        {
            return string.Join(Environment.NewLine, ErrorMessages);
        }

        public string ShowWarnings()
        {
            return string.Join(Environment.NewLine, WarningMessages);
        }

        public string ShowMessages()
        {
            return string.Join(Environment.NewLine, Messages);
        }

        public void Log(LogLevel level, string data)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    {
                        LogDebug(data);
                        break;
                    }

                case LogLevel.Error:
                    {
                        LogError(data);
                        break;
                    }

                case LogLevel.Information:
                    {
                        LogInformation(data);
                        break;
                    }

                case LogLevel.Minimal:
                    {
                        LogMinimal(data);
                        break;
                    }

                case LogLevel.Verbose:
                    {
                        LogVerbose(data);
                        break;
                    }

                case LogLevel.Warning:
                    {
                        LogWarning(data);
                        break;
                    }
            }
        }

        public Task LogAsync(LogLevel level, string data)
        {
            Log(level, data);

            return Task.FromResult(0);
        }

        public void Log(ILogMessage message)
        {
            LogMessages.Enqueue(message);

            Log(message.Level, message.Message);
        }

        public async Task LogAsync(ILogMessage message)
        {
            LogMessages.Enqueue(message);
            await LogAsync(message.Level, message.FormatWithCode());
        }
    }
}
