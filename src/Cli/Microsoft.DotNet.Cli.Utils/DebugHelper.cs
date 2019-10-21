// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class DebugHelper
    {
        [Conditional("DEBUG")]
        public static void HandleDebugSwitch(ref string[] args)
        {
            if (args.Length > 0 && string.Equals("--debug", args[0], StringComparison.OrdinalIgnoreCase))
            {
                args = args.Skip(1).ToArray();
                WaitForDebugger();
            }
        }

        public static void WaitForDebugger()
        {
            Console.WriteLine(LocalizableStrings.WaitingForDebuggerToAttach);
            Console.WriteLine(string.Format(LocalizableStrings.ProcessId, Process.GetCurrentProcess().Id));
            Console.ReadLine();
        }
    }
}
