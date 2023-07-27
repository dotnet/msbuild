// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;

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

            NuGetSignatureVerificationEnabler.ConditionallyEnable(_forwardingApp);
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
