using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Edge.Runner;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;

namespace dotnet_new3
{
    public static class TemplateCreator
    {
        public static IReadOnlyCollection<ITemplate> List(string searchString)
        {
            HashSet<ITemplate> matchingTemplates = new HashSet<ITemplate>(TemplateEqualityComparer.Default);
            IReadOnlyList<ITemplate> allTemplates = SettingsLoader.GetTemplates().ToList();

            foreach (ITemplate template in allTemplates)
            {
                if (string.IsNullOrEmpty(searchString)
                    || template.Name.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) > -1
                    || template.ShortName.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) > -1)
                {
                    matchingTemplates.Add(template);
                }
            }

            matchingTemplates.UnionWith(AliasRegistry.GetTemplatesForAlias(searchString, allTemplates));
            return matchingTemplates;
        }

        private static bool TryGetTemplate(string templateName, out ITemplate tmplt)
        {
            try
            {
                IReadOnlyCollection<ITemplate> result = List(templateName);

                if (result.Count == 1)
                {
                    tmplt = result.First();
                    return true;
                }
            }
            catch
            {
            }

            tmplt = null;
            return false;
        }

        public static async Task<int> Instantiate(CommandLineApplication app, string templateName, CommandOption name, CommandOption dir, CommandOption help, CommandOption alias, IReadOnlyDictionary<string, string> parameters, bool quiet, bool skipUpdateCheck)
        {
            if(string.IsNullOrWhiteSpace(templateName) && help.HasValue())
            {
                app.ShowHelp();
                return 0;
            }
            ITemplate tmplt;

            using (new Timing(x => Console.WriteLine(x.TotalMilliseconds)))
            {

                if (!TryGetTemplate(templateName, out tmplt))
                {
                    return -1;
                }
            }

            IGenerator generator = tmplt.Generator;

            if (!skipUpdateCheck)
            {
                if (!quiet)
                {
                    Reporter.Output.WriteLine("Checking for updates...");
                }

                //TODO: Implement check for updates over mount points
                //bool updatesReady;

                //if (tmplt.Source.ParentSource != null)
                //{
                //    updatesReady = await tmplt.Source.Source.CheckForUpdatesAsync(tmplt.Source.ParentSource, tmplt.Source.Location);
                //}
                //else
                //{
                //    updatesReady = await tmplt.Source.Source.CheckForUpdatesAsync(tmplt.Source.Location);
                //}

                //if (updatesReady)
                //{
                //    Console.WriteLine("Updates for this template are available. Install them now? [Y]");
                //    string answer = Console.ReadLine();

                //    if (string.IsNullOrEmpty(answer) || answer.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase))
                //    {
                //        string packageId = tmplt.Source.ParentSource != null
                //            ? tmplt.Source.Source.GetInstallPackageId(tmplt.Source.ParentSource, tmplt.Source.Location)
                //            : tmplt.Source.Source.GetInstallPackageId(tmplt.Source.Location);

                //        Command.CreateDotNet("new3", new[] { "-u", packageId, "--quiet" }).ForwardStdOut().ForwardStdErr().Execute();
                //        Command.CreateDotNet("new3", new[] { "-i", packageId, "--quiet" }).ForwardStdOut().ForwardStdErr().Execute();

                //        Program.Broker.ComponentRegistry.ForceReinitialize();

                //        if (!TryGetTemplate(templateName, source, quiet, out tmplt))
                //        {
                //            return -1;
                //        }

                //        generator = tmplt.Generator;
                //    }
                //}
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
                await generator.Create(new Orchestrator(), tmplt, templateParams);
                sw.Stop();

                if (!quiet)
                {
                    Reporter.Output.WriteLine($"Content generated in {sw.Elapsed.TotalMilliseconds} ms");
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(currentDir);
            }
            return 0;
        }
    }
}
