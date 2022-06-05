// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Cli;

namespace Microsoft.DotNet.Tools.Format
{
    public class DotnetFormatForwardingApp : ForwardingApp
    {
        private static string GetForwardApplicationPath()
            => Path.Combine(AppContext.BaseDirectory, "DotnetTools/dotnet-format/dotnet-format.dll");

        private static string GetDepsFilePath()
            => Path.Combine(AppContext.BaseDirectory, "DotnetTools/dotnet-format/dotnet-format.deps.json");

        private static string GetRuntimeConfigPath()
            => Path.Combine(AppContext.BaseDirectory, "DotnetTools/dotnet-format/dotnet-format.runtimeconfig.json");

        public DotnetFormatForwardingApp(IEnumerable<string> argsToForward)
            : base(forwardApplicationPath: GetForwardApplicationPath(),
                argsToForward: argsToForward,
                depsFile: GetDepsFilePath(),
                runtimeConfig: GetRuntimeConfigPath())
        {
        }
    }
}
