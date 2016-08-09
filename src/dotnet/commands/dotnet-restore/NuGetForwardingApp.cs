// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Tools.Restore
{
    public class NuGetForwardingApp
    {
        private const string s_nugetExeName = "NuGet.CommandLine.XPlat.dll";
        private readonly ForwardingApp _forwardingApp;

        public NuGetForwardingApp(string[] argsToForward)
        {
            _forwardingApp = new ForwardingApp(
                GetNuGetExePath(),
                argsToForward);
        }

        public int Execute()
        {
            return _forwardingApp.Execute();
        }

        private static string GetNuGetExePath()
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                s_nugetExeName);
        }
    }
}
