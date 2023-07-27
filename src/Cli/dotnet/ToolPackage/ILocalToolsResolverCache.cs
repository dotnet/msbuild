// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ToolPackage
{
    internal interface ILocalToolsResolverCache
    {
        void Save(
            IDictionary<RestoredCommandIdentifier, RestoredCommand> restoredCommandMap);

        bool TryLoad(
            RestoredCommandIdentifier restoredCommandIdentifier,
            out RestoredCommand restoredCommand);

        bool TryLoadHighestVersion(
            RestoredCommandIdentifierVersionRange query,
            out RestoredCommand restoredCommandList);
    }
}
