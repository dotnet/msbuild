using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Mutant.Chicken.Abstractions;
using Newtonsoft.Json.Linq;

namespace dotnet_new3
{
    public class Program
    {
        internal static IBroker Broker { get; private set; }

        public static int Main(string[] args)
        {
            Broker = new Broker();

            var app = new CommandLineApplication
            {
                Name = "dotnet new3",
                FullName = "Mutant Chicken Template Instantiation Commands for .NET Core CLI."
            };

            var sourceCommand = app.Command("source", SourceCommand.Configure);
            var templateCommand = app.Command("template", TemplateCommand.Configure);
            var componentCommand = app.Command("component", ComponentCommand.Configure);
            var resetCommand = app.Command("reset", ResetCommand.Configure);

            var template = app.Argument("template", "The template to instantiate.");
            var name = app.Option("-n|--name", "The name for the output. If no name is specified, the name of the current directory is used.", CommandOptionType.SingleValue);
            var help = app.Option("-h|--help", "Indicates whether to display the help for the template's parameters instead of creating it.", CommandOptionType.NoValue);
            var parametersFiles = app.Option("-a|--args", "Adds a parameters file.", CommandOptionType.MultipleValue);
            var source = app.Option("-s|--source", "The specific template source to get the template from.", CommandOptionType.SingleValue);
            var parameters = app.Option("-p|--parameter", "The parameter name/value alternations to supply to the template.", CommandOptionType.MultipleValue);

            app.OnExecute(() => TemplateCreator.Instantiate(app, template, name, source, parametersFiles, help, parameters));

            int result;
            try
            {
                result = app.Execute(args);
            }
            catch (Exception ex)
            {
                AggregateException ax = ex as AggregateException;

                while (ax != null && ax.InnerExceptions.Count == 1)
                {
                    ex = ax.InnerException;
                    ax = ex as AggregateException;
                }

                Reporter.Error.WriteLine(ex.Message.Bold().Red());
                Reporter.Error.WriteLine(ex.StackTrace.Bold().Red());
                result = 1;
            }

            return result;
        }
    }

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

        private JObject Load()
        {
            if (_truth != null)
            {
                return _truth;
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

            _truth = obj ?? new JObject();
            return _truth;
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
                if (source.GetType().GetTypeInfo().Assembly.GetName().Equals(asm.GetName()))
                {
                    removes.Add(() => ObjectCache<ITemplateSource>.Items.Remove(source.Name));
                }
            }

            foreach (IGenerator source in OfType<IGenerator>())
            {
                if (source.GetType().GetTypeInfo().Assembly.GetName().Equals(asm.GetName()))
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

    internal class Broker : IBroker
    {
        private IComponentRegistry _registry;
        private readonly Dictionary<string, TemplateSource> _configuredSources = new Dictionary<string, TemplateSource>();
        private string _path;

        private class TemplateSource
        {
            public ITemplateSource Source { get; set; }

            public string Alias { get; set; }

            public string Location { get; set; }

            public JObject Json { get; set; }
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
            string manifest = Path.Combine(dir, "template_sources.json");
            _path = manifest;
        }

        public IComponentRegistry ComponentRegistry => _registry ?? (_registry = new ComponentRegistry());

        private void Load()
        {
            if (_configuredSources.Count > 0)
            {
                return;
            }

            Initialize();

            if (!File.Exists(_path))
            {
                return;
            }

            string sourcesText = File.ReadAllText(_path);
            JObject sources = JObject.Parse(sourcesText);

            foreach (JProperty child in sources.Properties())
            {
                JObject entry = child.Value as JObject;
                ITemplateSource source;

                if (entry != null && ComponentRegistry.TryGetNamedComponent(entry["source"].Value<string>(), out source))
                {
                    _configuredSources[child.Name] = new TemplateSource
                    {
                        Alias = child.Name,
                        Json = entry,
                        Location = entry["location"].Value<string>(),
                        Source = source
                    };
                }
            }
        }

        public IEnumerable<IConfiguredTemplateSource> GetConfiguredSources()
        {
            Load();
            return _configuredSources.Values.Select(x => new ConfiguredTemplateSource(x.Source, x.Alias, x.Location));
        }

        public void AddConfiguredSource(string alias, string sourceName, string location)
        {
            Load();
            ITemplateSource component;
            if (!ComponentRegistry.TryGetNamedComponent(sourceName, out component))
            {
                return;
            }

            _configuredSources[alias] = new TemplateSource
            {
                Alias = alias,
                Location = location,
                Source = component
            };

            JObject result = new JObject();
            foreach (KeyValuePair<string, TemplateSource> entry in _configuredSources)
            {
                result[entry.Key] = new JObject
                {
                    {"location", entry.Value.Location },
                    {"source", entry.Value.Source.Name }
                };
            }

            File.WriteAllText(_path, result.ToString());
        }

        public void RemoveConfiguredSource(string alias)
        {
            Load();

            if(alias == "*")
            {
                File.WriteAllText(_path, "{}");
                return;
            }

            if (_configuredSources.Remove(alias))
            {
                JObject result = new JObject();
                foreach (KeyValuePair<string, TemplateSource> entry in _configuredSources)
                {
                    result[entry.Key] = new JObject
                    {
                        {"location", entry.Value.Location },
                        {"source", entry.Value.Source.Name }
                    };
                }

                File.WriteAllText(_path, result.ToString());
            }
        }
    }
}
