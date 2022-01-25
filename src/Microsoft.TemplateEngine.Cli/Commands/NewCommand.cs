// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal partial class NewCommand : BaseCommand<NewCommandArgs>, ICustomHelp
    {
        internal NewCommand(
            string commandName,
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            Func<ParseResult, ITelemetryLogger> telemetryLoggerBuilder,
            NewCommandCallbacks callbacks)
            : base(hostBuilder, telemetryLoggerBuilder, callbacks, commandName, SymbolStrings.Command_New_Description)
        {
            this.TreatUnmatchedTokensAsErrors = true;

            //it is important that legacy commands are built before non-legacy, as non legacy commands are building validators that rely on legacy stuff
            BuildLegacySymbols(hostBuilder, telemetryLoggerBuilder, callbacks);

            this.Add(new InstantiateCommand(hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new InstallCommand(this, hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new UninstallCommand(this, hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new UpdateCommand(this, hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new SearchCommand(this, hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new ListCommand(this, hostBuilder, telemetryLoggerBuilder, callbacks));
            this.Add(new AliasCommand(hostBuilder, telemetryLoggerBuilder, callbacks));

            this.AddGlobalOption(DebugCustomSettingsLocationOption);
            this.AddGlobalOption(DebugVirtualizeSettingsOption);
            this.AddGlobalOption(DebugAttachOption);
            this.AddGlobalOption(DebugReinitOption);
            this.AddGlobalOption(DebugRebuildCacheOption);
            this.AddGlobalOption(DebugShowConfigOption);
        }

        internal static Option<string?> DebugCustomSettingsLocationOption { get; } = new("--debug:custom-hive")
        {
            Description = SymbolStrings.Option_Debug_CustomSettings,
            IsHidden = true
        };

        internal static Option<bool> DebugVirtualizeSettingsOption { get; } = new(new[] { "--debug:ephemeral-hive", "--debug:virtual-hive" })
        {
            Description = SymbolStrings.Option_Debug_VirtualSettings,
            IsHidden = true
        };

        internal static Option<bool> DebugAttachOption { get; } = new("--debug:attach")
        {
            Description = SymbolStrings.Option_Debug_Attach,
            IsHidden = true
        };

        internal static Option<bool> DebugReinitOption { get; } = new("--debug:reinit")
        {
            Description = SymbolStrings.Option_Debug_Reinit,
            IsHidden = true
        };

        internal static Option<bool> DebugRebuildCacheOption { get; } = new(new[] { "--debug:rebuild-cache", "--debug:rebuildcache" })
        {
            Description = SymbolStrings.Option_Debug_RebuildCache,
            IsHidden = true
        };

        internal static Option<bool> DebugShowConfigOption { get; } = new(new[] { "--debug:show-config", "--debug:showconfig" })
        {
            Description = SymbolStrings.Option_Debug_ShowConfig,
            IsHidden = true
        };

        internal static Argument<string> ShortNameArgument { get; } = new Argument<string>("template-short-name")
        {
            Description = SymbolStrings.Command_Instantiate_Argument_ShortName,
            Arity = new ArgumentArity(0, 1),
            IsHidden = true
        };

        internal static Argument<string[]> RemainingArguments { get; } = new Argument<string[]>("template-args")
        {
            Description = SymbolStrings.Command_Instantiate_Argument_TemplateOptions,
            Arity = new ArgumentArity(0, 999),
            IsHidden = true
        };

        protected internal override IEnumerable<CompletionItem> GetCompletions(CompletionContext context, IEngineEnvironmentSettings environmentSettings)
        {
            if (context is not TextCompletionContext textCompletionContext)
            {
                foreach (CompletionItem completion in base.GetCompletions(context, environmentSettings))
                {
                    yield return completion;
                }
                yield break;
            }

            InstantiateCommandArgs instantiateCommandArgs = InstantiateCommandArgs.FromNewCommandArgs(ParseContext(context.ParseResult));

            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            HostSpecificDataLoader? hostSpecificDataLoader = new HostSpecificDataLoader(environmentSettings);

            //TODO: consider new API to get templates only from cache (non async)
            IReadOnlyList<ITemplateInfo> templates =
                Task.Run(async () => await templatePackageManager.GetTemplatesAsync(default).ConfigureAwait(false)).GetAwaiter().GetResult();

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templates, hostSpecificDataLoader));

            if (templateGroups.Any(template => template.ShortNames.Contains(instantiateCommandArgs.ShortName)))
            {
                foreach (CompletionItem completion in InstantiateCommand.GetTemplateCompletions(instantiateCommandArgs, templateGroups, environmentSettings, templatePackageManager, textCompletionContext))
                {
                    yield return completion;
                }
                yield break;
            }

            foreach (CompletionItem completion in InstantiateCommand.GetTemplateNameCompletions(instantiateCommandArgs.ShortName, templateGroups, environmentSettings))
            {
                yield return completion;
            }
            foreach (CompletionItem completion in base.GetCompletions(context, environmentSettings))
            {
                yield return completion;
            }
        }

        protected override Task<NewCommandStatus> ExecuteAsync(
            NewCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            ITelemetryLogger telemetryLogger,
            InvocationContext context)
        {
            return InstantiateCommand.ExecuteAsync(args, environmentSettings, telemetryLogger, context);
        }

        protected override NewCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }
}

