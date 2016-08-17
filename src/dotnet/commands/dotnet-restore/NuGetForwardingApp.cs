// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tools.Restore
{
    public class NuGetForwardingApp
    {
        private const string s_nugetExeName = "NuGet.CommandLine.XPlat.dll";
        private readonly ForwardingApp _forwardingApp;

        public NuGetForwardingApp(IEnumerable<string> argsToForward)
        {
            _forwardingApp = new ForwardingApp(
                GetNuGetExePath(),
                argsToForward);
        }

        public int Execute()
        {
            return _forwardingApp.Execute();
        }

        public NuGetForwardingApp WithEnvironmentVariable(string name, string value)
        {
            _forwardingApp.WithEnvironmentVariable(name, value);

            return this;
        }

        private static string GetNuGetExePath()
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                s_nugetExeName);
        }
    }
}
