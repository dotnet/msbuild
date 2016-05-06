using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Mutant.Chicken.Abstractions;

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
                FullName = "Mutant Chicken Template Instantiation Commands for .NET Core CLI."
            };

            CommandArgument template = app.Argument("template", "The template to instantiate.");
            CommandOption listOnly = app.Option("-l|--list", "Lists templates with containing the specified name.", CommandOptionType.NoValue);
            CommandOption name = app.Option("-n|--name", "The name for the output. If no name is specified, the name of the current directory is used.", CommandOptionType.SingleValue);
            CommandOption dir = app.Option("-d|--dir", "Indicates whether to display create a directory for the generated content.", CommandOptionType.NoValue);
            CommandOption alias = app.Option("-a|--alias", "Creates an alias for the specified template", CommandOptionType.SingleValue);
            CommandOption parametersFiles = app.Option("-x|--extra-args", "Adds a parameters file.", CommandOptionType.MultipleValue);
            CommandOption install = app.Option("-i|--install", "Installs a component or a source", CommandOptionType.MultipleValue);
            CommandOption uninstall = app.Option("-u|--uninstall", "Uninstalls a component or a source", CommandOptionType.MultipleValue);
            CommandOption source = app.Option("-s|--source", "The specific template source to get the template from.", CommandOptionType.SingleValue);
            CommandOption currentConfig = app.Option("-c|--current-config", "Lists the currently installed components and sources.", CommandOptionType.NoValue);
            CommandOption help = app.Option("-h|--help", "Indicates whether to display the help for the template's parameters instead of creating it.", CommandOptionType.NoValue);

            app.OnExecute(() =>
            {
                if (currentConfig.HasValue())
                {
                    ShowConfig();
                    return Task.FromResult(0);
                }

                if (install.HasValue())
                {
                    foreach (string value in install.Values)
                    {
                        if (!TryAddComponent(value) && !TryAddSource(value))
                        {
                            Reporter.Error.WriteLine($"Couldn't add {value} as either a template source or an assembly.".Red().Bold());
                        }
                    }

                    return Task.FromResult(0);
                }

                if (uninstall.HasValue())
                {
                    foreach (string value in uninstall.Values)
                    {
                        if (value == "*")
                        {
                            Assembly asm = Assembly.GetEntryAssembly();
                            Uri codebase = new Uri(asm.CodeBase, UriKind.Absolute);
                            string localPath = codebase.LocalPath;
                            string targetDir = Path.GetDirectoryName(localPath);
                            string manifest = Path.Combine(targetDir, "component_registry.json");
                            string sources = Path.Combine(targetDir, "template_sources.json");

                            if (File.Exists(manifest))
                            {
                                File.Delete(manifest);
                            }

                            if (File.Exists(sources))
                            {
                                File.Delete(sources);
                            }

                            return Task.FromResult(0);
                        }

                        if (!TryRemoveComponent(value) && !TryRemoveSource(value))
                        {
                            Reporter.Error.WriteLine($"Couldn't remove {value} as either a template source or an assembly.".Red().Bold());
                        }
                    }

                    return Task.FromResult(0);
                }

                if (listOnly.HasValue())
                {
                    IEnumerable<ITemplate> results = TemplateCreator.List(template, source);
                    TableFormatter.Print(results, "(No Items)", "   ", '-', new Dictionary<string, Func<ITemplate, string>>
                    {
                        {"Templates", x => x.Name},
                        {"Short Names", x => $"[{x.ShortName}]" },
                        {"Alias", x => AliasRegistry.GetAliasForTemplate(x) ?? "" }
                    });

                    return Task.FromResult(0);
                }

                IReadOnlyDictionary<string, string> parameters = app.ParseExtraArgs(parametersFiles);
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

                while(ex.InnerException != null)
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

        private static bool TryRemoveSource(string value)
        {
            return Broker.RemoveConfiguredSource(value);
        }

        private static bool TryRemoveComponent(string value)
        {
            Assembly asm;
            try
            {
                AssemblyName name = new AssemblyName(value);
                asm = Assembly.Load(name);
            }
            catch
            {
                return false;
            }

            if (asm == null)
            {
                return false;
            }

            Broker.ComponentRegistry.RemoveAll(asm);
            return true;
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

        private static bool TryAddComponent(string value)
        {
            Assembly asm;
            try
            {
                AssemblyName name = new AssemblyName(value);
                asm = Assembly.Load(name);
            }
            catch
            {
                return false;
            }

            if (asm == null)
            {
                Assembly entry = Assembly.GetEntryAssembly();
                Uri codebase = new Uri(entry.CodeBase, UriKind.Absolute);
                string localPath = codebase.LocalPath;
                string dir = Path.GetDirectoryName(localPath);
                string sourceFile = value;
                if (!Path.IsPathRooted(sourceFile))
                {
                    sourceFile = Path.Combine(Directory.GetCurrentDirectory(), sourceFile);
                }

                string file = Path.GetFileName(sourceFile);
                File.Copy(sourceFile, Path.Combine(dir, file), true);
                asm = Assembly.Load(new AssemblyName(file));
            }

            foreach (Type type in asm.ExportedTypes)
            {
                if (typeof(ITemplateSource).IsAssignableFrom(type))
                {
                    Broker.ComponentRegistry.Register<ITemplateSource>(type);
                }

                if (typeof(IGenerator).IsAssignableFrom(type))
                {
                    Broker.ComponentRegistry.Register<IGenerator>(type);
                }
            }

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

            int headerWidth = columnWidths.Sum() + columnPad.Length*(dictionary.Count - 1);

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
