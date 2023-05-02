// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiSymbolExtensions
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
