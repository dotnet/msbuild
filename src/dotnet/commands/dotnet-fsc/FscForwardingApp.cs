// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Microsoft.DotNet.Cli
{
    public class FscForwardingApp : ForwardingApp
    {
        private const string FscAppName = @"FSharp/fsc.exe";

        public FscForwardingApp(IEnumerable<string> argsToForward)
            : base(GetFscExePath(), argsToForward)
        {
        }

        private static string GetFscExePath()
        {
            return Path.Combine(AppContext.BaseDirectory, FscAppName);
        }
    }
}
