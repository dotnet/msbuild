// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.DotNet.Cli.Utils
{
    internal class MSBuildForwardingAppWithoutLogging
    {
        private const string MSBuildExeName = "MSBuild.dll";

        private const string SdksDirectoryName = "Sdks";

        private readonly ForwardingAppImplementation _forwardingApp;

        private readonly Dictionary<string, string> _msbuildRequiredEnvironmentVariables =
            new Dictionary<string, string>
            {
                { "MSBuildExtensionsPath", AppContext.BaseDirectory },
                { "MSBuildSDKsPath", GetMSBuildSDKsPath() },
                { "DOTNET_HOST_PATH", GetDotnetPath() },
            };

        private readonly IEnumerable<string> _msbuildRequiredParameters =
            new List<string> { "-maxcpucount", "-verbosity:m" };

        public MSBuildForwardingAppWithoutLogging(IEnumerable<string> argsToForward, string msbuildPath = null)
        {
            _forwardingApp = new ForwardingAppImplementation(
                msbuildPath ?? GetMSBuildExePath(),
                _msbuildRequiredParameters.Concat(argsToForward.Select(QuotePropertyValue)),
                environmentVariables: _msbuildRequiredEnvironmentVariables);
        }

        public virtual ProcessStartInfo GetProcessStartInfo()
        {
            return _forwardingApp
                .GetProcessStartInfo();
        }

        public int Execute()
        {
            return GetProcessStartInfo().Execute();
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

        private static string GetDotnetPath()
        {
            return new Muxer().MuxerPath;
        }

        private static bool IsPropertyArgument(string arg)
        {
            return
                arg.StartsWith("/p:", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("/property:", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-p:", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-property:", StringComparison.OrdinalIgnoreCase);
        }

        private static string QuotePropertyValue(string arg)
        {
            if (!IsPropertyArgument(arg))
            {
                return arg;
            }

            var parts = arg.Split(new[] { '=' }, 2);
            if (parts.Length != 2)
            {
                return arg;
            }

            // Escaping `://` is a workaround for https://github.com/Microsoft/msbuild/issues/1622
            // The issue is that MSBuild is collapsing multiple slashes to a single slash due to a bad regex.
            var value = parts[1].Replace("://", ":%2f%2f");
            if (ArgumentEscaper.IsSurroundedWithQuotes(value))
            {
                return $"{parts[0]}={value}";
            }

            return $"{parts[0]}={ArgumentEscaper.EscapeSingleArg(value, forceQuotes: true)}";
        }
    }
}

