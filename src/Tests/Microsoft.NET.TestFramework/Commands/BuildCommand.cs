// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public sealed class BuildCommand : MSBuildCommand
    {
        public BuildCommand(ITestOutputHelper log, string projectRootPath, string relativePathToProject = null)
            : base(log, "Build", projectRootPath, relativePathToProject)
        {
        }
    }
}
