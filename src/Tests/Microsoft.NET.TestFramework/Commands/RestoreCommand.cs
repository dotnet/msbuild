// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Configuration;
using Xunit.Abstractions;

namespace Microsoft.NET.TestFramework.Commands
{
    public sealed class RestoreCommand : MSBuildCommand
    {
        public RestoreCommand(ITestOutputHelper log, string projectPath, string relativePathToProject = null)
            : base(log, "Restore", projectPath, relativePathToProject)
        {
        }

        protected override bool ExecuteWithRestoreByDefault => false;
    }
}
