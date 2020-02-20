// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools
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
            // Ignore Ctrl-C for the remainder of the command's execution
            // Forwarding commands will just spawn the child process and exit
            Console.CancelKeyPress += (sender, e) => { e.Cancel = true; };

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
