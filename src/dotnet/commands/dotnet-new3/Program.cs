using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.DotNet.Tools.New3
{
    public class New3Command
    {
        private static readonly string HostIdentifier = "dotnetcli";
        private static readonly Version HostVersion = typeof(Program).GetTypeInfo().Assembly.GetName().Version;
        private static DefaultTemplateEngineHost Host;

        private static void SetupInternalCommands(ExtendedCommandParser appExt)
        {
            // visible
            appExt.InternalOption("-l|--list", "--list", "List templates containing the specified name.", CommandOptionType.NoValue);
            appExt.InternalOption("-n|--name", "--name", "The name for the output being created. If no name is specified, the name of the current directory is used.", CommandOptionType.SingleValue);
            appExt.InternalOption("-h|--help", "--help", "Display help for the indicated template's parameters.", CommandOptionType.NoValue);

            // hidden
            appExt.HiddenInternalOption("-d|--dir", "--dir", CommandOptionType.NoValue);
            appExt.HiddenInternalOption("-a|--alias", "--alias", CommandOptionType.SingleValue);
            appExt.HiddenInternalOption("-x|--extra-args", "--extra-args", CommandOptionType.MultipleValue);
            appExt.HiddenInternalOption("--locale", "--locale", CommandOptionType.SingleValue);
            appExt.HiddenInternalOption("--quiet", "--quiet", CommandOptionType.NoValue);
            appExt.HiddenInternalOption("-i|--install", "--install", CommandOptionType.MultipleValue);

            // reserved but not currently used
            appExt.HiddenInternalOption("-up|--update", "--update", CommandOptionType.MultipleValue);
            appExt.HiddenInternalOption("-u|--uninstall", "--uninstall", CommandOptionType.MultipleValue);
            appExt.HiddenInternalOption("--skip-update-check", "--skip-update-check", CommandOptionType.NoValue);

            // Preserve these for now - they've got the help text, in case we want it back.
            // (they'll need to get converted to extended option calls)
            //
            //CommandOption dirOption = app.Option("-d|--dir", "Indicates whether to create a directory for the generated content.", CommandOptionType.NoValue);
            //CommandOption aliasOption = app.Option("-a|--alias", "Creates an alias for the specified template.", CommandOptionType.SingleValue);
            //CommandOption parametersFilesOption = app.Option("-x|--extra-args", "Specifies a file containing additional parameters.", CommandOptionType.MultipleValue);
            //CommandOption localeOption = app.Option("--locale", "The locale to use", CommandOptionType.SingleValue);
            //CommandOption quietOption = app.Option("--quiet", "Doesn't output any status information.", CommandOptionType.NoValue);
            //CommandOption installOption = app.Option("-i|--install", "Installs a source or a template pack.", CommandOptionType.MultipleValue);

            //CommandOption update = app.Option("--update", "Update matching templates.", CommandOptionType.NoValue);
        }

        public static int Run(string[] args)
        {
            // Initial host setup has the current locale. May need to be changed based on inputs.
            Host = new DefaultTemplateEngineHost(HostIdentifier, HostVersion, CultureInfo.CurrentCulture.Name);
            EngineEnvironmentSettings.Host = Host;

            ExtendedCommandParser app = new ExtendedCommandParser()
            {
                Name = "dotnet new3",
                FullName = "Template Instantiation Commands for .NET Core CLI."
            };
            SetupInternalCommands(app);
            CommandArgument templateNames = app.Argument("template", "The template to instantiate.");

            app.OnExecute(async () =>
            {
                app.ParseArgs();
                if (app.InternalParamHasValue("--extra-args"))
                {
                    app.ParseArgs(app.InternalParamValueList("--extra-args"));
                }

                if (app.RemainingParameters.ContainsKey("--debug:attach"))
                {
                    Console.ReadLine();
                }

                if (app.InternalParamHasValue("--locale"))
                {
                    string newLocale = app.InternalParamValue("--locale");
                    if (!ValidateLocaleFormat(newLocale))
                    {
                        EngineEnvironmentSettings.Host.LogMessage(string.Format("Invalid format for input locale: [{0}]. Example valid formats: [en] [en-US]", newLocale));
                        return -1;
                    }

                    Host.UpdateLocale(newLocale);
                }

                int resultCode = InitializationAndDebugging(app, out bool shouldExit);
                if (shouldExit)
                {
                    return resultCode;
                }

                resultCode = ParseTemplateArgs(app, templateNames.Value, out shouldExit);
                if (shouldExit)
                {
                    return resultCode;
                }

                resultCode = MaintenanceAndInfo(app, templateNames.Value, out shouldExit);
                if (shouldExit)
                {
                    return resultCode;
                }

                return await CreateTemplate(app, templateNames.Value);
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

        private static async Task<int> CreateTemplate(ExtendedCommandParser app, string templateName)
        {
            string nameValue = app.InternalParamValue("--name");
            string fallbackName = new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
            bool dirValue = app.InternalParamHasValue("--dir");
            string aliasName = app.InternalParamValue("--alias");
            bool skipUpdateCheckValue = app.InternalParamHasValue("--skip-update-check");

            // TODO: refactor alias creation out of InstantiateAsync()
            TemplateCreationResult instantiateResult = await TemplateCreator.InstantiateAsync(templateName ?? "", nameValue, fallbackName, dirValue, aliasName, app.AllTemplateParams, skipUpdateCheckValue);

            string resultTemplateName = string.IsNullOrEmpty(instantiateResult.TemplateFullName) ? templateName : instantiateResult.TemplateFullName;

            switch (instantiateResult.Status)
            {
                case CreationResultStatus.AliasSucceeded:
                    // TODO: get this localized - in the mean time just list the templates, showing the alias
                    //EngineEnvironmentSettings.Host.LogMessage("Alias creation successful");
                    ListTemplates(templateName);
                    break;
                case CreationResultStatus.AliasFailed:
                    EngineEnvironmentSettings.Host.LogMessage(string.Format("Specified alias {0} already exists. Please specify a different alias.", aliasName));
                    ListTemplates(templateName);
                    break;
                case CreationResultStatus.CreateSucceeded:
                    EngineEnvironmentSettings.Host.LogMessage(string.Format("The template {0} created successfully. Please run \"dotnet restore\" to get started!", resultTemplateName));
                    break;
                case CreationResultStatus.CreateFailed:
                    EngineEnvironmentSettings.Host.LogMessage(string.Format("Template {0} could not be created. Error returned was: {1}", resultTemplateName, instantiateResult.Message));
                    ListTemplates(templateName);
                    break;
                case CreationResultStatus.InstallSucceeded:
                    EngineEnvironmentSettings.Host.LogMessage(string.Format("The template {0} installed successfully. You can use \"dotnet new {0}\" to get started with the new template.", resultTemplateName));
                    break;
                case CreationResultStatus.InstallFailed:
                    EngineEnvironmentSettings.Host.LogMessage(string.Format("Template {0} could not be created. Error returned was: {1}.", resultTemplateName, instantiateResult.Message));
                    break;
                case CreationResultStatus.MissingMandatoryParam:
                    EngineEnvironmentSettings.Host.LogMessage(string.Format("Mandatory parameter {0} missing for template {1}.", instantiateResult.Message, resultTemplateName));
                    break;
                case CreationResultStatus.TemplateNotFound:
                    EngineEnvironmentSettings.Host.LogMessage(string.Format("Template {0} could not be created. Error returned was: {1}.", resultTemplateName, instantiateResult.Message));
                    ListTemplates(templateName);
                    break;
                default:
                    break;
            }

            return instantiateResult.ResultCode;
        }

        private static int InitializationAndDebugging(ExtendedCommandParser app, out bool shouldExit)
        {
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
                shouldExit = true;
                return 0;
            }

            if (!Paths.User.BaseDir.Exists() || !Paths.User.FirstRunCookie.Exists())
            {
                if (!app.InternalParamHasValue("--quiet"))
                {
                    Reporter.Output.WriteLine("Getting things ready for first use...");
                }

                ConfigureEnvironment();
                Paths.User.FirstRunCookie.WriteAllText("");
            }

            if (app.RemainingArguments.Any(x => x == "--debug:showconfig"))
            {
                ShowConfig();
                shouldExit = true;
                return 0;
            }

            shouldExit = false;
            return 0;
        }

        private static int ParseTemplateArgs(ExtendedCommandParser app, string templateName, out bool shouldExit)
        {
            try
            {
                IReadOnlyCollection<ITemplateInfo> templates = TemplateCreator.List(templateName);
                if (templates.Count == 1)
                {
                    ITemplateInfo templateInfo = templates.First();

                    ITemplate template = SettingsLoader.LoadTemplate(templateInfo);
                    IParameterSet allParams = template.Generator.GetParametersForTemplate(template);
                    IReadOnlyDictionary<string, string> parameterNameMap = template.Generator.ParameterMapForTemplate(template);
                    app.SetupTemplateParameters(allParams, parameterNameMap);
                }

                // re-parse after setting up the template params
                app.ParseArgs(app.InternalParamValueList("--extra-args"));
            }
            catch (Exception ex)
            {
                Reporter.Error.WriteLine(ex.Message.Red().Bold());
                app.ShowHelp();
                shouldExit = true;
                return -1;
            }

            if (app.RemainingParameters.Any(x => !x.Key.StartsWith("--debug:")))
            {
                EngineEnvironmentSettings.Host.LogMessage("Invalid input switch:");
                foreach (string flag in app.RemainingParameters.Keys)
                {
                    EngineEnvironmentSettings.Host.LogMessage($"\t{flag}");
                }

                shouldExit = true;
                return DisplayHelp(templateName, app, app.AllTemplateParams);
            }

            shouldExit = false;
            return 0;
        }

        private static int MaintenanceAndInfo(ExtendedCommandParser app, string templateName, out bool shouldExit)
        {
            if (app.InternalParamHasValue("--list"))
            {
                ListTemplates(templateName);
                shouldExit = true;
                return -1;
            }

            if (app.InternalParamHasValue("--help"))
            {
                shouldExit = true;
                return DisplayHelp(templateName, app, app.AllTemplateParams);
            }

            if (app.InternalParamHasValue("--install"))
            {
                InstallPackages(app.InternalParamValueList("--install").ToList(), app.InternalParamHasValue("--quiet"));
                shouldExit = true;
                return 0;
            }

            //if (update.HasValue())
            //{
            //    return PerformUpdateAsync(templateName, quiet, source);
            //}

            if (string.IsNullOrEmpty(templateName))
            {
                ListTemplates(string.Empty);
                shouldExit = true;
                return -1;
            }

            shouldExit = false;
            return 0;
        }

        private static Regex _localeFormatRegex = new Regex(@"
            ^
                [a-z]{2}
                (?:-[A-Z]{2})?
            $"
            , RegexOptions.IgnorePatternWhitespace);

        private static bool ValidateLocaleFormat(string localeToCheck)
        {
            return _localeFormatRegex.IsMatch(localeToCheck);
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

            string[] packageList = Paths.Global.DefaultInstallPackageList.ReadAllText().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (packageList.Length > 0)
            {
                InstallPackages(packageList, true);
            }

            packageList = Paths.Global.DefaultInstallTemplateList.ReadAllText().Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (packageList.Length > 0)
            {
                InstallPackages(packageList, true);
            }
        }

        private static void InstallPackages(IReadOnlyList<string> packageNames, bool quiet = false)
        {
            List<string> toInstall = new List<string>();

            foreach (string package in packageNames)
            {
                string pkg = package.Trim();
                pkg = Environment.ExpandEnvironmentVariables(pkg);

                try
                {
                    if (Directory.Exists(pkg) || File.Exists(pkg))
                    {
                        string packageLocation = new DirectoryInfo(pkg).FullName;
                        TemplateCache.Scan(packageLocation);
                    }
                    else
                    {
                        string directory = Path.GetDirectoryName(pkg);
                        string fileGlob = Path.GetFileName(pkg);
                        string fullDirectory = new DirectoryInfo(directory).FullName;
                        string fullPathGlob = Path.Combine(fullDirectory, fileGlob);
                        TemplateCache.Scan(fullPathGlob);
                    }
                }
                catch
                {
                    EngineEnvironmentSettings.Host.OnNonCriticalError("InvalidPackageSpecification", string.Format($"Package [{pkg}] is not a valid package specification"), null, 0);
                }
            }

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
            formatter.DefineColumn(delegate (ITemplateInfo t) { return t.Name; }, "Templates");
            formatter.DefineColumn(delegate (ITemplateInfo t) { return $"[{t.ShortName}]"; }, "Short Name");
            formatter.DefineColumn(delegate (ITemplateInfo t) { return AliasRegistry.GetAliasForTemplate(t) ?? ""; }, "Alias");
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

        private static int DisplayHelp(string templateNames, ExtendedCommandParser app, IReadOnlyDictionary<string, string> userParameters)
        {
            if (string.IsNullOrWhiteSpace(templateNames))
            {   // no template specified
                app.ShowHelp();
                return 0;
            }

            IReadOnlyCollection<ITemplateInfo> templates = TemplateCreator.List(templateNames);

            if (templates.Count > 1)
            {
                ListTemplates(templateNames);
                return -1;
            }
            else if (templates.Count == 1)
            {
                ITemplateInfo templateInfo = templates.First();
                return TemplateHelp(templateInfo, app, userParameters);
            }
            else
            {
                // TODO: add a message indicating no templates matched the pattern. Requires LOC coordination
                ListTemplates(string.Empty);
                return -1;
            }
        }

        private static int TemplateHelp(ITemplateInfo templateInfo, ExtendedCommandParser app, IReadOnlyDictionary<string, string> userParameters)
        {
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
            IParameterSet allParams = TemplateCreator.SetupDefaultParamValuesFromTemplateAndHost(template, template.DefaultName);
            TemplateCreator.ResolveUserParameters(template, allParams, userParameters);
            ParameterHelp(allParams, app);

            return 0;
        }

        private static void ParameterHelp(IParameterSet allParams, ExtendedCommandParser app)
        {
            IEnumerable<ITemplateParameter> filteredParams = allParams.ParameterDefinitions.Where(x => x.Priority != TemplateParameterPriority.Implicit);

            if (filteredParams.Any())
            {
                HelpFormatter<ITemplateParameter> formatter = new HelpFormatter<ITemplateParameter>(filteredParams, 2, null, true);

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

                    if (allParams.ResolvedValues.TryGetValue(param, out object resolvedValueObject))
                    {
                        string resolvedValue = resolvedValueObject as string;

                        if (!string.IsNullOrEmpty(resolvedValue)
                            && !string.IsNullOrEmpty(param.DefaultValue)
                            && !string.Equals(param.DefaultValue, resolvedValue))
                        {
                            displayValue.AppendLine("Configured Value: " + resolvedValue);
                        }
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
