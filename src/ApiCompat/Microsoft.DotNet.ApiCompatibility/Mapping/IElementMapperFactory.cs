// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ApiCompatibility.Mapping
{
    /// <summary>
    /// Factory to create an IElementMapper instance.
    /// </summary>
    public interface IElementMapperFactory
    {
        /// <summary>
        /// Creates an <see cref="IAssemblySetMapper"/> instance with optional given mapper settings and the count of the rights that are compared.
        /// </summary>
        /// <param name="settings">The mapper settings.</param>
        /// <param name="rightCount">The number of rights that are compared.</param>
        /// <returns>Returns an AssemblySetMapper based on the given inputs.</returns>
        IAssemblySetMapper CreateAssemblySetMapper(IMapperSettings settings, int rightCount);

        /// <summary>
        /// Creates an <see cref="IAssemblyMapper"/> instance with optional given mapper settings and the count of the rights that are compared.
        /// </summary>
        /// <param name="settings">The mapper settings.</param>
        /// <param name="rightCount">The number of rights that are compared.</param>
        /// <returns>Returns an AssemblyMapper based on the given inputs.</returns>
        IAssemblyMapper CreateAssemblyMapper(IMapperSettings settings, int rightCount);
    }
}
