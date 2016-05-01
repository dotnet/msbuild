using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mutant.Chicken.Abstractions;
using Newtonsoft.Json.Linq;

namespace dotnet_new3
{
    public static class AliasRegistry
    {
        private static JObject _source;
        private static string _path;
        private static Dictionary<string, string> _aliasesToTemplates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> _templatesToAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static void Initialize()
        {
            if (_path != null)
            {
                return;
            }

            Assembly asm = Assembly.GetEntryAssembly();
            Uri codebase = new Uri(asm.CodeBase, UriKind.Absolute);
            string localPath = codebase.LocalPath;
            string dir = Path.GetDirectoryName(localPath);
            string manifest = Path.Combine(dir, "aliases.json");
            _path = manifest;
        }

        private static void Load()
        {
            if (_templatesToAliases.Count > 0)
            {
                return;
            }

            Initialize();

            if (!File.Exists(_path))
            {
                _source = new JObject();
                return;
            }

            string sourcesText = File.ReadAllText(_path);
            _source = JObject.Parse(sourcesText);

            foreach (JProperty child in _source.Properties())
            {
                _aliasesToTemplates[child.Name] = child.Value.ToString();
                _templatesToAliases[child.Value.ToString()] = child.Name;
            }
        }

        public static string GetTemplateNameForAlias(string alias)
        {
            if(alias == null)
            {
                return null;
            }

            Load();
            string templateName;
            if (_aliasesToTemplates.TryGetValue(alias, out templateName))
            {
                return templateName;
            }

            return null;
        }

        public static IReadOnlyList<ITemplate> GetTemplatesForAlias(string alias, IReadOnlyList<ITemplate> templates)
        {
            if(alias == null)
            {
                return new ITemplate[0];
            }

            Load();
            ITemplate match;
            string templateName;
            if(_aliasesToTemplates.TryGetValue(alias, out templateName))
            {
                match = templates.FirstOrDefault(x => string.Equals(x.Name, templateName, StringComparison.Ordinal));

                if (match != null)
                {
                    return new[] { match };
                }
            }

            HashSet<string> matchedAliases = new HashSet<string>(_aliasesToTemplates.Where(x => x.Key.IndexOf(alias, StringComparison.OrdinalIgnoreCase) > -1).Select(x => x.Value));
            List<ITemplate> results = new List<ITemplate>();

            foreach(ITemplate template in templates)
            {
                if (matchedAliases.Contains(template.Name))
                {
                    results.Add(template);
                }
            }

            return results;
        }

        public static string GetAliasForTemplate(ITemplate template)
        {
            Load();
            string alias;
            if(!_templatesToAliases.TryGetValue(template.Name, out alias))
            {
                return null;
            }

            return alias;
        }

        public static void SetTemplateAlias(string alias, ITemplate template)
        {
            Load();
            _source[alias] = template.Name;
            File.WriteAllText(_path, _source.ToString());
        }
    }
}
