// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions.Tests;

namespace Microsoft.DotNet.ApiCompatibility.Tests
{
    internal static class SymbolFactoryExtensions
    {
        internal static IReadOnlyList<ElementContainer<IAssemblySymbol>> GetElementContainersFromSyntaxes(IEnumerable<string> syntaxes, IEnumerable<string> referencesSyntax = null, bool enableNullable = false, byte[] publicKey = null, [CallerMemberName] string assemblyName = "")
        {
            int i = 0;
            List<ElementContainer<IAssemblySymbol>> result = new();
            foreach (string syntax in syntaxes)
            {
                string asmName = $"{assemblyName}-{i}";
                MetadataInformation info = new(asmName, $"runtime-{i}");
                IAssemblySymbol symbol = referencesSyntax != null ?
                    SymbolFactory.GetAssemblyFromSyntaxWithReferences(syntax, referencesSyntax, enableNullable, publicKey, asmName) :
                    SymbolFactory.GetAssemblyFromSyntax(syntax, enableNullable, publicKey, asmName);

                ElementContainer<IAssemblySymbol> container = new(symbol, info);
                result.Add(container);

                i++;
            }

            return result;
        }
    }
}
