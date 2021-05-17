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
        /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
        public AssemblyMapper(ComparingSettings settings, int rightSetSize = 1)
            : base(settings, rightSetSize) { }

        /// <summary>
        /// Gets the mappers for the namespaces contained in <see cref="ElementMapper{T}.Left"/> and <see cref="ElementMapper{T}.Right"/>
        /// </summary>
        /// <returns>The list of <see cref="NamespaceMapper"/>.</returns>
        public IEnumerable<NamespaceMapper> GetNamespaces()
        {
            if (_namespaces == null)
            {
                _namespaces = new Dictionary<INamespaceSymbol, NamespaceMapper>(Settings.EqualityComparer);
                AddOrCreateMappers(Left, ElementSide.Left);

                if (Right.Length == 1)
                {
                    AddOrCreateMappers(Right[0], ElementSide.Right);
                }
                else
                {
                    for (int i = 0; i < Right.Length; i++)
                    {
                        AddOrCreateMappers(Right[i], ElementSide.Right, i);
                    }
                }

                void AddOrCreateMappers(IAssemblySymbol symbol, ElementSide side, int setIndex = 0)
                {
                    if (symbol == null)
                    {
                        return;
                    }

                    Stack<INamespaceSymbol> stack = new();
                    stack.Push(symbol.GlobalNamespace);
                    while (stack.Count > 0)
                    {
                        INamespaceSymbol nsSymbol = stack.Pop();
                        if (nsSymbol.GetTypeMembers().Length > 0)
                        {
                            AddMapper(nsSymbol);
                        }

                        foreach (INamespaceSymbol child in nsSymbol.GetNamespaceMembers())
                            stack.Push(child);
                    }

                    Dictionary<INamespaceSymbol, List<INamedTypeSymbol>> typeForwards = ResolveTypeForwards(symbol, Settings.EqualityComparer);
                    foreach (KeyValuePair<INamespaceSymbol, List<INamedTypeSymbol>> kvp in typeForwards)
                    {
                        NamespaceMapper mapper = AddMapper(kvp.Key, checkIfExists: true);
                        mapper.AddForwardedTypes(kvp.Value, side, setIndex);
                    }

                    NamespaceMapper AddMapper(INamespaceSymbol ns, bool checkIfExists = false)
                    {
                        if (!_namespaces.TryGetValue(ns, out NamespaceMapper mapper))
                        {
                            mapper = new NamespaceMapper(Settings, Right.Length);
                            _namespaces.Add(ns, mapper);
                        }

                        if (checkIfExists && mapper.GetElement(side, setIndex) != null)
                        {
                            return mapper;
                        }

                        mapper.AddElement(ns, side, setIndex);
                        return mapper;
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
