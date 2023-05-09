// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiSymbolExtensions
{
    /// <summary>
    /// Factory to create an AssemblySymbolLoader
    /// </summary>
    public sealed class AssemblySymbolLoaderFactory : IAssemblySymbolLoaderFactory
    {
        private readonly bool _includeInternals;

        /// <summary>
        /// Creates a new AssemblyLoaderFactory
        /// </summary>
        /// <param name="includeInternals">True to include internal API when reading assemblies from the <see cref="AssemblyLoaderFactory"/> created.</param>
        public AssemblySymbolLoaderFactory(bool includeInternals = false)
        {
            _includeInternals = includeInternals;
        }

        /// <inheritdoc />
        public IAssemblySymbolLoader Create(bool shouldResolveReferences) => new AssemblySymbolLoader(shouldResolveReferences, _includeInternals);
    }
}
