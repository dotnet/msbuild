// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ShellShim
{
    internal interface IShellShimRepository
    {
        void CreateShim(FilePath targetExecutablePath, string commandName);

        void RemoveShim(string commandName);
    }
}
