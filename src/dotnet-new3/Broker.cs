using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mutant.Chicken.Abstractions;
using Newtonsoft.Json.Linq;

namespace dotnet_new3
{
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