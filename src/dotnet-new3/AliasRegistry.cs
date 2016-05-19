using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace dotnet_new3
{
    public static class AliasRegistry
    {
        private static JObject _source;
        private static readonly Dictionary<string, string> AliasesToTemplates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> TemplatesToAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static void Load()
        {
            if (TemplatesToAliases.Count > 0)
            {
                return;
            }
            
            if (!Paths.AliasesFile.Exists())
            {
                _source = new JObject();
                return;
            }

            string sourcesText = Paths.AliasesFile.ReadAllText("{}");
            _source = JObject.Parse(sourcesText);

            foreach (JProperty child in _source.Properties())
            {
                AliasesToTemplates[child.Name] = child.Value.ToString();
                TemplatesToAliases[child.Value.ToString()] = child.Name;
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
            if (AliasesToTemplates.TryGetValue(alias, out templateName))
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
            if(AliasesToTemplates.TryGetValue(alias, out templateName))
            {
                match = templates.FirstOrDefault(x => string.Equals(x.Name, templateName, StringComparison.Ordinal));

                if (match != null)
                {
                    return new[] { match };
                }
            }

            HashSet<string> matchedAliases = new HashSet<string>(AliasesToTemplates.Where(x => x.Key.IndexOf(alias, StringComparison.OrdinalIgnoreCase) > -1).Select(x => x.Value));
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
            if(!TemplatesToAliases.TryGetValue(template.Name, out alias))
            {
                return null;
            }

            return alias;
        }

        public static void SetTemplateAlias(string alias, ITemplate template)
        {
            Load();
            _source[alias] = template.Name;
            File.WriteAllText(Paths.AliasesFile, _source.ToString());
        }
    }
}
