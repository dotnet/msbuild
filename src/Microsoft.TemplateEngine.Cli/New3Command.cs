// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Reflection;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Cli.Alias;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.HelpAndUsage;
using Microsoft.TemplateEngine.Cli.TemplateSearch;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;
using TemplateCreator = Microsoft.TemplateEngine.Edge.Template.TemplateCreator;

namespace Microsoft.TemplateEngine.Cli
{
    public class New3Command
    {
        private static readonly Guid _entryMutexGuid = new Guid("5CB26FD1-32DB-4F4C-B3DC-49CFD61633D2");
        private static Mutex? _entryMutex;
        private readonly ITelemetryLogger _telemetryLogger;
        private readonly CliTemplateEngineHost _host;
        private readonly TemplateCreator _templateCreator;
        private readonly TemplatePackageManager _templatePackageManager;
        private readonly TemplateInformationCoordinator _templateInformationCoordinator;
        private readonly AliasRegistry _aliasRegistry;
        private readonly IHostSpecificDataLoader _hostDataLoader;
        private readonly string? _defaultLanguage;
        private readonly New3Callbacks _callbacks;
        private readonly Func<string> _inputGetter = () => Console.ReadLine() ?? string.Empty;

        internal New3Command(INewCommandInput commandInput, ITemplateEngineHost host, ITelemetryLogger telemetryLogger, New3Callbacks callbacks, string? hivePath, bool virtualize = false)
        {
            _telemetryLogger = telemetryLogger;
            _host = new CliTemplateEngineHost(host, commandInput);
            EnvironmentSettings = new EngineEnvironmentSettings(_host, settingsLocation: hivePath, virtualizeSettings: virtualize);
            _templatePackageManager = new TemplatePackageManager(EnvironmentSettings);
            _templateCreator = new TemplateCreator(EnvironmentSettings);
            _aliasRegistry = new AliasRegistry(EnvironmentSettings);
            _hostDataLoader = new HostSpecificDataLoader(EnvironmentSettings);
            _callbacks = callbacks ?? new New3Callbacks();

            if (!EnvironmentSettings.Host.TryGetHostParamDefault("prefs:language", out _defaultLanguage))
            {
                _defaultLanguage = null;
            }
            _templateInformationCoordinator = new TemplateInformationCoordinator(EnvironmentSettings, _templatePackageManager, _templateCreator, _hostDataLoader, _telemetryLogger, _defaultLanguage);
        }

        internal EngineEnvironmentSettings EnvironmentSettings { get; private set; }

        /// <summary>
        /// Runs the command using <paramref name="host"/> and <paramref name="args"/>.
        /// </summary>
        /// <param name="commandName">Command name that is being executed.</param>
        /// <param name="host">The <see cref="ITemplateEngineHost"/> that executes the command.</param>
        /// <param name="telemetryLogger"><see cref="ITelemetryLogger"/> to use to track events.</param>
        /// <param name="callbacks">set of callbacks to be used, <see cref="New3Callbacks"/> for more details.</param>
        /// <param name="args">arguments to be run using template engine.</param>
        /// <param name="hivePath">(optional) the path to template engine settings to use.</param>
        /// <returns>exit code: 0 on success, other on error.</returns>
        /// <exception cref="CommandParserException">when <paramref name="args"/> cannot be parsed.</exception>
        public static int Run(string commandName, ITemplateEngineHost host, ITelemetryLogger telemetryLogger, New3Callbacks callbacks, string[] args, string? hivePath = null)
        {
            _ = host ?? throw new ArgumentNullException(nameof(host));
            _ = telemetryLogger ?? throw new ArgumentNullException(nameof(telemetryLogger));
            _ = callbacks ?? throw new ArgumentNullException(nameof(callbacks));
            _ = args ?? throw new ArgumentNullException(nameof(args));

            if (!args.Any(x => string.Equals(x, "--debug:ephemeral-hive")))
            {
                EnsureEntryMutex(hivePath, host);

                if (!_entryMutex!.WaitOne())
                {
                    return -1;
                }
            }

            try
            {
                return ActualRun(commandName, host, telemetryLogger, callbacks, args, hivePath);
            }
            finally
            {
                if (_entryMutex != null)
                {
                    _entryMutex.ReleaseMutex();
                }
            }
        }

        private static Mutex EnsureEntryMutex(string? hivePath, ITemplateEngineHost host)
        {
            if (_entryMutex == null)
            {
                string entryMutexIdentity;

                // this effectively mimics EngineEnvironmentSettings.BaseDir, which is not initialized when this is needed.
                if (!string.IsNullOrEmpty(hivePath))
                {
                    entryMutexIdentity = $"{_entryMutexGuid.ToString()}-{hivePath}".Replace("\\", "_").Replace("/", "_");
                }
                else
                {
                    entryMutexIdentity = $"{_entryMutexGuid.ToString()}-{host.HostIdentifier}-{host.Version}".Replace("\\", "_").Replace("/", "_");
                }

                _entryMutex = new Mutex(false, entryMutexIdentity);
            }

            return _entryMutex;
        }

        private static int ActualRun(string commandName, ITemplateEngineHost host, ITelemetryLogger telemetryLogger, New3Callbacks callbacks, string[] args, string? hivePath)
        {
            if (args.Any(x => string.Equals(x, "--debug:version", StringComparison.Ordinal)))
            {
                ShowVersion();
                return 0;
            }

            if (args.Any(x => string.Equals(x, "--debug:attach", StringComparison.Ordinal)))
            {
                Console.ReadLine();
            }

            int customHiveFlagIndex = args.ToList().IndexOf("--debug:custom-hive");
            if (customHiveFlagIndex >= 0)
            {
                if (customHiveFlagIndex + 1 >= args.Length)
                {
                    Reporter.Error.WriteLine("--debug:custom-hive requires 1 arg indicating the absolute or relative path to the custom hive".Bold().Red());
                    return 1;
                }

                hivePath = args[customHiveFlagIndex + 1];
                if (hivePath.StartsWith("-"))
                {
                    Reporter.Error.WriteLine("--debug:custom-hive requires 1 arg indicating the absolute or relative path to the custom hive".Bold().Red());
                    return 1;
                }

                hivePath = Path.GetFullPath(hivePath);
            }

            bool ephemeralHiveFlag = args.Any(x => string.Equals(x, "--debug:ephemeral-hive", StringComparison.Ordinal));

            if (args.Length == 0)
            {
                telemetryLogger.TrackEvent(commandName + TelemetryConstants.CalledWithNoArgsEventSuffix);
            }

            INewCommandInput commandInput = BaseCommandInput.Parse(args, commandName);
            New3Command instance = new New3Command(commandInput, host, telemetryLogger, callbacks, hivePath, virtualize: ephemeralHiveFlag);

            int result;
            try
            {
                using (Timing.Over(instance.EnvironmentSettings.Host.Logger, "Execute"))
                {
                    result = (int)Task.Run(() => instance.ExecuteAsync(commandInput)).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                AggregateException? ax = ex as AggregateException;

                while (ax != null && ax.InnerExceptions.Count == 1 && ax.InnerException is not null)
                {
                    ex = ax.InnerException;
                    ax = ex as AggregateException;
                }

                Reporter.Error.WriteLine(ex.Message.Bold().Red());

                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    ax = ex as AggregateException;

                    while (ax != null && ax.InnerExceptions.Count == 1 && ax.InnerException is not null)
                    {
                        ex = ax.InnerException;
                        ax = ex as AggregateException;
                    }

                    Reporter.Error.WriteLine(ex.Message.Bold().Red());
                }

                Reporter.Error.WriteLine(ex.StackTrace.Bold().Red());
                result = 1;
            }
            finally
            {
                instance._templatePackageManager.Dispose();
            }

            return result;
        }

        private static void ShowVersion()
        {
            Reporter.Output.WriteLine(LocalizableStrings.CommandDescription);
            Reporter.Output.WriteLine();
            int targetLength = Math.Max(LocalizableStrings.Version.Length, LocalizableStrings.CommitHash.Length);
            Reporter.Output.WriteLine($" {LocalizableStrings.Version.PadRight(targetLength)} {GitInfo.PackageVersion}");
            Reporter.Output.WriteLine($" {LocalizableStrings.CommitHash.PadRight(targetLength)} {GitInfo.CommitHash}");
        }

        private async Task<New3CommandStatus> EnterMaintenanceFlowAsync(INewCommandInput commandInput)
        {
            // dotnet new --list case
            if (commandInput.IsListFlagSpecified)
            {
                return await _templateInformationCoordinator.DisplayTemplateGroupListAsync(commandInput, default).ConfigureAwait(false);
            }

            // dotnet new -h case
            if (commandInput.IsHelpFlagSpecified)
            {
                _templateInformationCoordinator.ShowUsageHelp(commandInput);
                return New3CommandStatus.Success;
            }

            // No options specified - should information about dotnet new
            return await _templateInformationCoordinator.DisplayCommandDescriptionAsync(commandInput, default)
                .ConfigureAwait(false);
        }

        private async Task<New3CommandStatus> EnterTemplateManipulationFlowAsync(INewCommandInput commandInput)
        {
            if (commandInput.IsHelpFlagSpecified)
            {
                return await _templateInformationCoordinator.DisplayTemplateHelpAsync(commandInput, default).ConfigureAwait(false);
            }
            if (commandInput.IsListFlagSpecified)
            {
                return await _templateInformationCoordinator.DisplayTemplateGroupListAsync(commandInput, default).ConfigureAwait(false);
            }

            TemplateInvocationCoordinator invocationCoordinator = new TemplateInvocationCoordinator(
                EnvironmentSettings,
                _templatePackageManager,
                _templateInformationCoordinator,
                _hostDataLoader,
                _telemetryLogger,
                _defaultLanguage,
                _inputGetter,
                _callbacks);

            return await invocationCoordinator.CoordinateInvocationAsync(commandInput, default).ConfigureAwait(false);
        }

        private async Task<New3CommandStatus> ExecuteAsync(INewCommandInput commandInput)
        {
            if (commandInput is null)
            {
                throw new ArgumentNullException(nameof(commandInput));
            }

            if (!commandInput.ValidateParseError())
            {
                return New3CommandStatus.InvalidParamValues;
            }

            if (commandInput.IsHelpFlagSpecified)
            {
                _telemetryLogger.TrackEvent(commandInput.CommandName + TelemetryConstants.HelpEventSuffix);
            }

            if (commandInput.ShowAliasesSpecified)
            {
                return AliasSupport.DisplayAliasValues(EnvironmentSettings, commandInput, _aliasRegistry, commandInput.CommandName);
            }

            if (commandInput.ExpandedExtraArgsFiles && string.IsNullOrEmpty(commandInput.Alias))
            {
                // Only show this if there was no alias expansion.
                // ExpandedExtraArgsFiles must be checked before alias expansion - it'll get reset if there's an alias.
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.ExtraArgsCommandAfterExpansion, string.Join(" ", commandInput.Tokens)));
            }

            {
                // The --alias param is for creating / updating / deleting aliases.
                // If it's not present, try expanding aliases now.
                (New3CommandStatus aliasExpansionResult, INewCommandInput? expandedCommandInput) = AliasSupport.CoordinateAliasExpansion(commandInput, _aliasRegistry);

                if (aliasExpansionResult != New3CommandStatus.Success || expandedCommandInput == null)
                {
                    return aliasExpansionResult != New3CommandStatus.Success ? aliasExpansionResult : New3CommandStatus.AliasFailed;
                }
                commandInput = expandedCommandInput;
                _host.ResetCommand(commandInput);
            }

            if (!Initialize(commandInput))
            {
                return New3CommandStatus.Success;
            }

            bool forceCacheRebuild = commandInput.HasDebuggingFlag("--debug:rebuildcache");
            try
            {
                if (forceCacheRebuild)
                {
                    await _templatePackageManager.RebuildTemplateCacheAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (EngineInitializationException eiex)
            {
                Reporter.Error.WriteLine(eiex.Message.Bold().Red());
                Reporter.Error.WriteLine(LocalizableStrings.SettingsReadError);
                return New3CommandStatus.CreateFailed;
            }

            try
            {
                if (!string.IsNullOrEmpty(commandInput.Alias) && !commandInput.IsHelpFlagSpecified)
                {
                    return AliasSupport.ManipulateAliasIfValid(_aliasRegistry, commandInput.Alias, commandInput.Tokens.ToList(), await GetAllTemplateShortNamesAsync().ConfigureAwait(false));
                }

                if (TemplatePackageCoordinator.IsTemplatePackageManipulationFlow(commandInput))
                {
                    TemplatePackageCoordinator packageCoordinator = new TemplatePackageCoordinator(_telemetryLogger, EnvironmentSettings, _templatePackageManager, _templateInformationCoordinator, _defaultLanguage);
                    return await packageCoordinator.ProcessAsync(commandInput).ConfigureAwait(false);
                }

                if (commandInput.IsSearchFlagSpecified)
                {
                    return await CliTemplateSearchCoordinator.SearchForTemplateMatchesAsync(EnvironmentSettings, _templatePackageManager, commandInput, _defaultLanguage).ConfigureAwait(false);
                }

                if (string.IsNullOrWhiteSpace(commandInput.TemplateName))
                {
                    return await EnterMaintenanceFlowAsync(commandInput).ConfigureAwait(false);
                }

                return await EnterTemplateManipulationFlowAsync(commandInput).ConfigureAwait(false);
            }
            catch (TemplateAuthoringException tae)
            {
                Reporter.Error.WriteLine(tae.Message.Bold().Red());
                return New3CommandStatus.CreateFailed;
            }
        }

        private bool Initialize(INewCommandInput commandInput)
        {
            bool reinitFlag = commandInput.HasDebuggingFlag("--debug:reinit");
            if (reinitFlag)
            {
                EnvironmentSettings.Host.FileSystem.DirectoryDelete(EnvironmentSettings.Paths.HostVersionSettingsDir, true);
                EnvironmentSettings.Host.FileSystem.CreateDirectory(EnvironmentSettings.Paths.HostVersionSettingsDir);
            }

            if (commandInput.HasDebuggingFlag("--debug:showconfig"))
            {
                ShowConfig();
                return false;
            }

            return true;
        }

        private async Task<HashSet<string>> GetAllTemplateShortNamesAsync()
        {
            IReadOnlyList<ITemplateInfo> allTemplates = await _templatePackageManager.GetTemplatesAsync(default).ConfigureAwait(false);
            HashSet<string> allShortNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (ITemplateInfo info in allTemplates)
            {
                allShortNames.UnionWith(info.ShortNameList);
            }

            return allShortNames;
        }

        private void ShowConfig()
        {
            Reporter.Output.WriteLine(LocalizableStrings.CurrentConfiguration);
            Reporter.Output.WriteLine(" ");

            TableFormatter.Print(EnvironmentSettings.Components.OfType<IMountPointFactory>(), LocalizableStrings.NoItems, "   ", '-', new Dictionary<string, Func<IMountPointFactory, object>>
            {
                { LocalizableStrings.MountPointFactories, x => x.Id },
                { LocalizableStrings.Type, x => x.GetType().FullName ?? string.Empty },
                { LocalizableStrings.Assembly, x => x.GetType().GetTypeInfo().Assembly.FullName ?? string.Empty }
            });

            TableFormatter.Print(EnvironmentSettings.Components.OfType<IGenerator>(), LocalizableStrings.NoItems, "   ", '-', new Dictionary<string, Func<IGenerator, object>>
            {
                { LocalizableStrings.Generators, x => x.Id },
                { LocalizableStrings.Type, x => x.GetType().FullName ?? string.Empty },
                { LocalizableStrings.Assembly, x => x.GetType().GetTypeInfo().Assembly.FullName ?? string.Empty }
            });
        }
    }
}
