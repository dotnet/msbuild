using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;

namespace dotnet_new3
{
    public class Program
    {
        private static void SetupHiddenCommands(ExtendedCommandParser appExt)
        {
            appExt.RegisterHiddenOption("-i|--install", "--install", CommandOptionType.MultipleValue);
            appExt.RegisterHiddenOption("-up|--update", "--update", CommandOptionType.MultipleValue);
            appExt.RegisterHiddenOption("-u|--uninstall", "--uninstall", CommandOptionType.MultipleValue);
            appExt.RegisterHiddenOption("-d|--dir", "--dir", CommandOptionType.NoValue);
            appExt.RegisterHiddenOption("-a|--alias", "--alias", CommandOptionType.SingleValue);
            //appExt.RegisterHiddenOption("-x|--extra-args", "--extra-args", CommandOptionType.MultipleValue);
            //appExt.RegisterHiddenOption("--locale", "--locale", CommandOptionType.SingleValue);
            //appExt.RegisterHiddenOption("--quiet", "--quiet", CommandOptionType.NoValue);
            appExt.RegisterHiddenOption("--skip-update-check", "--skip-update-check", CommandOptionType.NoValue);
        }

        public static int Main(string[] args)
        {
            ExtendedCommandParser app = new ExtendedCommandParser(false)
            {
                Name = "dotnet new3",
                FullName = "Template Instantiation Commands for .NET Core CLI."
            };

            SetupHiddenCommands(app);

            //TODO: determine which way we want to deal with options, and unify them. One of:
            //  1) Like in SetupHiddenCommands()
            //  2) Like below, using RemoveOption()
            CommandArgument templateNames = app.Argument("template", "The template to instantiate.");
            CommandOption listOnly = app.Option("-l|--list", "List templates containing the specified name.", CommandOptionType.NoValue);
            CommandOption name = app.Option("-n|--name", "The name for the output being created. If no name is specified, the name of the current directory is used.", CommandOptionType.SingleValue);
            //CommandOption dir = app.Option("-d|--dir", "Indicates whether to create a directory for the generated content.", CommandOptionType.NoValue);
            //CommandOption alias = app.Option("-a|--alias", "Creates an alias for the specified template.", CommandOptionType.SingleValue);

            CommandOption parametersFilesOption = app.Option("-x|--extra-args", "Specifies a file containing additional parameters.", CommandOptionType.MultipleValue);
            CommandOption help = app.Option("-h|--help", "Display help for the indicated template's parameters.", CommandOptionType.NoValue);

            CommandOption localeOption = app.Option("--locale", "The locale to use", CommandOptionType.SingleValue);
            CommandOption quietOption = app.Option("--quiet", "Doesn't output any status information.", CommandOptionType.NoValue);

            //CommandOption install = app.Option("-i|--install", "Installs a source or a template pack.", CommandOptionType.MultipleValue);
            //CommandOption update = app.Option("--update", "Update matching templates.", CommandOptionType.NoValue);

            app.OnExecute(async () =>
            {
            string locale = localeOption.HasValue() ? localeOption.Value() : CultureInfo.CurrentCulture.Name;
            app.RemoveOption(localeOption);
            bool quiet = quietOption.HasValue();
            app.RemoveOption(quietOption);
            IList<string> parametersFiles = parametersFilesOption.Values;
            app.RemoveOption(parametersFilesOption);

            EngineEnvironmentSettings.Host = new DefaultTemplateEngineHost(locale);

            bool reinitFlag = app.RemainingArguments.Any(x => x == "--debug:reinit");
            if (reinitFlag)
            {
                Paths.User.FirstRunCookie.Delete();
            }

            // Note: this leaves things in a weird state. Might be related to the localized caches.
            // not sure, need to look into it.
            if (reinitFlag || app.RemainingArguments.Any(x => x == "--debug:reset-config"))
            {
                Paths.User.AliasesFile.Delete();
                Paths.User.SettingsFile.Delete();
                TemplateCache.DeleteAllLocaleCacheFiles();
                return 0;
            }

            if (!Paths.User.BaseDir.Exists() || !Paths.User.FirstRunCookie.Exists())
            {
                if (!quiet)
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

            if (listOnly.HasValue())
            {
                ListTemplates(templateNames.Value);
                return -1;
            }

            IReadOnlyDictionary<string, string> templateParameters;
            IReadOnlyDictionary<string, IList<string>> internalParameters;
            bool anyTemplateMatches = false;

                try
                {
                    IReadOnlyCollection<ITemplateInfo> templates = TemplateCreator.List(templateNames.Value);
                    if (templates.Count == 1)
                    {
                        ITemplateInfo templateInfo = templates.First();
                        app.SetupTemplateParameters(templateInfo);
                    }
                
                    anyTemplateMatches = templates.Any();

                    app.ParseExtraArgs(parametersFiles, out templateParameters, out internalParameters);
                }
                catch (Exception ex)
                {
                    Reporter.Error.WriteLine(ex.Message.Red().Bold());
                    app.ShowHelp();
                    return -1;
                }

                // No template specified, or none matched the input. General help on this command
                if (! anyTemplateMatches && help.HasValue())
                {
                    app.ShowHelp();
                    return 0;
                }

                // Help for the requested template
                if (help.HasValue())
                {
                    return DisplayHelp(templateNames.Value, app);
                }

                // probably doesn't matter if this is before or after the help calls.
                IList<string> install;
                if (internalParameters.TryGetValue("--install", out install) && install.Count > 0)
                {
                    InstallPackage(install.ToList(), quiet);
                    return 0;
                }

                //if (update.HasValue())
                //{
                //    return PerformUpdateAsync(template.Value, quiet, source);
                //}

                //string aliasName = alias.HasValue() ? alias.Value() : null;
                string aliasName = null;
                if (internalParameters.TryGetValue("--alias", out IList<string> aliasNameValues))
                {
                    aliasName = aliasNameValues[0];
                }

                string fallbackName = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;

                bool dirValue = internalParameters.ContainsKey("--dir");
                bool skipUpdateCheckValue = internalParameters.ContainsKey("--skip-update-check");
                    
                if (await TemplateCreator.InstantiateAsync(templateNames.Value ?? "", name.Value(), fallbackName, dirValue, aliasName, templateParameters, skipUpdateCheckValue) == -1)
                {
                    ListTemplates(templateNames.Value);
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
            List<string> toInstall = new List<string>();

            foreach (string package in packages)
            {
                string pkg = package.Trim();
                pkg = Environment.ExpandEnvironmentVariables(pkg);

                //if (package.Exists())
                //{
                TemplateCache.Scan(pkg);
                //}
                //else
                //{
                //    toInstall.Add(package);
                //}
            }

            //NuGetUtil.InstallPackage(toInstall, quiet);

            TemplateCache.WriteTemplateCaches();

            if (!quiet)
            {
                ListTemplates(string.Empty);
            }
        }

        private static void ListTemplates(string templateNames)
        {
            IEnumerable<ITemplateInfo> results = TemplateCreator.List(templateNames);
            HelpFormatter<ITemplateInfo> formatter = new HelpFormatter<ITemplateInfo>(results, 6, '-', false);
            formatter.DefineColumn(delegate(ITemplateInfo t) { return t.Name; }, "Templates");
            formatter.DefineColumn(delegate(ITemplateInfo t) { return $"[{t.ShortName}]"; }, "Short Name");
            formatter.DefineColumn(delegate(ITemplateInfo t) { return AliasRegistry.GetAliasForTemplate(t) ?? ""; }, "Alias");
            Reporter.Output.WriteLine(formatter.Layout());
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

        private static int DisplayHelp(string templateNames, ExtendedCommandParser app)
        {
            IReadOnlyCollection<ITemplateInfo> templates = TemplateCreator.List(templateNames);

            if (templates.Count > 1)
            {
                ListTemplates(templateNames);
                return -1;
            }

            ITemplateInfo templateInfo = templates.First();

            Reporter.Output.WriteLine(templateInfo.Name);
            if (!string.IsNullOrWhiteSpace(templateInfo.Author))
            {
                Reporter.Output.WriteLine($"Author: {templateInfo.Author}");
            }

            if (!string.IsNullOrWhiteSpace(templateInfo.Description))
            {
                Reporter.Output.WriteLine($"Description: {templateInfo.Description}");
            }

            ITemplate template = SettingsLoader.LoadTemplate(templateInfo);
            IParameterSet allParams = template.Generator.GetParametersForTemplate(template);

            //Reporter.Output.WriteLine($"Parameters: {templateInfo.Description}");

            ParameterHelp(allParams, app);

            return 0;
        }

        private static void ParameterHelp(IParameterSet allParams, ExtendedCommandParser app)
        {
            bool anyParams = allParams.ParameterDefinitions.Any(x => x.Priority != TemplateParameterPriority.Implicit);

            if (anyParams)
            {
                HelpFormatter<ITemplateParameter> formatter = new HelpFormatter<ITemplateParameter>(allParams.ParameterDefinitions, 2, null, true);

                formatter.DefineColumn(delegate (ITemplateParameter param)
                    {
                        // the key is guaranteed to exist
                        IList<string> variants = app.CanonicalToVariantsTemplateParamMap[param.Name];
                        string options = string.Join("|", variants.Reverse());
                        return "  " + options;
                    },
                    "Options:"
                );

                formatter.DefineColumn(delegate (ITemplateParameter param)
                    {
                        StringBuilder displayValue = new StringBuilder(255);
                        displayValue.AppendLine(param.Documentation);

                        if (string.Equals(param.DataType, "choice", StringComparison.OrdinalIgnoreCase))
                        {
                            displayValue.AppendLine(string.Join(", ", param.Choices));
                        }
                        else
                        {
                            displayValue.Append(param.DataType ?? "string");
                            displayValue.AppendLine(" - " + param.Priority.ToString());
                        }

                        if (!string.IsNullOrEmpty(param.DefaultValue))
                        {
                            displayValue.AppendLine("Default: " + param.DefaultValue);
                        }

                        return displayValue.ToString();
                    },
                    string.Empty
                );

                Reporter.Output.WriteLine(formatter.Layout());
            }
            else
            {
                Reporter.Output.WriteLine("    (No Parameters)");
            }
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
