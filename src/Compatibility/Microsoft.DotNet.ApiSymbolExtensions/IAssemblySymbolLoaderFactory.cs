// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiSymbolExtensions
{
    /// <summary>
    /// Factory to create an AssemblySymbolLoader
    /// </summary>
    public interface IAssemblySymbolLoaderFactory
    {
        /// <summary>
        /// Create an assembly symbol loader and configure if references should be resolved.
        /// </summary>
        /// <param name="shouldResolveReferences">Set to true if assembly references should be resolved.</param>
        /// <returns>Returns an assembly symbol loader</returns>
        public IAssemblySymbolLoader Create(bool shouldResolveReferences);
    }
}
