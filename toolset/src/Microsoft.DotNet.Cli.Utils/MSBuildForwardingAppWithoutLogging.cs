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

        internal static string MSBuildExtensionsPathTestHook = null;

        private readonly Dictionary<string, string> _msbuildRequiredEnvironmentVariables =
            new Dictionary<string, string>
            {
                { "MSBuildExtensionsPath", MSBuildExtensionsPathTestHook ?? AppContext.BaseDirectory },
                { "MSBuildSDKsPath", GetMSBuildSDKsPath() },
                { "DOTNET_HOST_PATH", GetDotnetPath() },
            };

        private readonly IEnumerable<string> _msbuildRequiredParameters =
            new List<string> { "-maxcpucount", "-verbosity:m" };

        public MSBuildForwardingAppWithoutLogging(IEnumerable<string> argsToForward, string msbuildPath = null)
        {
            _forwardingApp = new ForwardingAppImplementation(
                msbuildPath ?? GetMSBuildExePath(),
                _msbuildRequiredParameters.Concat(argsToForward.Select(Escape)),
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

        private static string Escape(string arg) =>
             // this is a workaround for https://github.com/Microsoft/msbuild/issues/1622
             IsRestoreSources(arg) ?
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

        private static string GetDotnetPath()
        {
            return new Muxer().MuxerPath;
        }

        private static bool IsRestoreSources(string arg)
        {
            return arg.StartsWith("/p:RestoreSources=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("/property:RestoreSources=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-p:RestoreSources=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-property:RestoreSources=", StringComparison.OrdinalIgnoreCase);
        }
    }
}

