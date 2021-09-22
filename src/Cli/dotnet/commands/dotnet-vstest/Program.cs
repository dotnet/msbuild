// Copyright(c) .NET Foundation and contributors.All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Tools.VSTest
{
    public class VSTestCommand
    {
        public static int Run(ParseResult parseResult)
        {
            parseResult.HandleDebugSwitch();

            VSTestForwardingApp vsTestforwardingApp = new VSTestForwardingApp(parseResult.GetArguments());

            return vsTestforwardingApp.Execute();
        }
    }
}
