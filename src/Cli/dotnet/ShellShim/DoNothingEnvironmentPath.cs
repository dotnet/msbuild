// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
