using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    public class AssemblyMapper : ElementMapper<IAssemblySymbol>
    {
        private Dictionary<INamespaceSymbol, NamespaceMapper> _namespaces;

        public AssemblyMapper(DiffingSettings settings) : base(settings) { }

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
