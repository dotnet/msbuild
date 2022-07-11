// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.TestHelper
{
    public class AssemblyComponentCatalog : IReadOnlyList<(Type, IIdentifiedComponent)>
    {
        private readonly IReadOnlyList<Assembly> _assemblies;
        private IReadOnlyList<(Type, IIdentifiedComponent)>? _lookup;

        public AssemblyComponentCatalog(IReadOnlyList<Assembly> assemblies)
        {
            _assemblies = assemblies;
        }

        public int Count
        {
            get
            {
                return EnsureLookupLoaded().Count;
            }
        }

        public (Type, IIdentifiedComponent) this[int index]
        {
            get
            {
                return EnsureLookupLoaded()[index];
            }
        }

        public IEnumerator<(Type, IIdentifiedComponent)> GetEnumerator()
        {
            return EnsureLookupLoaded().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private IReadOnlyList<(Type, IIdentifiedComponent)> EnsureLookupLoaded()
        {
            if (_lookup != null)
            {
                return _lookup;
            }

            var builder = new List<(Type, IIdentifiedComponent)>();

            foreach (Assembly asm in _assemblies)
            {
                foreach (Type type in asm.GetTypes())
                {
                    if (!typeof(IIdentifiedComponent).GetTypeInfo().IsAssignableFrom(type) || type.GetTypeInfo().GetConstructor(Type.EmptyTypes) == null || !type.GetTypeInfo().IsClass || type.IsAbstract)
                    {
                        continue;
                    }

                    IReadOnlyList<Type> registerFor = type.GetTypeInfo().ImplementedInterfaces.Where(x => x != typeof(IIdentifiedComponent) && x != typeof(IPrioritizedComponent) && typeof(IIdentifiedComponent).GetTypeInfo().IsAssignableFrom(x)).ToList();
                    if (registerFor.Count == 0)
                    {
                        continue;
                    }

                    IIdentifiedComponent instance = (IIdentifiedComponent)Activator.CreateInstance(type);
                    foreach (var interfaceType in registerFor)
                    {
                        builder.Add((interfaceType, instance));
                    }
                }
            }

            return builder.ToList();
        }
    }
}
