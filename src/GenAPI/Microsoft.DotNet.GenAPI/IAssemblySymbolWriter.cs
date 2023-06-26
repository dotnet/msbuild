// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI
{
    /// <summary>
    /// Interface encapsulates logic for processing symbol assemblies.
    /// </summary>
    public interface IAssemblySymbolWriter
    {
        /// <summary>
        /// Process a given assembly symbol.
        /// </summary>
        /// <param name="assemblySymbol"><see cref="IAssemblySymbol"/> representing the loaded assembly.</param>
        void WriteAssembly(IAssemblySymbol assemblySymbol);
    }
}
