using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Frameworks;

namespace dotnet_new3
{
    public class Program
    {
        internal static IBroker Broker { get; private set; }

        public static int Main(string[] args)
        {
            //Console.ReadLine();
            Broker = new Broker();

            CommandLineApplication app = new CommandLineApplication(false)
            {
                Name = "dotnet new3",
                FullName = "Template Instantiation Commands for .NET Core CLI."
            };

            CommandArgument template = app.Argument("template", "The template to instantiate.");
            CommandOption listOnly = app.Option("-l|--list", "Lists templates with containing the specified name.", CommandOptionType.NoValue);
            CommandOption name = app.Option("-n|--name", "The name for the output. If no name is specified, the name of the current directory is used.", CommandOptionType.SingleValue);
            CommandOption dir = app.Option("-d|--dir", "Indicates whether to display create a directory for the generated content.", CommandOptionType.NoValue);
            CommandOption alias = app.Option("-a|--alias", "Creates an alias for the specified template", CommandOptionType.SingleValue);
            CommandOption parametersFiles = app.Option("-x|--extra-args", "Adds a parameters file.", CommandOptionType.MultipleValue);
            CommandOption install = app.Option("-i|--install", "Installs a source or a template pack.", CommandOptionType.MultipleValue);
            CommandOption uninstall = app.Option("-u|--uninstall", "Uninstalls a source", CommandOptionType.MultipleValue);
            CommandOption source = app.Option("-s|--source", "The specific template source to get the template from.", CommandOptionType.SingleValue);
            CommandOption currentConfig = app.Option("-c|--current-config", "Lists the currently installed components and sources.", CommandOptionType.NoValue);
            CommandOption help = app.Option("-h|--help", "Indicates whether to display the help for the template's parameters instead of creating it.", CommandOptionType.NoValue);

            CommandOption installComponent = app.Option("--install-component", "Installs a component.", CommandOptionType.MultipleValue);
            CommandOption resetConfig = app.Option("--reset", "Resets the component cache and installed template sources.", CommandOptionType.NoValue);
            CommandOption rescan = app.Option("--rescan", "Rebuilds the component cache.", CommandOptionType.NoValue);

            app.OnExecute(() =>
            {
                if (rescan.HasValue())
                {
                    Broker.ComponentRegistry.ForceReinitialize();
                    ShowConfig();
                    return Task.FromResult(0);
                }

                if (resetConfig.HasValue())
                {
                    Paths.ComponentsDir.Delete();
                    Paths.TemplateCacheDir.Delete();
                    Paths.ScratchDir.Delete();
                    Paths.TemplateSourcesFile.Delete();
                    Paths.AliasesFile.Delete();
                    Paths.ComponentCacheFile.Delete();

                    Paths.TemplateCacheDir.CreateDirectory();
                    Paths.ComponentsDir.CreateDirectory();
                    Broker.ComponentRegistry.ForceReinitialize();
                    TryAddSource(Paths.TemplateCacheDir);

                    ShowConfig();

                    return Task.FromResult(0);
                }

                if (currentConfig.HasValue())
                {
                    ShowConfig();
                    return Task.FromResult(0);
                }

                if (install.HasValue())
                {
                    JObject dependenciesObject = new JObject();
                    JObject projJson = new JObject
                    {
                        {"version", "1.0.0-*"},
                        {"dependencies", dependenciesObject },
                        {
                            "frameworks", new JObject
                            {
                                {
                                    "netcoreapp1.0", new JObject
                                    {
                                        { "imports", "dnxcore50" }
                                    }
                                }
                            }
                        }
                    };
                    
                    foreach (string value in install.Values)
                    {
                        if (value.IndexOfAny(Path.GetInvalidPathChars()) < 0 && value.Exists())
                        {
                            TryAddSource(value);
                        }
                        else
                        {
                            dependenciesObject[value] = "*";
                        }
                    }

                    if (dependenciesObject.Count > 0)
                    {
                        Paths.ScratchDir.CreateDirectory();
                        Paths.ComponentsDir.CreateDirectory();
                        Paths.TemplateCacheDir.CreateDirectory();
                        string projectFile = Path.Combine(Paths.ScratchDir, "project.json");
                        File.WriteAllText(projectFile, projJson.ToString());

                        Reporter.Output.WriteLine("Installing...");
                        Command.CreateDotNet("restore", new[] { "--ignore-failed-sources", "--packages", Paths.ComponentsDir}, NuGetFramework.AnyFramework).WorkingDirectory(Paths.ScratchDir).OnErrorLine(x => Reporter.Error.WriteLine(x.Red().Bold())).Execute();
                        Reporter.Output.WriteLine("Done.");

                        foreach (string value in install.Values)
                        {
                            MoveTemplateToTemplatesCache(value);
                        }

                        Paths.ScratchDir.Delete();
                        Broker.ComponentRegistry.ForceReinitialize();
                        ListTemplates(new CommandArgument(), new CommandOption("--notReal", CommandOptionType.SingleValue));
                    }

                    return Task.FromResult(0);
                }

                if (installComponent.HasValue())
                {
                    JObject dependenciesObject = new JObject();
                    JObject projJson = new JObject
                    {
                        {"version", "1.0.0-*"},
                        {"dependencies", dependenciesObject },
                        {
                            "frameworks", new JObject
                            {
                                {
                                    "netcoreapp1.0", new JObject
                                    {
                                        { "imports", "dnxcore50" }
                                    }
                                }
                            }
                        }
                    };

                    foreach (string value in installComponent.Values)
                    {
                        if (value.IndexOfAny(Path.GetInvalidPathChars()) < 0 && value.Exists())
                        {
                            TryAddSource(value);
                        }
                        else
                        {
                            dependenciesObject[value] = "*";
                        }
                    }

                    if (dependenciesObject.Count > 0)
                    {
                        Paths.ScratchDir.CreateDirectory();
                        Paths.ComponentsDir.CreateDirectory();
                        Paths.TemplateCacheDir.CreateDirectory();
                        string projectFile = Path.Combine(Paths.ScratchDir, "project.json");
                        File.WriteAllText(projectFile, projJson.ToString());

                        Reporter.Output.WriteLine("Installing...");
                        Command.CreateDotNet("restore", new[] { "--ignore-failed-sources", "--packages", Paths.ComponentsDir }, NuGetFramework.AnyFramework).WorkingDirectory(Paths.ScratchDir).OnErrorLine(x => Reporter.Error.WriteLine(x.Red().Bold())).Execute();
                        Reporter.Output.WriteLine("Done.");

                        Paths.ScratchDir.Delete();
                        ShowConfig();
                    }

                    return Task.FromResult(0);
                }

                if (uninstall.HasValue())
                {
                    foreach (string value in uninstall.Values)
                    {
                        if (value == "*")
                        {
                            Paths.TemplateSourcesFile.Delete();
                            Paths.AliasesFile.Delete();
                            Paths.TemplateCacheDir.Delete();
                            return Task.FromResult(0);
                        }

                        if (!TryRemoveSource(value))
                        {
                            Reporter.Error.WriteLine($"Couldn't remove {value} as either a template source or an assembly.".Red().Bold());
                        }
                    }

                    return Task.FromResult(0);
                }

                if (listOnly.HasValue())
                {
                    ListTemplates(template, source);
                    return Task.FromResult(0);
                }

                IReadOnlyDictionary<string, string> parameters;

                try
                {
                    parameters = app.ParseExtraArgs(parametersFiles);
                }
                catch(Exception ex)
                {
                    Reporter.Error.WriteLine(ex.Message.Red().Bold());
                    app.ShowHelp();
                    return Task.FromResult(-1);
                }

                return TemplateCreator.Instantiate(app, template, name, dir, source, help, alias, parameters);
            });

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

                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    ax = ex as AggregateException;

                    while (ax != null && ax.InnerExceptions.Count == 1)
                    {
                        ex = ax.InnerException;
                        ax = ex as AggregateException;
                    }

                    Reporter.Error.WriteLine(ex.Message.Bold().Red());
                }

                Reporter.Error.WriteLine(ex.StackTrace.Bold().Red());
                result = 1;
            }

            return result;
        }

        private static void ListTemplates(CommandArgument template, CommandOption source)
        {
            IEnumerable<ITemplate> results = TemplateCreator.List(template, source);
            TableFormatter.Print(results, "(No Items)", "   ", '-', new Dictionary<string, Func<ITemplate, string>>
            {
                {"Templates", x => x.Name},
                {"Short Names", x => $"[{x.ShortName}]"},
                {"Alias", x => AliasRegistry.GetAliasForTemplate(x) ?? ""}
            });
        }

        private static void MoveTemplateToTemplatesCache(string name)
        {
            string templateSource = Path.Combine(Paths.ComponentsDir, name);

            foreach (string dir in Directory.GetDirectories(templateSource, "*", SearchOption.TopDirectoryOnly))
            {
                foreach (string file in Directory.GetFiles(dir, "*.nupkg", SearchOption.TopDirectoryOnly))
                {
                    File.Copy(file, Path.Combine(Paths.TemplateCacheDir, Path.GetFileName(file)), true);
                }
            }

            Directory.Delete(templateSource, true);
        }

        private static bool TryRemoveSource(string value)
        {
            return Broker.RemoveConfiguredSource(value);
        }

        private static void ShowConfig()
        {
            Reporter.Output.WriteLine("dotnet new3 current configuration:");
            Reporter.Output.WriteLine(" ");
            TableFormatter.Print(Broker.GetConfiguredSources(), "(No Items)", "   ", '-', new Dictionary<string, Func<IConfiguredTemplateSource, string>>
            {
                { "Template Sources", x => x.Location }
            });

            TableFormatter.Print(Broker.ComponentRegistry.OfType<ITemplateSource>(), "(No Items)", "   ", '-', new Dictionary<string, Func<ITemplateSource, string>>
            {
                { "Template Source Readers", x => x.Name },
                { "Assembly", x => x.GetType().GetTypeInfo().Assembly.FullName }
            });

            TableFormatter.Print(Broker.ComponentRegistry.OfType<IGenerator>(), "(No Items)", "   ", '-', new Dictionary<string, Func<IGenerator, string>>
            {
                { "Generators", x => x.Name },
                { "Assembly", x => x.GetType().GetTypeInfo().Assembly.FullName }
            });
        }

        private static bool TryAddSource(string value)
        {
            ITemplateSource source = null;
            foreach (ITemplateSource src in Broker.ComponentRegistry.OfType<ITemplateSource>())
            {
                if (src.CanHandle(value))
                {
                    source = src;
                }
            }

            if (source == null)
            {
                return false;
            }

            Broker.AddConfiguredSource(value, source.Name, value);
            return true;
        }
    }

    internal class TableFormatter
    {
        public static void Print<T>(IEnumerable<T> items, string noItemsMessage, string columnPad, char header, Dictionary<string, Func<T, string>> dictionary)
        {
            List<string>[] columns = new List<string>[dictionary.Count];

            for (int i = 0; i < dictionary.Count; ++i)
            {
                columns[i] = new List<string>();
            }

            string[] headers = new string[dictionary.Count];
            int[] columnWidths = new int[dictionary.Count];
            int valueCount = 0;

            foreach (T item in items)
            {
                int index = 0;
                foreach (KeyValuePair<string, Func<T, string>> act in dictionary)
                {
                    headers[index] = act.Key;
                    columns[index++].Add(act.Value(item));
                }
                ++valueCount;
            }

            if (valueCount > 0)
            {
                for (int i = 0; i < columns.Length; ++i)
                {
                    columnWidths[i] = Math.Max(columns[i].Max(x => x.Length), headers[i].Length);
                }
            }
            else
            {
                int index = 0;
                foreach (KeyValuePair<string, Func<T, string>> act in dictionary)
                {
                    headers[index] = act.Key;
                    columnWidths[index++] = act.Key.Length;
                }
            }

            int headerWidth = columnWidths.Sum() + columnPad.Length * (dictionary.Count - 1);

            for (int i = 0; i < headers.Length - 1; ++i)
            {
                Reporter.Output.Write(headers[i].PadRight(columnWidths[i]));
                Reporter.Output.Write(columnPad);
            }

            Reporter.Output.WriteLine(headers[headers.Length - 1]);
            Reporter.Output.WriteLine("".PadRight(headerWidth, header));

            for (int i = 0; i < valueCount; ++i)
            {
                for (int j = 0; j < columns.Length - 1; ++j)
                {
                    Reporter.Output.Write(columns[j][i].PadRight(columnWidths[j]));
                    Reporter.Output.Write(columnPad);
                }

                Reporter.Output.WriteLine(columns[headers.Length - 1][i]);
            }

            if (valueCount == 0)
            {
                Reporter.Output.WriteLine(noItemsMessage);
            }

            Reporter.Output.WriteLine(" ");
        }
    }
}
