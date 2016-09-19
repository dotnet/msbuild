// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.Cli
{
    public class VSTestForwardingApp
    {
        private const string VstestAppName = "vstest.console.dll";
        private readonly ForwardingApp _forwardingApp;

        public VSTestForwardingApp(IEnumerable<string> argsToForward)
        {
            _forwardingApp = new ForwardingApp(
                GetVSTestExePath(),
                argsToForward);
        }

        public int Execute()
        {
            return _forwardingApp.Execute();
        }

        private string GetVSTestExePath()
        {
            return Path.Combine(AppContext.BaseDirectory, VstestAppName);
        }
    }
}
