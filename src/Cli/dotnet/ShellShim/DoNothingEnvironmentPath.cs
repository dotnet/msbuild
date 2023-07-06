// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.ShellShim
{
    internal class DoNothingEnvironmentPath : IEnvironmentPath
    {
        public void AddPackageExecutablePathToUserPath()
        {
        }

        public void PrintAddPathInstructionIfPathDoesNotExist()
        {
        }
    }
}
