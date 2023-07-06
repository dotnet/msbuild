// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using System.IO;
using Xunit.Abstractions;
using System.Collections.Generic;

namespace Microsoft.NET.TestFramework.Commands
{
    public sealed class CleanCommand : MSBuildCommand
    {
        public CleanCommand(ITestOutputHelper log, string projectPath, string relativePathToProject = null)
            : base(log, "Clean", projectPath, relativePathToProject)
        {
        }

        public CleanCommand(TestAsset testAsset, string relativePathToProject = null)
           : base(testAsset, "Clean", relativePathToProject)
        {
        }

        protected override bool ExecuteWithRestoreByDefault => false;
    }
}
