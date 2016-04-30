using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mutant.Chicken.Abstractions;
using Newtonsoft.Json.Linq;

namespace dotnet_new3
{
    internal class ComponentRegistry : IComponentRegistry
    {
        private JObject _truth;
        private string _path;

        private static class ObjectCache<T>
        {
            public static readonly Dictionary<string, T> Items = new Dictionary<string, T>();
        }

        private void Initialize()
        {
            if (_path != null)
            {
                return;
            }

            Assembly asm = Assembly.GetEntryAssembly();
            Uri codebase = new Uri(asm.CodeBase, UriKind.Absolute);
            string localPath = codebase.LocalPath;
            string dir = Path.GetDirectoryName(localPath);
            string manifest = Path.Combine(dir, "component_registry.json");
            _path = manifest;
        }

        private void Load()
        {
            if (_truth != null)
            {
                return;
            }

            Initialize();
            string manifestText = File.Exists(_path) ? File.ReadAllText(_path) : "{}";

            if (string.IsNullOrEmpty(manifestText))
            {
                manifestText = "{}";
            }

            Assembly abstractions = typeof(ITemplateSource).GetTypeInfo().Assembly;

            JObject obj = JObject.Parse(manifestText);

            foreach (JProperty prop in obj.Properties())
            {
                Type match = abstractions.GetType(prop.Name);
                Type t = typeof(ObjectCache<>).MakeGenericType(match);
                FieldInfo itemsField = t.GetField("Items", BindingFlags.Public | BindingFlags.Static);
                IDictionary items = (IDictionary)itemsField.GetValue(null);

                JArray propValues = prop.Value as JArray;

                if (propValues != null)
                {
                    foreach (JToken token in propValues.Children())
                    {
                        string qualifiedTypeName = token.Value<string>();
                        Type loaded = Type.GetType(qualifiedTypeName);
                        IComponent instance = Activator.CreateInstance(loaded) as IComponent;

                        if (instance != null)
                        {
                            items[instance.Name] = instance;
                        }
                    }
                }
            }

            _truth = obj;
            return;
        }

        public IEnumerable<TComponent> OfType<TComponent>()
        {
            Load();
            return ObjectCache<TComponent>.Items.Values;
        }

        public void Register<T>(Type type)
        {
            Load();

            JToken itemsToken;
            JArray items;
            if (!_truth.TryGetValue(typeof(T).FullName, out itemsToken) || (items = itemsToken as JArray) == null)
            {
                _truth[typeof(T).FullName] = items = new JArray();
            }

            items.Add(type.AssemblyQualifiedName);
            File.WriteAllText(_path, _truth.ToString());
        }

        public void RemoveAll(Assembly asm)
        {
            List<Action> removes = new List<Action>();
            foreach (ITemplateSource source in OfType<ITemplateSource>())
            {
                if (asm.GetName().ToString().Equals(source.GetType().GetTypeInfo().Assembly.GetName().ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    removes.Add(() => ObjectCache<ITemplateSource>.Items.Remove(source.Name));
                }
            }

            foreach (IGenerator source in OfType<IGenerator>())
            {
                if (asm.GetName().ToString().Equals(source.GetType().GetTypeInfo().Assembly.GetName().ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    removes.Add(() => ObjectCache<IGenerator>.Items.Remove(source.Name));
                }
            }

            foreach (Action act in removes)
            {
                act();
            }

            JObject newTruth = new JObject();
            JArray sources = new JArray();
            JArray generators = new JArray();

            newTruth[typeof(ITemplateSource).FullName] = sources;
            newTruth[typeof(IGenerator).FullName] = generators;

            foreach (ITemplateSource source in OfType<ITemplateSource>())
            {
                sources.Add(source.GetType().AssemblyQualifiedName);
            }

            foreach (IGenerator generator in OfType<IGenerator>())
            {
                generators.Add(generator.GetType().AssemblyQualifiedName);
            }


            File.WriteAllText(_path, newTruth.ToString());
        }

        public bool TryGetNamedComponent<TComponent>(string name, out TComponent source)
        {
            Load();
            return ObjectCache<TComponent>.Items.TryGetValue(name, out source);
        }
    }
}