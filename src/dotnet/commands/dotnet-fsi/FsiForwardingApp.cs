// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Microsoft.DotNet.Cli
{
    public class FsiForwardingApp : ForwardingApp
    {
        private const string FsiAppName = @"FSharp/fsi.exe";

        public FsiForwardingApp(IEnumerable<string> argsToForward)
            : base(GetFsiExePath(), argsToForward)
        {
        }

        private static string GetFsiExePath()
        {
            return Path.Combine(AppContext.BaseDirectory, FsiAppName);
        }
    }
}
