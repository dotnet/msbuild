using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;

namespace dotnet_new3
{
    public class Program
    {
        public static int Main(string[] args)
        {
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
            CommandOption help = app.Option("-h|--help", "Indicates whether to display the help for the template's parameters instead of creating it.", CommandOptionType.NoValue);

            CommandOption quiet = app.Option("--quiet", "Doesn't output any status information.", CommandOptionType.NoValue);
            CommandOption skipUpdateCheck = app.Option("--skip-update-check", "Don't check for updates.", CommandOptionType.NoValue);
            CommandOption update = app.Option("--update", "Update matching templates.", CommandOptionType.NoValue);

            app.OnExecute(async () =>
            {
                bool reinitFlag = app.RemainingArguments.Any(x => x == "--debug:reinit");

                if (reinitFlag)
                {
                    Paths.User.FirstRunCookie.Delete();
                }

                if (reinitFlag || app.RemainingArguments.Any(x => x == "--debug:reset-config"))
                {
                    Paths.User.AliasesFile.Delete();
                    Paths.User.SettingsFile.Delete();
                    Paths.User.TemplateCacheFile.Delete();
                    return 0;
                }

                if (!Paths.User.BaseDir.Exists() || !Paths.User.FirstRunCookie.Exists())
                {
                    if (!quiet.HasValue())
                    {
                        Reporter.Output.WriteLine("Getting things ready for first use...");
                    }

                    ConfigureEnvironment();
                    Paths.User.FirstRunCookie.WriteAllText("");
                }

                if (app.RemainingArguments.Any(x => x == "--debug:showconfig"))
                {
                    ShowConfig();
                    return 0;
                }

                if (install.HasValue())
                {
                    InstallPackage(install.Values, quiet.HasValue());
                    return 0;
                }

                if (update.HasValue())
                {
                    //return PerformUpdateAsync(template.Value, quiet.HasValue(), source);
                }

                if (listOnly.HasValue())
                {
                    ListTemplates(template);
                    return 0;
                }

                IReadOnlyDictionary<string, string> parameters;

                    try
                    {
                        parameters = app.ParseExtraArgs(parametersFiles);
                    }
                    catch (Exception ex)
                    {
                        Reporter.Error.WriteLine(ex.Message.Red().Bold());
                        app.ShowHelp();
                        return -1;
                    }

                if (await TemplateCreator.Instantiate(app, template.Value ?? "", name, dir, help, alias, parameters, quiet.HasValue(), skipUpdateCheck.HasValue()) == -1)
                {
                    ListTemplates(template);
                    return -1;
                }

                return 0;
            });

            int result;
            try
            {
                using (Timing.Over("Execute"))
                {
                    result = app.Execute(args);
                }
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

        //private static async Task<int> PerformUpdateAsync(string name, bool quiet, CommandOption source)
        //{
        //    HashSet<IConfiguredTemplateSource> allSources = new HashSet<IConfiguredTemplateSource>();
        //    HashSet<string> toInstall = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        //    foreach (ITemplate template in TemplateCreator.List(name, source))
        //    {
        //        allSources.Add(template.Source);
        //    }

        //    foreach (IConfiguredTemplateSource src in allSources)
        //    {
        //        if (!quiet)
        //        {
        //            Reporter.Output.WriteLine($"Checking for updates for {src.Alias}...");
        //        }

        //        bool updatesReady;

        //        if (src.ParentSource != null)
        //        {
        //            updatesReady = await src.Source.CheckForUpdatesAsync(src.ParentSource, src.Location);
        //        }
        //        else
        //        {
        //            updatesReady = await src.Source.CheckForUpdatesAsync(src.Location);
        //        }

        //        if (updatesReady)
        //        {
        //            if (!quiet)
        //            {
        //                Reporter.Output.WriteLine($"An update for {src.Alias} is available...");
        //            }

        //            string packageId = src.ParentSource != null
        //                ? src.Source.GetInstallPackageId(src.ParentSource, src.Location)
        //                : src.Source.GetInstallPackageId(src.Location);

        //            toInstall.Add(packageId);
        //        }
        //    }

        //    if(toInstall.Count == 0)
        //    {
        //        if (!quiet)
        //        {
        //            Reporter.Output.WriteLine("No updates were found.");
        //        }

        //        return 0;
        //    }

        //    if (!quiet)
        //    {
        //        Reporter.Output.WriteLine("Installing updates...");
        //    }

        //    List<string> installCommands = new List<string>();
        //    List<string> uninstallCommands = new List<string>();

        //    foreach (string packageId in toInstall)
        //    {
        //        installCommands.Add("-i");
        //        installCommands.Add(packageId);

        //        uninstallCommands.Add("-i");
        //        uninstallCommands.Add(packageId);
        //    }

        //    installCommands.Add("--quiet");
        //    uninstallCommands.Add("--quiet");

        //    Command.CreateDotNet("new3", uninstallCommands).ForwardStdOut().ForwardStdErr().Execute();
        //    Command.CreateDotNet("new3", installCommands).ForwardStdOut().ForwardStdErr().Execute();
        //    Broker.ComponentRegistry.ForceReinitialize();

        //    if (!quiet)
        //    {
        //        Reporter.Output.WriteLine("Done.");
        //    }

        //    return 0;
        //}

        private static void ConfigureEnvironment()
        {
            string userNuGetConfig = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""dotnet new3 builtins"" value = ""{Paths.Global.BuiltInsFeed}""/>
  </packageSources>
</configuration>";

            Paths.User.NuGetConfig.WriteAllText(userNuGetConfig);
            string[] packages = Paths.Global.DefaultInstallPackageList.ReadAllText().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (packages.Length > 0)
            {
                InstallPackage(packages, true);
            }

            packages = Paths.Global.DefaultInstallTemplateList.ReadAllText().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (packages.Length > 0)
            {
                InstallPackage(packages, true);
            }
        }

        private static void InstallPackage(IReadOnlyList<string> packages, bool quiet = false)
        {
            NuGetUtil.InstallPackage(packages, quiet);

            if (!quiet)
            {
                ListTemplates(new CommandArgument());
            }
        }

        private static void ListTemplates(CommandArgument template)
        {
            IEnumerable<ITemplateInfo> results = TemplateCreator.List(template.Value);
            TableFormatter.Print(results, "(No Items)", "   ", '-', new Dictionary<string, Func<ITemplateInfo, object>>
            {
                {"Templates", x => x.Name},
                {"Short Names", x => $"[{x.ShortName}]"},
                {"Alias", x => AliasRegistry.GetAliasForTemplate(x) ?? ""}
            });
        }

        private static void ShowConfig()
        {
            Reporter.Output.WriteLine("dotnet new3 current configuration:");
            Reporter.Output.WriteLine(" ");
            TableFormatter.Print(SettingsLoader.MountPoints, "(No Items)", "   ", '-', new Dictionary<string, Func<MountPointInfo, object>>
            {
                {"Mount Points", x => x.Place},
                {"Id", x => x.MountPointId},
                {"Parent", x => x.ParentMountPointId},
                {"Factory", x => x.MountPointFactoryId}
            });

            TableFormatter.Print(SettingsLoader.Components.OfType<IMountPointFactory>(), "(No Items)", "   ", '-', new Dictionary<string, Func<IMountPointFactory, object>>
            {
                {"Mount Point Factories", x => x.Id},
                {"Type", x => x.GetType().FullName},
                {"Assembly", x => x.GetType().GetTypeInfo().Assembly.FullName}
            });

            TableFormatter.Print(SettingsLoader.Components.OfType<IGenerator>(), "(No Items)", "   ", '-', new Dictionary<string, Func<IGenerator, object>>
            {
                {"Generators", x => x.Id},
                {"Type", x => x.GetType().FullName},
                {"Assembly", x => x.GetType().GetTypeInfo().Assembly.FullName}
            });
        }
    }

    internal class TableFormatter
    {
        public static void Print<T>(IEnumerable<T> items, string noItemsMessage, string columnPad, char header, Dictionary<string, Func<T, object>> dictionary)
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
                foreach (KeyValuePair<string, Func<T, object>> act in dictionary)
                {
                    headers[index] = act.Key;
                    columns[index++].Add(act.Value(item)?.ToString() ?? "(null)");
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
                foreach (KeyValuePair<string, Func<T, object>> act in dictionary)
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
