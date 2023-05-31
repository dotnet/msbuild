// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiSymbolExtensions
{
    /// <summary>
    /// Factory to create an AssemblySymbolLoader
    /// </summary>
    public sealed class AssemblySymbolLoaderFactory : IAssemblySymbolLoaderFactory
    {
        private readonly bool _includeInternalSymbols;

        /// <summary>
        /// Creates a new AssemblySymbolLoaderFactory
        /// </summary>
        /// <param name="includeInternalSymbols">True to include internal API when reading assemblies from the <see cref="AssemblySymbolLoader"/> created.</param>
        public AssemblySymbolLoaderFactory(bool includeInternalSymbols = false)
        {
            _includeInternalSymbols = includeInternalSymbols;
        }

        /// <inheritdoc />
        public IAssemblySymbolLoader Create(bool shouldResolveReferences) => new AssemblySymbolLoader(shouldResolveReferences, _includeInternalSymbols);
    }
}
