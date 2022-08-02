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

                    Dictionary<INamespaceSymbol, List<INamedTypeSymbol>> typeForwards = ResolveTypeForwards(symbol, Settings.EqualityComparer, setIndex);

                    Stack<INamespaceSymbol> stack = new();
                    stack.Push(symbol.GlobalNamespace);
                    while (stack.Count > 0)
                    {
                        INamespaceSymbol nsSymbol = stack.Pop();
                        bool hasTypeForwards = typeForwards.TryGetValue(nsSymbol, out List<INamedTypeSymbol> types);
                        if (hasTypeForwards || nsSymbol.GetTypeMembers().Length > 0)
                        {
                            NamespaceMapper mapper = AddMapper(nsSymbol);
                            if (hasTypeForwards)
                            {
                                mapper.AddForwardedTypes(types, side, setIndex);

                                // remove the typeforwards for this namespace as we did
                                // find and instance of the namespace on the current assembly
                                // and we don't want to create a mapper with the namespace in
                                // the assembly where the forwarded type is defined.
                                typeForwards.Remove(nsSymbol);
                            }
                        }

                        foreach (INamespaceSymbol child in nsSymbol.GetNamespaceMembers())
                            stack.Push(child);
                    }

                    // If the current assembly didn't have a namespace symbol for the resolved typeforwards
                    // use the namespace symbol in the assembly where the forwarded type is defined.
                    // But create the mapper with typeForwardsOnly setting to not visit types defined in the target assembly.
                    foreach (KeyValuePair<INamespaceSymbol, List<INamedTypeSymbol>> kvp in typeForwards)
                    {
                        NamespaceMapper mapper = AddMapper(kvp.Key, checkIfExists: true, typeforwardsOnly: true);
                        mapper.AddForwardedTypes(kvp.Value, side, setIndex);
                    }

                    NamespaceMapper AddMapper(INamespaceSymbol ns, bool checkIfExists = false, bool typeforwardsOnly = false)
                    {
                        if (!_namespaces.TryGetValue(ns, out NamespaceMapper mapper))
                        {
                            mapper = new NamespaceMapper(Settings, Right.Length, typeforwardsOnly: typeforwardsOnly);
                            _namespaces.Add(ns, mapper);
                        }
                        else if (checkIfExists && mapper.GetElement(side, setIndex) != null)
                        {
                            return mapper;
                        }

                        mapper.AddElement(ns, side, setIndex);
                        return mapper;
                    }
                }

                Dictionary<INamespaceSymbol, List<INamedTypeSymbol>> ResolveTypeForwards(IAssemblySymbol assembly, IEqualityComparer<ISymbol> comparer, int index)
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
                            // If we should warn on missing references and we are unable to resolve the type forward, then we should log a diagnostic
                            if (Settings.WarnOnMissingReferences)
                            {
                                _assemblyLoadErrors[index].Add(new CompatDifference(
                                        DiagnosticIds.AssemblyReferenceNotFound,
                                        string.Format(Resources.MatchingAssemblyNotFound, $"{symbol.ContainingAssembly.Name}.dll"),
                                        DifferenceType.Changed,
                                        string.Empty));
                            }
                        }
                    }

                    return typeForwards;
                }
            }

            return _namespaces.Values;
        }
    }
}
