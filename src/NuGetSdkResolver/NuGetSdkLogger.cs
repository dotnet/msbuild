// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using NuGet.Common;
using System.Collections.Generic;
using System.Threading.Tasks;

using INuGetLogger = NuGet.Common.ILogger;
using SdkLoggerBase = Microsoft.Build.Framework.SdkLogger;

namespace NuGet.MSBuildSdkResolver
{
    /// <summary>
    /// An implementation of <see cref="T:NuGet.Common.ILogger" /> that logs messages to an <see cref="T:Microsoft.Build.Framework.SdkLogger" />.
    /// </summary>
    /// <inheritdoc />
    internal class NuGetSdkLogger : INuGetLogger
    {
        /// <summary>
        /// A collection of errors that have been logged.
        /// </summary>
        private readonly ICollection<string> _errors;

        /// <summary>
        /// A <see cref="SdkLogger"/> to forward events to.
        /// </summary>
        private readonly SdkLoggerBase _sdkLogger;

        /// <summary>
        /// A collection of warnings that have been logged.
        /// </summary>
        private readonly ICollection<string> _warnings;

        /// <summary>
        /// Initializes a new instance of the NuGetLogger class.
        /// </summary>
        /// <param name="sdkLogger">A <see cref="SdkLogger"/> to forward events to.</param>
        /// <param name="warnings">A <see cref="ICollection{String}"/> to add logged warnings to.</param>
        /// <param name="errors">An <see cref="ICollection{String}"/> to add logged errors to.</param>
        public NuGetSdkLogger(SdkLoggerBase sdkLogger, ICollection<string> warnings, ICollection<string> errors)
        {
            ErrorUtilities.VerifyThrowArgumentNull(sdkLogger, nameof(sdkLogger));
            ErrorUtilities.VerifyThrowArgumentNull(warnings, nameof(warnings));
            ErrorUtilities.VerifyThrowArgumentNull(errors, nameof(errors));

            _sdkLogger = sdkLogger;
            _warnings = warnings;
            _errors = errors;
        }

        public void Log(LogLevel level, string data)
        {
            switch (level)
            {
                case LogLevel.Debug:
                case LogLevel.Minimal:
                case LogLevel.Verbose:
                case LogLevel.Information:
                    // ReSharper disable once RedundantArgumentDefaultValue
                    _sdkLogger.LogMessage(data, MessageImportance.Low);
                    break;

                case LogLevel.Warning:
                    _warnings.Add(data);
                    break;

                case LogLevel.Error:
                    _errors.Add(data);
                    break;
            }
        }

        public void Log(ILogMessage message) => Log(message.Level, message.Message);

        public Task LogAsync(LogLevel level, string data)
        {
            Log(level, data);

            return Task.CompletedTask;
        }

        public Task LogAsync(ILogMessage message)
        {
            Log(message);

            return Task.CompletedTask;
        }

        public void LogDebug(string data) => Log(LogLevel.Debug, data);

        public void LogError(string data) => Log(LogLevel.Error, data);

        public void LogInformation(string data) => Log(LogLevel.Information, data);

        public void LogInformationSummary(string data) => Log(LogLevel.Information, data);

        public void LogMinimal(string data) => Log(LogLevel.Minimal, data);

        public void LogVerbose(string data) => Log(LogLevel.Verbose, data);

        public void LogWarning(string data) => Log(LogLevel.Warning, data);
    }
}
