// Copyright(c) .NET Foundation and contributors.All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Tools.Fsi
{
    public class FsiCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);
            return new FsiForwardingApp(args).Execute();
        }
    }
}
