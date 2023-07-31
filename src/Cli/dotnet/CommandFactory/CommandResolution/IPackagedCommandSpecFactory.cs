// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
