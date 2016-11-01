// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tools.MSBuild
{
    public class MSBuildForwardingApp
    {
        internal const string TelemetrySessionIdEnvironmentVariableName = "DOTNET_CLI_TELEMETRY_SESSIONID";

        private const string s_msbuildExeName = "MSBuild.dll";

        private readonly ForwardingApp _forwardingApp;

        private readonly Dictionary<string, string> _msbuildRequiredEnvironmentVariables =
            new Dictionary<string, string>
            {
                { "MSBuildExtensionsPath", AppContext.BaseDirectory },
                { "CscToolExe", GetRunCscPath() }
            };
        
        private readonly IEnumerable<string> _msbuildRequiredParameters = 
            new List<string> { "/m" };

        public MSBuildForwardingApp(IEnumerable<string> argsToForward)
        {
            if (Telemetry.CurrentSessionId != null)
            {
                Type loggerType = typeof(MSBuildLogger);
                
                argsToForward = argsToForward.Concat(new[] {$"\"/Logger:{loggerType.FullName},{loggerType.GetTypeInfo().Assembly.Location};{Telemetry.CurrentSessionId}\""});
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

        private static string GetMSBuildExePath()
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                s_msbuildExeName);
        }

        private static string GetRunCscPath()
        {
            var scriptExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".cmd" : ".sh";
            return Path.Combine(AppContext.BaseDirectory, $"RunCsc{scriptExtension}");
        }
    }
}
