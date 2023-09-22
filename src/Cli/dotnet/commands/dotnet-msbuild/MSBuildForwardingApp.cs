// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.MSBuild
{
    public class MSBuildForwardingApp
    {
        internal const string TelemetrySessionIdEnvironmentVariableName = "DOTNET_CLI_TELEMETRY_SESSIONID";

        private MSBuildForwardingAppWithoutLogging _forwardingAppWithoutLogging;

        private static IEnumerable<string> ConcatTelemetryLogger(IEnumerable<string> argsToForward)
        {
            if (Telemetry.CurrentSessionId != null)
            {
                try
                {
                    Type loggerType = typeof(MSBuildLogger);
                    Type forwardingLoggerType = typeof(MSBuildForwardingLogger);

                    return argsToForward
                        .Concat(new[]
                        {
                            $"-distributedlogger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}*{forwardingLoggerType.FullName},{forwardingLoggerType.GetTypeInfo().Assembly.Location}"
                        });
                }
                catch (Exception)
                {
                    // Exceptions during telemetry shouldn't cause anything else to fail
                }
            }
            return argsToForward;
        }

        public MSBuildForwardingApp(IEnumerable<string> argsToForward, string msbuildPath = null)
        {
            _forwardingAppWithoutLogging = new MSBuildForwardingAppWithoutLogging(
                ConcatTelemetryLogger(argsToForward),
                msbuildPath);

            // Add the performance log location to the environment of the target process.
            if (PerformanceLogManager.Instance != null && !string.IsNullOrEmpty(PerformanceLogManager.Instance.CurrentLogDirectory))
            {
                EnvironmentVariable(PerformanceLogManager.PerfLogDirEnvVar, PerformanceLogManager.Instance.CurrentLogDirectory);
            }
        }

        public IEnumerable<string> MSBuildArguments { get { return _forwardingAppWithoutLogging.GetAllArguments(); } }

        public void EnvironmentVariable(string name, string value)
        {
            _forwardingAppWithoutLogging.EnvironmentVariable(name, value);
        }

        public ProcessStartInfo GetProcessStartInfo()
        {
            InitializeRequiredEnvironmentVariables();

            return _forwardingAppWithoutLogging.GetProcessStartInfo();
        }

        private void InitializeRequiredEnvironmentVariables()
        {
            EnvironmentVariable(TelemetrySessionIdEnvironmentVariableName, Telemetry.CurrentSessionId);
        }

        /// <summary>
        /// Test hook returning concatenated and escaped command line arguments that would be passed to MSBuild.
        /// </summary>
        internal string GetArgumentsToMSBuild()
        {
            var argumentsUnescaped = _forwardingAppWithoutLogging.GetAllArguments();
            return ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(argumentsUnescaped);
        }

        public virtual int Execute()
        {
            // Ignore Ctrl-C for the remainder of the command's execution
            // Forwarding commands will just spawn the child process and exit
            Console.CancelKeyPress += (sender, e) => { e.Cancel = true; };

            int exitCode;
            if (_forwardingAppWithoutLogging.ExecuteMSBuildOutOfProc)
            {
                ProcessStartInfo startInfo = GetProcessStartInfo();

                PerformanceLogEventSource.Log.LogMSBuildStart(startInfo.FileName, startInfo.Arguments);
                exitCode = startInfo.Execute();
                PerformanceLogEventSource.Log.MSBuildStop(exitCode);
            }
            else
            {
                InitializeRequiredEnvironmentVariables();
                string[] arguments = _forwardingAppWithoutLogging.GetAllArguments();
                if (PerformanceLogEventSource.Log.IsEnabled())
                {
                    PerformanceLogEventSource.Log.LogMSBuildStart(
                        _forwardingAppWithoutLogging.MSBuildPath,
                        ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(arguments));
                }
                exitCode = _forwardingAppWithoutLogging.ExecuteInProc(arguments);
                PerformanceLogEventSource.Log.MSBuildStop(exitCode);
            }

            return exitCode;
        }
    }
}
