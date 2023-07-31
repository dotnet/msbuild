// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;


namespace Microsoft.DotNet.ShellShim
{
    internal interface IShellShimRepository
    {
        void CreateShim(FilePath targetExecutablePath, ToolCommandName commandName, IReadOnlyList<FilePath> packagedShims = null);

        void RemoveShim(ToolCommandName commandName);
    }
}
