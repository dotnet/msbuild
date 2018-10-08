// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.CommandFactory
{
    public interface IPackagedCommandSpecFactory
    {
        CommandSpec CreateCommandSpecFromLibrary(
            LockFileTargetLibrary toolLibrary,
            string commandName,
            IEnumerable<string> commandArguments,
            IEnumerable<string> allowedExtensions,
            LockFile lockFile,
            string depsFilePath,
            string runtimeConfigPath);
    }
}
