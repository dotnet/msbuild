// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Cli
{
    public class MSBuildForwardingApp
    {
        private const string s_msbuildExeName = "MSBuild.exe";
        private readonly ForwardingApp _forwardingApp;

        public MSBuildForwardingApp(string[] argsToForward)
        {
            _forwardingApp = new ForwardingApp(
                GetMSBuildExePath(),
                argsToForward,
                environmentVariables: GetEnvironmentVariables());
        }

        public int Execute()
        {
            return _forwardingApp.Execute();
        }

        private static Dictionary<string, string> GetEnvironmentVariables()
        {
            return new Dictionary<string, string>
            {
                { "MSBuildExtensionsPath", AppContext.BaseDirectory },
                { "DotnetHostPath", GetHostPath() },
                { "BaseNuGetRuntimeIdentifier", GetCurrentBaseRid() },
                { "Platform", GetCurrentArchitecture() },
                { "PlatformTarget", GetCurrentArchitecture() }
            };
        }

        private static string GetCurrentBaseRid()
        {
            return RuntimeEnvironment.GetRuntimeIdentifier()
                .Replace("-" + RuntimeEnvironment.RuntimeArchitecture, "");
        }

        private static string GetHostPath()
        {
            return new Muxer().MuxerPath;
        }

        private static string GetMSBuildExePath()
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                s_msbuildExeName);
        }

        private static string GetCurrentArchitecture()
        {
            return RuntimeEnvironment.RuntimeArchitecture;
        }
    }
}
