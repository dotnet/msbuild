// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tools.MSBuild
{
    public class MSBuildCommand
    {
        public static int Run(string[] args)
        {
            return new MSBuildForwardingApp(args).Execute();
        }
    }
}
