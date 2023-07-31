// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace Microsoft.NET.TestFramework.Commands
{
    public sealed class RestoreCommand : MSBuildCommand
    {
        //  Encourage use of the other overload, which is generally simpler to use
        [EditorBrowsable(EditorBrowsableState.Never)]
        public RestoreCommand(ITestOutputHelper log, string projectPath, string relativePathToProject = null)
            : base(log, "Restore", projectPath, relativePathToProject)
        {
        }

        public RestoreCommand(TestAsset testAsset, string relativePathToProject = null)
            : base(testAsset, "Restore", relativePathToProject)
        {
        }

        protected override bool ExecuteWithRestoreByDefault => false;
    }
}
