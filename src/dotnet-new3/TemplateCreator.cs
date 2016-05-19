using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.TemplateEngine.Abstractions;

namespace dotnet_new3
{
    public static class TemplateCreator
    {
        public static IEnumerable<ITemplate> List(CommandArgument searchString, CommandOption source)
        {
            List<ITemplate> results = new List<ITemplate>();
            IReadOnlyList<IConfiguredTemplateSource> searchSources;

            if (!source.HasValue())
            {
                searchSources = Program.Broker.GetConfiguredSources().ToList();
            }
            else
            {
                IConfiguredTemplateSource realSource = Program.Broker.GetConfiguredSources().FirstOrDefault(x => x.Alias == source.Value());
                if (realSource == null)
                {
                    return results;
                }

                searchSources = new List<IConfiguredTemplateSource> { realSource };
            }

            searchSources = ConfiguredTemplateSourceHelper.Scan(searchSources, Program.Broker.ComponentRegistry.OfType<ITemplateSource>());

            foreach (IGenerator gen in Program.Broker.ComponentRegistry.OfType<IGenerator>())
            {
                foreach (IConfiguredTemplateSource target in searchSources)
                {
                    results.AddRange(gen.GetTemplatesFromSource(target));
                }
            }

            IReadOnlyList<ITemplate> aliasResults = AliasRegistry.GetTemplatesForAlias(searchString.Value, results);

            if (!string.IsNullOrEmpty(searchString.Value))
            {
                results.RemoveAll(x => x.Name.IndexOf(searchString.Value, StringComparison.OrdinalIgnoreCase) < 0 && (x.ShortName?.IndexOf(searchString.Value, StringComparison.OrdinalIgnoreCase) ?? -1) < 0);
            }

            return results.Union(aliasResults);
        }

        public static async Task<int> Instantiate(CommandLineApplication app, CommandArgument template, CommandOption name, CommandOption dir, CommandOption source, CommandOption help, CommandOption alias, IReadOnlyDictionary<string, string> parameters)
        {
            if (string.IsNullOrEmpty(template.Value))
            {
                app.ShowHelp();
                return help.HasValue() ? 0 : -1;
            }

            IReadOnlyList<IConfiguredTemplateSource> searchSources;

            if (!source.HasValue())
            {
                searchSources = Program.Broker.GetConfiguredSources().ToList();
            }
            else
            {
                IConfiguredTemplateSource realSource = Program.Broker.GetConfiguredSources().FirstOrDefault(x => x.Alias == source.Value());
                if (realSource == null)
                {
                    return -1;
                }

                searchSources = new List<IConfiguredTemplateSource> { realSource };
            }

            searchSources = ConfiguredTemplateSourceHelper.Scan(searchSources, Program.Broker.ComponentRegistry.OfType<ITemplateSource>());

            IGenerator generator = null;
            ITemplate tmplt = null;
            string aliasTemplateName = AliasRegistry.GetTemplateNameForAlias(template.Value);

            foreach (IGenerator gen in Program.Broker.ComponentRegistry.OfType<IGenerator>())
            {
                foreach (IConfiguredTemplateSource target in searchSources)
                {
                    if (gen.TryGetTemplateFromSource(target, template.Value, out tmplt))
                    {
                        generator = gen;
                        break;
                    }
                    
                    if(aliasTemplateName != null && gen.TryGetTemplateFromSource(target, aliasTemplateName, out tmplt))
                    {
                        generator = gen;
                        break;
                    }
                }

                if (generator != null)
                {
                    break;
                }
            }

            if (generator == null || tmplt == null)
            {
                List<ITemplate> results = List(template, source).ToList();

                if (results.Count == 0)
                {
                    Reporter.Output.WriteLine($"No template containing \"{template.Value}\" was found in any of the configured sources.".Bold().Red());
                    return -1;
                }

                int index = 0;

                if (results.Count != 1)
                {
                    int counter = 0;
                    TableFormatter.Print(results, "(No Items)", "   ", '-', new Dictionary<string, Func<ITemplate, string>>
                    {
                        {"#", x => $"{++counter}." },
                        {"Templates", x => x.Name},
                        {"Short Names", x => $"[{x.ShortName}]"}
                    });

                    Reporter.Output.WriteLine();
                    Reporter.Output.WriteLine($"Select a template [1]:");

                    string key = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(key))
                    {
                        key = "1";
                    }

                    while (!int.TryParse(key, out index))
                    {
                        if (string.Equals(key, "q", StringComparison.OrdinalIgnoreCase))
                        {
                            return -1;
                        }

                        key = Console.ReadLine();
                    }
                }
                else
                {
                    Reporter.Output.WriteLine($"Using template: {results[0].Name} [{results[0].ShortName}] {AliasRegistry.GetAliasForTemplate(results[0])}");
                    index = 1;
                }

                tmplt = results[index - 1];
                generator = results[index - 1].Generator;
            }

            string realName = name.Value() ?? tmplt.DefaultName ?? new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
            string currentDir = Directory.GetCurrentDirectory();
            bool missingProps = false;

            if (dir.HasValue())
            {
                Directory.SetCurrentDirectory(Directory.CreateDirectory(realName).FullName);
            }

            IParameterSet templateParams = generator.GetParametersForTemplate(tmplt);

            foreach (ITemplateParameter param in templateParams.Parameters)
            {
                if (param.IsName)
                {
                    templateParams.ParameterValues[param] = realName;
                }
                else if (param.Priority != TemplateParameterPriority.Required && param.DefaultValue != null)
                {
                    templateParams.ParameterValues[param] = param.DefaultValue;
                }
            }

            if (alias.HasValue())
            {
                //TODO: Add parameters to aliases (from _parameters_ collection)
                AliasRegistry.SetTemplateAlias(alias.Value(), tmplt);
                Reporter.Output.WriteLine("Alias created.");
                return 0;
            }

            foreach (KeyValuePair<string, string> pair in parameters)
            {
                ITemplateParameter param;
                if (templateParams.TryGetParameter(pair.Key, out param))
                {
                    templateParams.ParameterValues[param] = pair.Value;
                }
            }

            foreach (ITemplateParameter parameter in templateParams.Parameters)
            {
                if (!help.HasValue() && parameter.Priority == TemplateParameterPriority.Required && !templateParams.ParameterValues.ContainsKey(parameter))
                {
                    Reporter.Error.WriteLine($"Missing required parameter {parameter.Name}".Bold().Red());
                    missingProps = true;
                }
            }

            if (help.HasValue() || missingProps)
            {
                string val;
                if (tmplt.TryGetProperty("Description", out val))
                {
                    Reporter.Output.WriteLine($"{val}");
                }

                if (tmplt.TryGetProperty("Author", out val))
                {
                    Reporter.Output.WriteLine($"Author: {val}");
                }

                if (tmplt.TryGetProperty("DiskPath", out val))
                {
                    Reporter.Output.WriteLine($"Disk Path: {val}");
                }

                Reporter.Output.WriteLine("Parameters:");
                foreach (ITemplateParameter parameter in generator.GetParametersForTemplate(tmplt).Parameters.OrderBy(x => x.Priority).ThenBy(x => x.Name))
                {
                    Reporter.Output.WriteLine(
                        $@"    {parameter.Name} ({parameter.Priority})
        Type: {parameter.Type}");

                    if (!string.IsNullOrEmpty(parameter.Documentation))
                    {
                        Reporter.Output.WriteLine($"        Documentation: {parameter.Documentation}");
                    }

                    if (!string.IsNullOrEmpty(parameter.DefaultValue))
                    {
                        Reporter.Output.WriteLine($"        Default: {parameter.DefaultValue}");
                    }
                }

                return missingProps ? -1 : 0;
            }

            try
            {
                Stopwatch sw = Stopwatch.StartNew();
                await generator.Create(tmplt, templateParams);
                sw.Stop();
                Reporter.Output.WriteLine($"Content generated in {sw.Elapsed.TotalMilliseconds} ms");
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDir);
            }
            return 0;
        }
    }
}
