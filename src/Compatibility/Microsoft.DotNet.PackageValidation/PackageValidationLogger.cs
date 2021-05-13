// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using NuGet.Common;

namespace Microsoft.DotNet.PackageValidation
{
    public class PackageValidationLogger : ILogger
    {
        public void Log(LogLevel level, string data)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    LogDebug(data);
                    break;
                case LogLevel.Error:
                    LogError(data);
                    break;
                case LogLevel.Information:
                    LogInformation(data);
                    break;
                case LogLevel.Minimal:
                    LogMinimal(data);
                    break;
                case LogLevel.Verbose:
                    LogVerbose(data);
                    break;
                case LogLevel.Warning:
                    LogWarning(data);
                    break;
            }
        }

        public void Log(ILogMessage message)
        {
            Log(message.Level, message.Message);
        }

        public Task LogAsync(LogLevel level, string data)
        {
            Log(level, data);
            return Task.FromResult(0);
        }

        public Task LogAsync(ILogMessage message)
        {
            Log(message);
            return Task.FromResult(0);
        }

        public void LogDebug(string data)
        {
            Console.WriteLine($"[DEBUG] {data}");
        }

        public void LogError(string data)
        {
            Console.WriteLine($"[Error] {data}");
        }

        public void LogInformation(string data)
        {
            Console.WriteLine($"[Info] {data}");
        }

        public void LogInformationSummary(string data)
        {
            Console.WriteLine($"[Info Summary] {data}");
        }

        public void LogMinimal(string data)
        {
            Console.WriteLine($"{data}");
        }

        public void LogVerbose(string data)
        {
            Console.WriteLine($"[Verbose] {data}");
        }

        public void LogWarning(string data)
        {
            Console.WriteLine($"[Warning] {data}");
        }
    }
}
