// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Microsoft.DotNet.Cli
{
    public class Build3Command
    {
        public static int Run(string[] args)
        {
            return new MSBuildForwardingApp(args).Execute();
        }
    }
}
