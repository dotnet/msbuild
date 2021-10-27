// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Reflection;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal abstract class BaseCommand : Command
    {
        protected BaseCommand(string name, string? description = null) : base(name, description)
        {
        }

        internal Option<string?> DebugCustomSettingsLocationOption { get; } = new("--debug:custom-hive", "Sets custom settings location")
        {
            IsHidden = true
        };

        internal Option<bool> DebugVirtualizeSettingsOption { get; } = new("--debug:ephemeral-hive", "Use virtual settings")
        {
            IsHidden = true
        };

        internal Option<bool> DebugAttachOption { get; } = new("--debug:attach", "Allows to pause execution in order to attach to the process for debug purposes")
        {
            IsHidden = true
        };

        internal Option<bool> DebugReinitOption { get; } = new("--debug:reinit", "Resets the settings")
        {
            IsHidden = true
        };

        internal Option<bool> DebugRebuildCacheOption { get; } = new(new[] { "--debug:rebuild-cache", "--debug:rebuildcache" }, "Resets template cache")
        {
            IsHidden = true
        };

        internal Option<bool> DebugShowConfigOption { get; } = new(new[] { "--debug:show-config", "--debug:showconfig" }, "Shows the template engine config")
        {
            IsHidden = true
        };
    }

    internal abstract class BaseCommand<TArgs> : BaseCommand, ICommandHandler where TArgs : GlobalArgs
    {
        private static readonly Guid _entryMutexGuid = new Guid("5CB26FD1-32DB-4F4C-B3DC-49CFD61633D2");
        private readonly ITemplateEngineHost _host;

        internal BaseCommand(ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks, string name, string? description = null)
            : base(name, description)
        {
            _host = host;
            TelemetryLogger = logger;
            Callbacks = callbacks;
            this.Handler = this;

            this.AddOption(DebugCustomSettingsLocationOption);
            this.AddOption(DebugVirtualizeSettingsOption);
            this.AddOption(DebugAttachOption);
            this.AddOption(DebugReinitOption);
            this.AddOption(DebugRebuildCacheOption);
            this.AddOption(DebugShowConfigOption);
        }

        internal ITelemetryLogger TelemetryLogger { get; }

        internal NewCommandCallbacks Callbacks { get; }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            TArgs args = ParseContext(context.ParseResult);
            IEngineEnvironmentSettings environmentSettings = CreateEnvironmentSettings(args);

            CancellationToken cancellationToken = context.GetCancellationToken();

            using AsyncMutex? entryMutex = await EnsureEntryMutex(args, environmentSettings, cancellationToken).ConfigureAwait(false);

            try
            {
                //TODO:
                //if (commandInput.IsHelpFlagSpecified)
                //{
                //    _telemetryLogger.TrackEvent(commandInput.CommandName + TelemetryConstants.HelpEventSuffix);
                //}
                using (Timing.Over(environmentSettings.Host.Logger, "Execute"))
                {
                    await HandleGlobalOptionsAsync(args, environmentSettings, cancellationToken).ConfigureAwait(false);
                    return (int)await ExecuteAsync(args, environmentSettings, context).ConfigureAwait(false);
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
                return 1;
            }
        }

        public override IEnumerable<string> GetSuggestions(ParseResult? parseResult = null, string? textToMatch = null)
        {
            if (parseResult == null)
            {
                return base.GetSuggestions(parseResult, textToMatch);
            }
            TArgs args = ParseContext(parseResult);
            IEngineEnvironmentSettings environmentSettings = CreateEnvironmentSettings(args);
            return GetSuggestions(args, environmentSettings, textToMatch);
        }

        protected virtual IEnumerable<string> GetSuggestions(TArgs args, IEngineEnvironmentSettings environmentSettings, string? textToMatch)
        {
            return base.GetSuggestions(args.ParseResult, textToMatch);
        }

        protected IEngineEnvironmentSettings CreateEnvironmentSettings(TArgs args)
        {
            string? outputPath = (args as InstantiateCommandArgs)?.OutputPath;

            IEngineEnvironmentSettings environmentSettings = new EngineEnvironmentSettings(
                new CliTemplateEngineHost(_host, outputPath),
                settingsLocation: args.DebugCustomSettingsLocation,
                virtualizeSettings: args.DebugVirtualizeSettings,
                environment: new CliEnvironment());
            return environmentSettings;
        }

        protected abstract Task<NewCommandStatus> ExecuteAsync(TArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context);

        protected abstract TArgs ParseContext(ParseResult parseResult);

        protected virtual Option GetFilterOption(FilterOptionDefinition def)
        {
            return def.OptionFactory();
        }

        protected IReadOnlyDictionary<FilterOptionDefinition, Option> SetupFilterOptions(IReadOnlyList<FilterOptionDefinition> filtersToSetup)
        {
            Dictionary<FilterOptionDefinition, Option> options = new Dictionary<FilterOptionDefinition, Option>();
            foreach (var filterDef in filtersToSetup)
            {
                var newOption = GetFilterOption(filterDef);
                this.AddOption(newOption);
                options[filterDef] = newOption;
            }
            return options;
        }

        /// <summary>
        /// Adds the tabular output settings options for the command from <paramref name="command"/>.
        /// </summary>
        protected void SetupTabularOutputOptions(ITabularOutputCommand command)
        {
            this.AddOption(command.ColumnsAllOption);
            this.AddOption(command.ColumnsOption);
        }

        private static async Task<AsyncMutex?> EnsureEntryMutex(TArgs args, IEngineEnvironmentSettings environmentSettings, CancellationToken token)
        {
            // we don't need to acquire mutex in case of virtual settings
            if (args.DebugVirtualizeSettings)
            {
                return null;
            }
            string entryMutexIdentity = $"Global\\{_entryMutexGuid}_{environmentSettings.Paths.HostVersionSettingsDir.Replace("\\", "_").Replace("/", "_")}";
            return await AsyncMutex.WaitAsync(entryMutexIdentity, token).ConfigureAwait(false);
        }

        private static async Task HandleGlobalOptionsAsync(TArgs args, IEngineEnvironmentSettings environmentSettings, CancellationToken cancellationToken)
        {
            HandleDebugAttach(args);
            HandleDebugReinit(args, environmentSettings);
            await HandleDebugRebuildCacheAsync(args, environmentSettings, cancellationToken).ConfigureAwait(false);
            HandleDebugShowConfig(args, environmentSettings);
        }

        private static void HandleDebugAttach(TArgs args)
        {
            if (!args.DebugAttach)
            {
                return;
            }
            Reporter.Output.WriteLine("Attach to the process and press any key");
            Console.ReadLine();
        }

        private static void HandleDebugReinit(TArgs args, IEngineEnvironmentSettings environmentSettings)
        {
            if (!args.DebugReinit)
            {
                return;
            }
            environmentSettings.Host.FileSystem.DirectoryDelete(environmentSettings.Paths.HostVersionSettingsDir, true);
            environmentSettings.Host.FileSystem.CreateDirectory(environmentSettings.Paths.HostVersionSettingsDir);
        }

        private static Task HandleDebugRebuildCacheAsync(TArgs args, IEngineEnvironmentSettings environmentSettings, CancellationToken cancellationToken)
        {
            if (!args.DebugRebuildCache)
            {
                return Task.FromResult(0);
            }
            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            return templatePackageManager.RebuildTemplateCacheAsync(cancellationToken);
        }

        private static void HandleDebugShowConfig(TArgs args, IEngineEnvironmentSettings environmentSettings)
        {
            if (!args.DebugShowConfig)
            {
                return;
            }

            Reporter.Output.WriteLine(LocalizableStrings.CurrentConfiguration);
            Reporter.Output.WriteLine(" ");

            TableFormatter.Print(environmentSettings.Components.OfType<IMountPointFactory>(), LocalizableStrings.NoItems, "   ", '-', new Dictionary<string, Func<IMountPointFactory, object>>
            {
                { LocalizableStrings.MountPointFactories, x => x.Id },
                { LocalizableStrings.Type, x => x.GetType().FullName ?? string.Empty },
                { LocalizableStrings.Assembly, x => x.GetType().GetTypeInfo().Assembly.FullName ?? string.Empty }
            });

            TableFormatter.Print(environmentSettings.Components.OfType<IGenerator>(), LocalizableStrings.NoItems, "   ", '-', new Dictionary<string, Func<IGenerator, object>>
            {
                { LocalizableStrings.Generators, x => x.Id },
                { LocalizableStrings.Type, x => x.GetType().FullName ?? string.Empty },
                { LocalizableStrings.Assembly, x => x.GetType().GetTypeInfo().Assembly.FullName ?? string.Empty }
            });
        }
    }
}
