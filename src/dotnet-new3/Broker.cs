using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;
using Newtonsoft.Json.Linq;

namespace dotnet_new3
{
    internal class Broker : IBroker
    {
        private IComponentRegistry _registry;
        private readonly Dictionary<string, TemplateSource> _configuredSources = new Dictionary<string, TemplateSource>(StringComparer.OrdinalIgnoreCase);

        private class TemplateSource
        {
            public ITemplateSource Source { get; set; }

            public string Alias { get; set; }

            public string Location { get; set; }
        }

        public IComponentRegistry ComponentRegistry => _registry ?? (_registry = new ComponentRegistry());

        private void Load()
        {
            if (_configuredSources.Count > 0)
            {
                return;
            }

            string sourcesText = Paths.TemplateSourcesFile.ReadAllText("{}");
            JObject sources = JObject.Parse(sourcesText);

            foreach (JProperty child in sources.Properties())
            {
                JObject entry = child.Value as JObject;
                IMountPoint source;

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

        public IEnumerable<IMountPoint> GetConfiguredSources()
        {
            Load();
            return _configuredSources.Values.Select(x => new ConfiguredTemplateSource(x.Source, x.Alias, x.Location));
        }

        public bool AddConfiguredSource(string alias, string sourceName, string location)
        {
            Load();
            ITemplateSource component;
            if (!ComponentRegistry.TryGetNamedComponent(sourceName, out component))
            {
                return false;
            }

            _configuredSources[alias] = new TemplateSource
            {
                Alias = alias,
                Location = location.ProcessPath(),
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

            File.WriteAllText(Paths.TemplateSourcesFile, result.ToString());
            return true;
        }

        public bool RemoveConfiguredSource(string alias)
        {
            Load();

            if(alias == "*")
            {
                File.WriteAllText(Paths.TemplateSourcesFile, "{}");
                return true;
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

                File.WriteAllText(Paths.TemplateSourcesFile, result.ToString());
                return true;
            }

            return false;
        }
    }
}