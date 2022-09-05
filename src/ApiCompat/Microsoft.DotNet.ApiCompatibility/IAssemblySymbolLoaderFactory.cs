// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ApiCompatibility
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
