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
using System.Diagnostics;

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

        public MSBuildForwardingApp(IEnumerable<string> argsToForward, string msbuildPath = null)
        {
            if (Telemetry.CurrentSessionId != null)
            {
                try
                {
                    Type loggerType = typeof(MSBuildLogger);

                    argsToForward = argsToForward
                        .Concat(new[]
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
                msbuildPath ?? GetMSBuildExePath(),
                _msbuildRequiredParameters.Concat(argsToForward.Select(Escape)),
                environmentVariables: _msbuildRequiredEnvironmentVariables);
        }

        public ProcessStartInfo GetProcessStartInfo()
        {
            return _forwardingApp
                .WithEnvironmentVariable(TelemetrySessionIdEnvironmentVariableName, Telemetry.CurrentSessionId)
                .GetProcessStartInfo();
        }

        public int Execute()
        {
            return GetProcessStartInfo().Execute();
        }

        private static string Escape(string arg) =>
            // this is a workaround for https://github.com/Microsoft/msbuild/issues/1622
             (arg.StartsWith("/p:RestoreSources=", StringComparison.OrdinalIgnoreCase)) ?
                arg.Replace(";", "%3B") 
                   .Replace("://", ":%2F%2F") : 
                arg;

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
            return Path.Combine(AppContext.BaseDirectory, "Roslyn", $"RunCsc{scriptExtension}");
        }
    }
}
