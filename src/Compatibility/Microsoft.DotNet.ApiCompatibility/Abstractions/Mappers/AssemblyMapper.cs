// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Object that represents a mapping between two <see cref="IAssemblySymbol"/> objects.
    /// This also holds a list of <see cref="NamespaceMapper"/> to represent the mapping of namespaces in between
    /// <see cref="ElementMapper{T}.Left"/> and <see cref="ElementMapper{T}.Right"/>.
    /// </summary>
    public class AssemblyMapper : ElementMapper<IAssemblySymbol>
    {
        private Dictionary<INamespaceSymbol, NamespaceMapper> _namespaces;

        /// <summary>
        /// Instantiates an object with the provided <see cref="ComparingSettings"/>.
        /// </summary>
        /// <param name="settings">The settings used to diff the elements in the mapper.</param>
        public AssemblyMapper(ComparingSettings settings) : base(settings) { }

        /// <summary>
        /// Gets the mappers for the namespaces contained in <see cref="ElementMapper{T}.Left"/> and <see cref="ElementMapper{T}.Right"/>
        /// </summary>
        /// <returns>The list of <see cref="NamespaceMapper"/>.</returns>
        public IEnumerable<NamespaceMapper> GetNamespaces()
        {
            if (_namespaces == null)
            {
                _namespaces = new Dictionary<INamespaceSymbol, NamespaceMapper>(Settings.EqualityComparer);
                Dictionary<INamespaceSymbol, List<INamedTypeSymbol>> typeForwards;
                if (Left != null)
                {
                    typeForwards = ResolveTypeForwards(Left, Settings.EqualityComparer);
                    AddOrCreateMappers(Left.GlobalNamespace, 0);
                }

                if (Right != null)
                {
                    typeForwards = ResolveTypeForwards(Right, Settings.EqualityComparer);
                    AddOrCreateMappers(Right.GlobalNamespace, 1);
                }

                void AddOrCreateMappers(INamespaceSymbol ns, int index)
                {
                    Stack<INamespaceSymbol> stack = new();
                    stack.Push(ns);
                    while (stack.Count > 0)
                    {
                        INamespaceSymbol symbol = stack.Pop();
                        if (typeForwards.TryGetValue(symbol, out List<INamedTypeSymbol> forwardedTypes) || symbol.GetTypeMembers().Length > 0)
                        {
                            if (!_namespaces.TryGetValue(symbol, out NamespaceMapper mapper))
                            {
                                mapper = new NamespaceMapper(Settings);
                                _namespaces.Add(symbol, mapper);
                            }

                            mapper.AddElement(symbol, index);
                            mapper.AddForwardedTypes(forwardedTypes ?? new List<INamedTypeSymbol>(), index);
                        }

                        foreach (INamespaceSymbol child in symbol.GetNamespaceMembers())
                            stack.Push(child);
                    }
                }

                static Dictionary<INamespaceSymbol, List<INamedTypeSymbol>> ResolveTypeForwards(IAssemblySymbol assembly, IEqualityComparer<ISymbol> comparer)
                {
                    Dictionary<INamespaceSymbol, List<INamedTypeSymbol>> typeForwards = new(comparer);
                    foreach (INamedTypeSymbol symbol in assembly.GetForwardedTypes())
                    {
                        if (symbol.TypeKind != TypeKind.Error)
                        {
                            if (!typeForwards.TryGetValue(symbol.ContainingNamespace, out List<INamedTypeSymbol> types))
                            {
                                types = new List<INamedTypeSymbol>();
                                typeForwards.Add(symbol.ContainingNamespace, types);
                            }

                            types.Add(symbol);
                        }
                        else
                        {
                            // TODO: Log Warning;
                        }
                    }

                    return typeForwards;
                }
            }

            return _namespaces.Values;
        }
    }
}
