using System.Collections.Generic;
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
                    return results;
                }

                searchSources = new List<IConfiguredTemplateSource> { realSource };
            }

            foreach (IGenerator gen in Program.Broker.ComponentRegistry.OfType<IGenerator>())
            {
                foreach (IConfiguredTemplateSource target in searchSources)
                {
                    results.AddRange(gen.GetTemplatesFromSource(target));
                }
            }

            if (!string.IsNullOrEmpty(searchString.Value))
            {
                results.RemoveAll(x => x.Name.IndexOf(searchString.Value) < 0);
            }

            return results;
        }

        public static async Task<int> Instantiate(CommandLineApplication app, CommandArgument template, CommandOption name, CommandOption source, CommandOption parametersFiles, CommandOption help, CommandOption parameters)
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
                Reporter.Error.WriteLine($"No template \"{template.Value}\" was found in any of the configured sources");
                return -1;
            }

            if (help.HasValue())
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
                }

                return 0;
            }

            string realName = name.Value() ?? new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
            string currentDir = Directory.GetCurrentDirectory();

            if (name.HasValue())
            {
                Directory.SetCurrentDirectory(Directory.CreateDirectory(name.Value()).FullName);
            }

            IParameterSet templateParams = generator.GetParametersForTemplate(tmplt);

            foreach (ITemplateParameter param in templateParams.Parameters)
            {
                if (param.IsName)
                {
                    templateParams.ParameterValues[param] = realName;
                    break;
                }
            }

            for (int i = 0; i < parameters.Values.Count - 1; i += 2)
            {
                ITemplateParameter param;
                if (templateParams.TryGetParameter(parameters.Values[i], out param))
                {
                    templateParams.ParameterValues[param] = parameters.Values[i + 1];
                }
            }

            try
            {
                await generator.Create(tmplt, templateParams);
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDir);
            }
            return 0;
        }
    }
}
