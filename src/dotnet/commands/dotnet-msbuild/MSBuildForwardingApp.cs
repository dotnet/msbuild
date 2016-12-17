// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Tools.MSBuild
{
    public class MSBuildForwardingApp
    {
        internal const string TelemetrySessionIdEnvironmentVariableName = "DOTNET_CLI_TELEMETRY_SESSIONID";

        private const string MSBuildExeName = "MSBuild.dll";

        private const string SdksDirectoryName = "Sdks";

        private readonly ForwardingApp _forwardingApp;

        private readonly Dictionary<string, string> _msbuildRequiredEnvironmentVariables =
            new Dictionary<string, string>
            {
                { "MSBuildExtensionsPath", AppContext.BaseDirectory },
                { "CscToolExe", GetRunCscPath() },
                { "MSBuildSDKsPath", GetMSBuildSDKsPath() }
            };
        
        private readonly IEnumerable<string> _msbuildRequiredParameters = 
            new List<string> { "/m", "/v:m" };

        public MSBuildForwardingApp(IEnumerable<string> argsToForward)
        {
            if (Telemetry.CurrentSessionId != null)
            {
                try
                {
                    Type loggerType = typeof(MSBuildLogger);

                    argsToForward = argsToForward.Concat(new[]
                    {
                        $"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location}"
                    });
                }
                catch (Exception)
                {
                    // Exceptions during telemetry shouldn't cause anything else to fail
                }
            }

            _forwardingApp = new ForwardingApp(
                GetMSBuildExePath(),
                _msbuildRequiredParameters.Concat(argsToForward),
                environmentVariables: _msbuildRequiredEnvironmentVariables);
        }

        public int Execute()
        {
            try
            {
                Environment.SetEnvironmentVariable(TelemetrySessionIdEnvironmentVariableName, Telemetry.CurrentSessionId);

                return _forwardingApp.Execute();
            }
            finally
            {
                Environment.SetEnvironmentVariable(TelemetrySessionIdEnvironmentVariableName, null);
            }
        }

        internal static CommandOption AddVerbosityOption(CommandLineApplication app)
        {
            return app.Option("-v|--verbosity", LocalizableStrings.VerbosityOptionDescription, CommandOptionType.SingleValue);
        }

        private static string GetMSBuildExePath()
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                MSBuildExeName);
        }

        private static string GetMSBuildSDKsPath()
        {
            var envMSBuildSDKsPath = Environment.GetEnvironmentVariable("MSBuildSDKsPath");

            if (envMSBuildSDKsPath != null)
            {
                return envMSBuildSDKsPath;
            }

            return Path.Combine(
                AppContext.BaseDirectory,
                SdksDirectoryName);
        }

        private static string GetRunCscPath()
        {
            var scriptExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".cmd" : ".sh";
            return Path.Combine(AppContext.BaseDirectory, $"RunCsc{scriptExtension}");
        }
    }
}
