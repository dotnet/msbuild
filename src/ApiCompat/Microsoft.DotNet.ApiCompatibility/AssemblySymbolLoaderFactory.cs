// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Factory to create an AssemblySymbolLoader
    /// </summary>
    public sealed class AssemblySymbolLoaderFactory : IAssemblySymbolLoaderFactory
    {
        /// <inheritdoc />
        public IAssemblySymbolLoader Create(bool shouldResolveReferences) => new AssemblySymbolLoader(shouldResolveReferences);
    }
}
