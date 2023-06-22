// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using System.IO;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public sealed class RebuildCommand : MSBuildCommand
    {
        public RebuildCommand(ITestOutputHelper log, string projectPath, string relativePathToProject = null)
            : base(log, "Rebuild", projectPath, relativePathToProject)
        {
        }
    }
}
