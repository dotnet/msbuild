// Copyright(c) .NET Foundation and contributors.All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tools.VSTest
{
    public class VSTestCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            VSTestForwardingApp vsTestForwardingApp = new VSTestForwardingApp(args);

            var rootVariableName = Environment.Is64BitProcess ? "DOTNET_ROOT" : "DOTNET_ROOT(x86)";
            if (Environment.GetEnvironmentVariable(rootVariableName) == null)
            {
                vsTestForwardingApp.WithEnvironmentVariable(rootVariableName, Path.GetDirectoryName(new Muxer().MuxerPath));
            }

            return vsTestForwardingApp.Execute();
        }
    }
}
