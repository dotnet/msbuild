// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Microsoft.DotNet.Cli
{
    public class VSTestForwardingApp : ForwardingApp
    {
        private const string VstestAppName = "vstest.console.dll";

        public VSTestForwardingApp(IEnumerable<string> argsToForward)
            : base(GetVSTestExePath(), argsToForward)
        {
        }

        private static string GetVSTestExePath()
        {
            return Path.Combine(AppContext.BaseDirectory, VstestAppName);
        }
    }
}
