using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;
using System.Text;

namespace dotnet_new3
{
    public class Program
    {
        // Hidden & default options. Listed here to avoid clashes with on-the-fly params from individual templates.
        private static HashSet<string> _defaultCommandOptions;
        private static IDictionary<string, string> _hiddenCommandCanonicalMapping;

        // maps the template param variants to the canonical forms
        private static IDictionary<string, string> _templateParamCanonicalMapping;

        // Initializes the hidden command information.
        // Note that any params containing a colon are also considered hidden (this takes care of the debugging params)
        private static void EnsureHiddenCommands()
        {
            _defaultCommandOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _defaultCommandOptions.Add("l");
            _defaultCommandOptions.Add("list");
            _defaultCommandOptions.Add("n");
            _defaultCommandOptions.Add("name");
            _defaultCommandOptions.Add("d");
            _defaultCommandOptions.Add("dir");
            _defaultCommandOptions.Add("a");
            _defaultCommandOptions.Add("alias");
            _defaultCommandOptions.Add("x");
            _defaultCommandOptions.Add("extra-args");
            _defaultCommandOptions.Add("h");
            _defaultCommandOptions.Add("help");
            _defaultCommandOptions.Add("quiet");
            _defaultCommandOptions.Add("locale");

            // Add anything else we want to reserve.
            _hiddenCommandCanonicalMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _hiddenCommandCanonicalMapping.Add("i", "install");
            _hiddenCommandCanonicalMapping.Add("install", "install");
            _hiddenCommandCanonicalMapping.Add("up", "update");
            _hiddenCommandCanonicalMapping.Add("update", "update");
            _hiddenCommandCanonicalMapping.Add("u", "uninstall");
            _hiddenCommandCanonicalMapping.Add("uninstall", "uninstall");

            _templateParamCanonicalMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsParameterNameTaken(string testName)
        {
            return _defaultCommandOptions.Contains(testName)
                || _hiddenCommandCanonicalMapping.ContainsKey(testName)
                || _templateParamCanonicalMapping.ContainsKey(testName);
        }

        private static void MapTemplateParamToCanonical(string variant, string canonical)
        {
            if (_templateParamCanonicalMapping.TryGetValue(variant, out string existingCanonical))
            {
                throw new Exception($"Option variant {variant} for canonical {canonical} was already defined for canonical ${existingCanonical}");
            }

            _templateParamCanonicalMapping[variant] = canonical;
        }

        private static IDictionary<string, IList<string>> _canonicalToVariantsTemplateParamMap;

        private static IDictionary<string, IList<string>> CanonicalToVariantsTemplateParamMap
        {
            get
            {
                if (_canonicalToVariantsTemplateParamMap == null)
                {
                    _canonicalToVariantsTemplateParamMap = new Dictionary<string, IList<string>>();

                    foreach (KeyValuePair<string, string> variantToCanonical in _templateParamCanonicalMapping)
                    {
                        string variant = variantToCanonical.Key;
                        string canonical = variantToCanonical.Value;

                        IList<string> variantList;
                        if (!_canonicalToVariantsTemplateParamMap.TryGetValue(canonical, out variantList))
                        {
                            variantList = new List<string>();
                            _canonicalToVariantsTemplateParamMap.Add(canonical, variantList);
                        }

                        variantList.Add(variant);
                    }
                }

                return _canonicalToVariantsTemplateParamMap;
            }
        }

        public static int Main(string[] args)
        {
            EnsureHiddenCommands();

            CommandLineApplication app = new CommandLineApplication(false)
            {
                Name = "dotnet new3",
                FullName = "Template Instantiation Commands for .NET Core CLI."
            };

            CommandArgument templateNames = app.Argument("template", "The template to instantiate.");
            CommandOption listOnly = app.Option("-l|--list", "List templates containing the specified name.", CommandOptionType.NoValue);
            CommandOption name = app.Option("-n|--name", "The name for the output being created. If no name is specified, the name of the current directory is used.", CommandOptionType.SingleValue);
            CommandOption dir = app.Option("-d|--dir", "Indicates whether to create a directory for the generated content.", CommandOptionType.NoValue);
            CommandOption alias = app.Option("-a|--alias", "Creates an alias for the specified template.", CommandOptionType.SingleValue);
            CommandOption parametersFiles = app.Option("-x|--extra-args", "Specifies a file containing additional parameters.", CommandOptionType.MultipleValue);
            CommandOption help = app.Option("-h|--help", "Display help for the indicated template's parameters.", CommandOptionType.NoValue);
            CommandOption localeOption = app.Option("--locale", "The locale to use", CommandOptionType.SingleValue);

            CommandOption quiet = app.Option("--quiet", "Doesn't output any status information.", CommandOptionType.NoValue);

            //CommandOption install = app.Option("-i|--install", "Installs a source or a template pack.", CommandOptionType.MultipleValue);

            // TODO: decide if we're keeping these
            CommandOption skipUpdateCheck = app.Option("--skip-update-check", "Don't check for updates.", CommandOptionType.NoValue);
            CommandOption update = app.Option("--update", "Update matching templates.", CommandOptionType.NoValue);

            app.OnExecute(async () =>
            {
                string locale = localeOption.HasValue() ? localeOption.Value() : CultureInfo.CurrentCulture.Name;
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

                if (listOnly.HasValue())
                {
                    ListTemplates(templateNames.Value);
                    return -1;
                }

                IReadOnlyDictionary<string, string> templateParameters;
                IReadOnlyDictionary<string, string> internalParameters;

                try
                {
                    IReadOnlyCollection<ITemplateInfo> templates = TemplateCreator.List(templateNames.Value);
                    if (templates.Count == 1)
                    {
                        ITemplateInfo templateInfo = templates.First();
                        DetermineTemplateParameterNameVariants(templateInfo);
                    }

                    ParseExtraArgs(app, parametersFiles, out templateParameters, out internalParameters);
                }
                catch (Exception ex)
                {
                    Reporter.Error.WriteLine(ex.Message.Red().Bold());
                    app.ShowHelp();
                    return -1;
                }

                if (internalParameters.ContainsKey("install"))
                {
                    InstallPackage(new List<string>() { internalParameters["install"] }, quiet.HasValue());
                }

                //if (update.HasValue())
                //{
                //    return PerformUpdateAsync(template.Value, quiet.HasValue(), source);
                //}

                // No template specified. General help on this command
                if (string.IsNullOrWhiteSpace(templateNames.Value) && help.HasValue())
                {
                    app.ShowHelp();
                    return 0;
                }

                // Help for the requested template
                if (help.HasValue())
                {
                    return DisplayHelp(templateNames.Value);
                }

                string aliasName = alias.HasValue() ? alias.Value() : null;
                string fallbackName = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;

                if (await TemplateCreator.InstantiateAsync(templateNames.Value ?? "", name.Value(), fallbackName, dir.HasValue(), aliasName, templateParameters, skipUpdateCheck.HasValue()) == -1)
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

        // Reads all the extra args, and splits them into the internal parameters, and the template parameters.
        // Internal parameters are hidden params meant to manipulate the behavior of dotnet.
        // Template parameters affect the content of the template.
        private static void ParseExtraArgs(CommandLineApplication app, CommandOption parametersFiles, out IReadOnlyDictionary<string, string> templateParameters, out IReadOnlyDictionary<string, string> internalParameters)
        {
            IReadOnlyDictionary<string, string> allParameters;
            Dictionary<string, string> tempTemplateParameters = new Dictionary<string, string>();
            Dictionary<string, string> tempInternalParameters = new Dictionary<string, string>();

            allParameters = app.ParseExtraArgs(parametersFiles);

            foreach (KeyValuePair<string, string> param in allParameters)
            {
                string canonicalName;
                if (_hiddenCommandCanonicalMapping.TryGetValue(param.Key, out canonicalName))
                {   // this is a known internal parameter
                    tempInternalParameters[canonicalName] = param.Value;
                }
                else if (_templateParamCanonicalMapping.TryGetValue(param.Key, out canonicalName))
                {
                    if (tempTemplateParameters.ContainsKey(canonicalName))
                    {
                        // error, the same param was specified twice
                        // TODO: handle accordingly
                        throw new Exception($"Parameter [${canonicalName}] was specified multiple times, including with the flag [${param.Key}]");
                    }
                    else
                    {
                        tempTemplateParameters[canonicalName] = param.Value;
                    }
                }
                else
                {
                    // not a known internal or template param, we'll throw it in the internal bucket.
                    // TODO: determine a better way to deal with this. As-is, the param will be ignored.
                    tempInternalParameters[param.Key] = param.Value;
                }
            }

            internalParameters = tempInternalParameters;
            templateParameters = tempTemplateParameters;
        }

        // Decides on the acceptable forms of input params for the template params
        // Sets up the canonical mappings
        private static void DetermineTemplateParameterNameVariants(ITemplateInfo templateInfo)
        {
            ITemplate template = SettingsLoader.LoadTemplate(templateInfo);
            IParameterSet allParams = template.Generator.GetParametersForTemplate(template);
            HashSet<string> unusableParams = new HashSet<string>();

            foreach (ITemplateParameter parameter in allParams.ParameterDefinitions.OrderBy(x => x.Name))
            {
                if (parameter.Name.IndexOf(':') >= 0)
                {   // Colon is reserved, template param names cannot have any.
                    unusableParams.Add(parameter.Name);
                    continue;
                }

                bool longNameFound = false;
                bool shortNameFound = false;

                // always unless taken
                if (!IsParameterNameTaken(parameter.Name))
                {
                    MapTemplateParamToCanonical(parameter.Name, parameter.Name);  // include the actual name, so we can easily know what is a template param
                    longNameFound = true;
                }

                // only as fallback
                string qualifiedName = "param:" + parameter.Name;
                if (!longNameFound && !IsParameterNameTaken(qualifiedName))
                {
                    MapTemplateParamToCanonical(qualifiedName, parameter.Name);
                    longNameFound = true;
                }

                // always unless taken
                string singleLetterName = parameter.Name.Substring(0, 1);
                if (!IsParameterNameTaken(singleLetterName))
                {
                    MapTemplateParamToCanonical(singleLetterName, parameter.Name);
                    shortNameFound = true;
                }

                // only as fallback
                string qualifiedSingleLetterName = "p:" + singleLetterName;
                if (!shortNameFound && !IsParameterNameTaken(qualifiedSingleLetterName))
                {
                    MapTemplateParamToCanonical(qualifiedSingleLetterName, parameter.Name);
                    shortNameFound = true;
                }

                // always unless taken
                string shortName = PosixNameToShortName(parameter.Name);
                if (!IsParameterNameTaken(shortName))
                {
                    MapTemplateParamToCanonical(shortName, parameter.Name);
                    shortNameFound = true;
                }

                // only as fallback
                string qualifiedShortName = "p:" + shortName;
                if (!shortNameFound && !IsParameterNameTaken(qualifiedShortName))
                {
                    MapTemplateParamToCanonical(qualifiedShortName, parameter.Name);
                    shortNameFound = true;
                }

                if (!shortNameFound && !longNameFound)
                {
                    unusableParams.Add(parameter.Name);
                }
            }

            if (unusableParams.Count > 0)
            {
                string unusableDisplayList = string.Join(", ", unusableParams);
                throw new Exception($"Template is malformed. The following parameter names are invalid: ${unusableDisplayList}");
            }
        }

        // Concats the first letter of dash separated word.
        private static string PosixNameToShortName(string name)
        {
            IList<string> wordsInName = name.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            IList<string> firstLetters = new List<string>();

            foreach (string word in wordsInName)
            {
                firstLetters.Add(word.Substring(0, 1));
            }

            return string.Join("", firstLetters);
        }

        private static int DisplayHelp(string templateNames)
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

            Reporter.Output.WriteLine($"Parameters: {templateInfo.Description}");

            ParameterHelp(allParams);

            return 0;
        }

        private static void ParameterHelp(IParameterSet allParams)
        {
            bool anyParams = allParams.ParameterDefinitions.Any(x => x.Priority != TemplateParameterPriority.Implicit);

            if (anyParams)
            {
                HelpFormatter<ITemplateParameter> formatter = new HelpFormatter<ITemplateParameter>(allParams.ParameterDefinitions, 3, '-', true);

                // dummy column so the params table is indented
                formatter.DefineColumn(delegate (ITemplateParameter param) { return string.Empty; }, "      ");

                formatter.DefineColumn(delegate (ITemplateParameter param)
                    {
                    // the key is guaranteed to exist
                    IList<string> variants = CanonicalToVariantsTemplateParamMap[param.Name];
                        string options = "--" + string.Join("| --", variants);
                        return options;
                    },
                    "Parameters"
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
                    "Description"
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
