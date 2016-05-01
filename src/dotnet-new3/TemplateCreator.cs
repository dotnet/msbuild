using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Mutant.Chicken.Abstractions;

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

            //searchSources = ConfiguredTemplateSourceHelper.Scan(searchSources, Program.Broker.ComponentRegistry.OfType<ITemplateSource>());

            foreach (IGenerator gen in Program.Broker.ComponentRegistry.OfType<IGenerator>())
            {
                foreach (IConfiguredTemplateSource target in searchSources)
                {
                    results.AddRange(gen.GetTemplatesFromSource(target));
                }
            }

            if (!string.IsNullOrEmpty(searchString.Value))
            {
                results.RemoveAll(x => x.Name.IndexOf(searchString.Value, StringComparison.OrdinalIgnoreCase) < 0);
            }

            return results;
        }

        public static async Task<int> Instantiate(CommandLineApplication app, CommandArgument template, CommandOption name, CommandOption dir, CommandOption source, CommandOption parametersFiles, CommandOption help, IReadOnlyDictionary<string, string> parameters)
        {
            if (string.IsNullOrEmpty(template.Value))
            {
                app.ShowHelp();
                return help.HasValue() ? 0 : -1;
            }

            IEnumerable<IConfiguredTemplateSource> searchSources;

            if (!source.HasValue())
            {
                searchSources = Program.Broker.GetConfiguredSources();
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

            IGenerator generator = null;
            ITemplate tmplt = null;

            foreach (IGenerator gen in Program.Broker.ComponentRegistry.OfType<IGenerator>())
            {
                foreach (IConfiguredTemplateSource target in searchSources)
                {
                    if (gen.TryGetTemplateFromSource(target, template.Value, out tmplt))
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
                Reporter.Output.WriteLine($"No template with name \"{template.Value}\" was found in any of the configured sources, searching...".Bold().Yellow());

                List<ITemplate> results = List(template, source).ToList();
                Reporter.Output.WriteLine($"{results.Count} matching template(s) found.".Bold().Yellow());

                if (results.Count == 0)
                {
                    return -1;
                }

                int index = 0;

                if (results.Count != 1)
                {
                    Reporter.Output.WriteLine("Template Name - Source Name - Generator Name");
                    foreach (ITemplate result in results)
                    {
                        Reporter.Output.WriteLine($"[{index++}] {result.Name} - {result.Source.Alias} - {result.Generator.Name}");
                    }

                    Reporter.Output.WriteLine();
                    Reporter.Output.WriteLine($"Template # [0 - {results.Count - 1}] (q to cancel):");

                    string key = Console.ReadLine();
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
                    Reporter.Output.WriteLine($"Using template: {results[0].Name} - {results[0].Source.Alias} - {results[0].Generator.Name}");
                    index = 0;
                }

                tmplt = results[index];
                generator = results[index].Generator;
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
                if (parameter.Priority == TemplateParameterPriority.Required && !templateParams.ParameterValues.ContainsKey(parameter))
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
