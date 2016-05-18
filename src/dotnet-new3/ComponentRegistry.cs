using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mutant.Chicken.Abstractions;

namespace dotnet_new3
{
    internal class ComponentRegistry : IComponentRegistry
    {
        public bool IsUninitialized { get; private set; }

        private static class ObjectCache<T>
        {
            public static readonly Dictionary<string, T> Items = new Dictionary<string, T>();
        }

        private void ProcessAssembly(Assembly assembly)
        {
            foreach (Type loaded in assembly.GetTypes().Where(x => typeof(IComponent).IsAssignableFrom(x)))
            {
                object instance = Activator.CreateInstance(loaded);
                ITemplateSource source = instance as ITemplateSource;

                if (source != null)
                {
                    ObjectCache<ITemplateSource>.Items[source.Name] = source;
                }
                else
                {
                    IGenerator generator = instance as IGenerator;

                    if (generator != null)
                    {
                        ObjectCache<IGenerator>.Items[generator.Name] = generator;
                    }
                }
            }
        }

        private void Load()
        {
            if (IsUninitialized)
            {
                return;
            }

            foreach (Assembly assembly in AssemblyLoader.LoadAllFromCodebase().ToList())
            {
                ProcessAssembly(assembly);
            }

            ProcessAssembly(typeof(Broker).GetTypeInfo().Assembly);

            IsUninitialized = false;
        }

        public IEnumerable<TComponent> OfType<TComponent>()
        {
            Load();
            return ObjectCache<TComponent>.Items.Values;
        }

        public bool TryGetNamedComponent<TComponent>(string name, out TComponent source)
        {
            Load();
            return ObjectCache<TComponent>.Items.TryGetValue(name, out source);
        }
    }
}