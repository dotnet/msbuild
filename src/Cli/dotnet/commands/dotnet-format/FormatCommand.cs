// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Format
{
    public static class FormatCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);
            return new DotnetFormatForwardingApp(args).Execute();
        }
    }
}
