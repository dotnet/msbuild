using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace dotnet_new3
{
    internal class ComponentRegistry : IComponentRegistry
    {
        private bool _isInitialized;

        private static class ObjectCache<T>
        {
            public static readonly Dictionary<string, T> Items = new Dictionary<string, T>();
        }

        private void ProcessType(Type loaded)
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

        private IEnumerable<Type> ProcessAssembly(Assembly assembly)
        {
            List<Type> discoveredTypes = new List<Type>();
            
            foreach (Type loaded in assembly.GetTypes().Where(x => typeof(IComponent).IsAssignableFrom(x)))
            {
                ProcessType(loaded);
                discoveredTypes.Add(loaded);
            }

            return discoveredTypes;
        }

        private void Load()
        {
            if (_isInitialized)
            {
                return;
            }

            bool loadSuccess = true;

            try
            {
                if (Paths.ComponentCacheFile.Exists())
                {
                    string componentsCache = Paths.ComponentCacheFile.ReadAllText("{}");
                    JObject obj = JObject.Parse(componentsCache);
                    JArray loadItems = obj["toLoad"] as JArray;
                    JArray parts = obj["parts"] as JArray;

                    if (loadItems != null && parts != null)
                    {
                        foreach (JToken loadItem in loadItems)
                        {
                            string path = loadItem.ToString();
                            AssemblyLoader.Load(path);
                        }

                        foreach (JToken part in parts)
                        {
                            string typeName = part.ToString();
                            ProcessType(Type.GetType(typeName));
                        }
                    }
                    else
                    {
                        loadSuccess = false;
                    }
                }
                else
                {
                    loadSuccess = false;
                }
            }
            catch
            {
                loadSuccess = false;
            }

            if(!loadSuccess)
            {
                Console.WriteLine("Rebuilding component cache...");
                IEnumerable<string> failures;

                JArray toLoad = new JArray();
                JArray parts = new JArray();
                JObject cache = new JObject
                {
                    {"toLoad", toLoad},
                    {"parts", parts}
                };

                foreach (Assembly assembly in AssemblyLoader.LoadAllAssemblies(out failures))
                {
                    toLoad.Add(assembly.CodeBase.ToPath());
                    foreach (Type type in ProcessAssembly(assembly))
                    {
                        parts.Add(type.AssemblyQualifiedName);
                    }
                }

                //foreach (string failure in failures)
                //{
                //    Console.WriteLine($"Info: Unable to load {failure}");
                //}

                foreach (Type type in ProcessAssembly(typeof(Broker).GetTypeInfo().Assembly))
                {
                    parts.Add(type.AssemblyQualifiedName);
                }

                File.WriteAllText(Paths.ComponentCacheFile, cache.ToString());
            }

            _isInitialized = true;
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

        public void ForceReinitialize()
        {
            Paths.ComponentCacheFile.Delete();
            _isInitialized = false;
        }
    }
}