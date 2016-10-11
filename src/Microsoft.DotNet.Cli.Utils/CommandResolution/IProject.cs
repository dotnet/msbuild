// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Cli.Utils
{
    internal interface IProject
    {
        LockFile GetLockFile();

        IEnumerable<SingleProjectInfo> GetTools();

        string DepsJsonPath { get; }

        string RuntimeConfigJsonPath { get; }

        Dictionary<string, string> EnvironmentVariables { get; }
    }
}