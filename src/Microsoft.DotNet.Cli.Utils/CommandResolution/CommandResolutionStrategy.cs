// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Cli.Utils
{
    public enum CommandResolutionStrategy
    {
        // command loaded from a deps file
        DepsFile,

        // command loaded from project dependencies nuget package
        ProjectDependenciesPackage,

        // command loaded from project tools nuget package
        ProjectToolsPackage,

        // command loaded from bundled DotnetTools nuget package
        DotnetToolsPackage,

        // command loaded from the same directory as the executing assembly
        BaseDirectory,

        // command loaded from the same directory as a project.json file
        ProjectLocal,

        // command loaded from PATH environment variable
        Path,

        // command loaded from rooted path
        RootedPath,

        // command loaded from project build output path
        OutputPath,

        // command not found
        None
    }
}
