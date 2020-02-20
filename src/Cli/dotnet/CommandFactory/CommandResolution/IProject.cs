// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.CommandFactory
{
    internal interface IProject
    {
        LockFile GetLockFile();

        bool TryGetLockFile(out LockFile lockFile);

        IEnumerable<SingleProjectInfo> GetTools();

        string DepsJsonPath { get; }

        string ProjectRoot { get; }

        string RuntimeConfigJsonPath { get; }

        string FullOutputPath { get; }

        NuGetFramework DotnetCliToolTargetFramework { get; }

        Dictionary<string, string> EnvironmentVariables { get; }

        string ToolDepsJsonGeneratorProject { get; }
    }
}
