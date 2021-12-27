// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Completions;
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
        private readonly ITemplateEngineHost _host;

        protected BaseCommand(
            ITemplateEngineHost host,
            ITelemetryLogger logger,
            NewCommandCallbacks callbacks,
            string name,
            string description)
            : base(name, description)
        {
            _host = host;
            TelemetryLogger = logger;
            Callbacks = callbacks;
            this.AddOption(DebugCustomSettingsLocationOption);
            this.AddOption(DebugVirtualizeSettingsOption);
            this.AddOption(DebugAttachOption);
            this.AddOption(DebugReinitOption);
            this.AddOption(DebugRebuildCacheOption);
            this.AddOption(DebugShowConfigOption);
        }

        protected BaseCommand(BaseCommand baseCommand, string name, string description)
             : this(baseCommand._host, baseCommand.TelemetryLogger, baseCommand.Callbacks, name, description) { }

        internal Option<string?> DebugCustomSettingsLocationOption { get; } = new("--debug:custom-hive")
        {
            Description = SymbolStrings.Option_Debug_CustomSettings,
            IsHidden = true
        };

        internal Option<bool> DebugVirtualizeSettingsOption { get; } = new("--debug:ephemeral-hive")
        {
            Description = SymbolStrings.Option_Debug_VirtualSettings,
            IsHidden = true
        };

        internal Option<bool> DebugAttachOption { get; } = new("--debug:attach")
        {
            Description = SymbolStrings.Option_Debug_Attach,
            IsHidden = true
        };

        internal Option<bool> DebugReinitOption { get; } = new("--debug:reinit")
        {
            Description = SymbolStrings.Option_Debug_Reinit,
            IsHidden = true
        };

        internal Option<bool> DebugRebuildCacheOption { get; } = new(new[] { "--debug:rebuild-cache", "--debug:rebuildcache" })
        {
            Description = SymbolStrings.Option_Debug_RebuildCache,
            IsHidden = true
        };

        internal Option<bool> DebugShowConfigOption { get; } = new(new[] { "--debug:show-config", "--debug:showconfig" })
        {
            Description = SymbolStrings.Option_Debug_ShowConfig,
            IsHidden = true
        };

        internal ITelemetryLogger TelemetryLogger { get; }

        internal NewCommandCallbacks Callbacks { get; }

        protected IEngineEnvironmentSettings CreateEnvironmentSettings(GlobalArgs args, ParseResult parseResult)
        {
            //reparse to get output option if present
            //it's kept private so it is not reused for any other purpose except initializing host
            //for template instantiaton it has to be reparsed
            string? outputPath = ParseOutputOption(parseResult);
            IEngineEnvironmentSettings environmentSettings = new EngineEnvironmentSettings(
                new CliTemplateEngineHost(_host, outputPath),
                settingsLocation: args.DebugCustomSettingsLocation,
                virtualizeSettings: args.DebugVirtualizeSettings,
                environment: new CliEnvironment());
            return environmentSettings;
        }

        private static string? ParseOutputOption(ParseResult commandParseResult)
        {
            Option<string> outputOption = SharedOptionsFactory.CreateOutputOption();
            Command helperCommand = new Command("parse-output")
            {
                outputOption
            };

            ParseResult reparseResult = ParserFactory
                .CreateParser(helperCommand, disableHelp: true)
                .Parse(commandParseResult.Tokens.Select(t => t.Value).ToArray());

            return reparseResult.GetValueForOption<string>(outputOption);
        }

    }

    internal abstract class BaseCommand<TArgs> : BaseCommand, ICommandHandler where TArgs : GlobalArgs
    {
        internal BaseCommand(
            ITemplateEngineHost host,
            ITelemetryLogger logger,
            NewCommandCallbacks callbacks,
            string name,
            string description)
            : base(host, logger, callbacks, name, description)
        {
            this.Handler = this;
        }

        //command called via this constructor is not invokable
        internal BaseCommand(BaseCommand parent, string name, string description)
            : base(parent, name, description) { }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            TArgs args = ParseContext(context.ParseResult);
            IEngineEnvironmentSettings environmentSettings = CreateEnvironmentSettings(args, context.ParseResult);

            CancellationToken cancellationToken = context.GetCancellationToken();

            try
            {
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

        public override IEnumerable<CompletionItem> GetCompletions(CompletionContext context)
        {
            if (context.ParseResult == null)
            {
                return base.GetCompletions(context);
            }
            GlobalArgs args = new GlobalArgs(this, context.ParseResult);
            IEngineEnvironmentSettings environmentSettings = CreateEnvironmentSettings(args, context.ParseResult);
            return GetCompletions(context, environmentSettings);
        }

        protected internal virtual IEnumerable<CompletionItem> GetCompletions(CompletionContext context, IEngineEnvironmentSettings environmentSettings)
        {
            return base.GetCompletions(context);
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

        private static async Task HandleDebugRebuildCacheAsync(TArgs args, IEngineEnvironmentSettings environmentSettings, CancellationToken cancellationToken)
        {
            if (!args.DebugRebuildCache)
            {
                return;
            }
            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            //need to await, otherwise template package manager is disposed too early - before the task is completed
            await templatePackageManager.RebuildTemplateCacheAsync(cancellationToken).ConfigureAwait(true);
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
