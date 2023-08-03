// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
