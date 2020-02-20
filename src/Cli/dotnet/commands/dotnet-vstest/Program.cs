// Copyright(c) .NET Foundation and contributors.All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tools.VSTest
{
    public class VSTestCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);
            return new VSTestForwardingApp(args).Execute();
        }
    }
}
