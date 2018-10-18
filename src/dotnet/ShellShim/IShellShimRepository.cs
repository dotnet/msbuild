// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
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
