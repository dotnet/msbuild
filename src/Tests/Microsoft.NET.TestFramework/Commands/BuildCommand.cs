// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace Microsoft.NET.TestFramework.Commands
{
    public sealed class BuildCommand : MSBuildCommand
    {
        //  Encourage use of the other overload, which is generally simpler to use
        [EditorBrowsable(EditorBrowsableState.Never)]
        public BuildCommand(ITestOutputHelper log, string projectRootPath, string relativePathToProject = null)
            : base(log, "Build", projectRootPath, relativePathToProject)
        {
        }

        public BuildCommand(TestAsset testAsset, string relativePathToProject = null)
            : base(testAsset, "Build", relativePathToProject)
        {
        }
    }
}
