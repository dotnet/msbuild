// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Cli
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
