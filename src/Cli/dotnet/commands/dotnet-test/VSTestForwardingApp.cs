// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Cli
{
    public class VSTestForwardingApp : ForwardingApp
    {
        private const string VstestAppName = "vstest.console.dll";

        public VSTestForwardingApp(IEnumerable<string> argsToForward)
            : base(GetVSTestExePath(), argsToForward)
        {
            (bool hasRootVariable, string rootVariableName, string rootValue) = GetRootVariable();
            if (!hasRootVariable)
            {
                WithEnvironmentVariable(rootVariableName, rootValue);
                VSTestTrace.SafeWriteTrace(() => $"Root variable set {rootVariableName}:{rootValue}");
            }

            VSTestTrace.SafeWriteTrace(() => $"Forwarding to '{GetVSTestExePath()}' with args \"{argsToForward?.Aggregate((a, b) => $"{a} | {b}")}\"");
        }

        private static string GetVSTestExePath()
        {
            // Provide custom path to vstest.console.dll or exe to be able to test it against any version of 
            // vstest.console. This is useful especially for our integration tests.
            // This is equivalent to specifying -p:VSTestConsolePath when using dotnet test with csproj.
            string vsTestConsolePath = Environment.GetEnvironmentVariable("VSTEST_CONSOLE_PATH");
            if (!string.IsNullOrWhiteSpace(vsTestConsolePath))
            {
                return vsTestConsolePath;
            }

            return Path.Combine(AppContext.BaseDirectory, VstestAppName);
        }

        internal static (bool hasRootVariable, string rootVariableName, string rootValue) GetRootVariable()
        {
            string rootVariableName = Environment.Is64BitProcess ? "DOTNET_ROOT" : "DOTNET_ROOT(x86)";
            bool hasRootVariable = Environment.GetEnvironmentVariable(rootVariableName) != null;
            string rootValue = hasRootVariable ? null : Path.GetDirectoryName(new Muxer().MuxerPath);

            // We rename env variable to support --arch switch that relies on DOTNET_ROOT/DOTNET_ROOT(x86)
            // We provide VSTEST_WINAPPHOST_ only in case of testhost*.exe removing VSTEST_WINAPPHOST_ prefix and passing as env vars.
            return (hasRootVariable, $"VSTEST_WINAPPHOST_{rootVariableName}", rootValue);
        }
    }
}
