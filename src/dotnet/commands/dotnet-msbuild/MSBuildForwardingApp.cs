// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.Cli
{
    public class MSBuildForwardingApp
    {
        private const string s_msbuildExeName = "MSBuild.exe";
        private readonly ForwardingApp _forwardingApp;

        public MSBuildForwardingApp(IEnumerable<string> argsToForward)
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
            };
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
    }
}
