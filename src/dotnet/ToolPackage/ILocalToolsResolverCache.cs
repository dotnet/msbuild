// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolPackage
{
    internal interface ILocalToolsResolverCache
    {
        void Save(
            IDictionary<RestoredCommandIdentifier, RestoredCommand> restoredCommandMap,
            DirectoryPath nuGetGlobalPackagesFolder);

        bool TryLoad(
            RestoredCommandIdentifier restoredCommandIdentifier,
            DirectoryPath nuGetGlobalPackagesFolder,
            out RestoredCommand restoredCommand);

        bool TryLoadHighestVersion(
            RestoredCommandIdentifierVersionRange query,
            DirectoryPath nuGetGlobalPackagesFolder,
            out RestoredCommand restoredCommandList);
    }
}
