// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal partial class NewCommand : BaseCommand<NewCommandArgs>, ICustomHelp
    {
        internal NewCommand(
            string commandName,
            Func<ParseResult, ITemplateEngineHost> hostBuilder)
            : base(hostBuilder, commandName, SymbolStrings.Command_New_Description)
        {
            TreatUnmatchedTokensAsErrors = true;

            //it is important that legacy commands are built before non-legacy, as non legacy commands are building validators that rely on legacy stuff
            BuildLegacySymbols(hostBuilder);

            Add(new InstantiateCommand(this, hostBuilder));
            Add(new InstallCommand(this, hostBuilder));
            Add(new UninstallCommand(this, hostBuilder));
            Add(new UpdateCommand(this, hostBuilder));
            Add(new SearchCommand(this, hostBuilder));
            Add(new ListCommand(this, hostBuilder));
            Add(new AliasCommand(hostBuilder));
            Add(new DetailsCommand(hostBuilder));

            Options.Add(DebugCustomSettingsLocationOption);
            Options.Add(DebugVirtualizeSettingsOption);
            Options.Add(DebugAttachOption);
            Options.Add(DebugReinitOption);
            Options.Add(DebugRebuildCacheOption);
            Options.Add(DebugShowConfigOption);

            Options.Add(SharedOptions.OutputOption);
            Options.Add(SharedOptions.NameOption);
            Options.Add(SharedOptions.DryRunOption);
            Options.Add(SharedOptions.ForceOption);
            Options.Add(SharedOptions.NoUpdateCheckOption);
            Options.Add(SharedOptions.ProjectPathOption);
        }

        internal static CliOption<string?> DebugCustomSettingsLocationOption { get; } = new("--debug:custom-hive")
        {
            Description = SymbolStrings.Option_Debug_CustomSettings,
            Hidden = true,
            Recursive = true
        };

        internal static CliOption<bool> DebugVirtualizeSettingsOption { get; } = new("--debug:ephemeral-hive", "--debug:virtual-hive")
        {
            Description = SymbolStrings.Option_Debug_VirtualSettings,
            Hidden = true,
            Recursive = true
        };

        internal static CliOption<bool> DebugAttachOption { get; } = new("--debug:attach")
        {
            Description = SymbolStrings.Option_Debug_Attach,
            Hidden = true,
            Recursive = true
        };

        internal static CliOption<bool> DebugReinitOption { get; } = new("--debug:reinit")
        {
            Description = SymbolStrings.Option_Debug_Reinit,
            Hidden = true,
            Recursive = true
        };

        internal static CliOption<bool> DebugRebuildCacheOption { get; } = new("--debug:rebuild-cache", "--debug:rebuildcache")
        {
            Description = SymbolStrings.Option_Debug_RebuildCache,
            Hidden = true,
            Recursive = true
        };

        internal static CliOption<bool> DebugShowConfigOption { get; } = new("--debug:show-config", "--debug:showconfig")
        {
            Description = SymbolStrings.Option_Debug_ShowConfig,
            Hidden = true,
            Recursive = true
        };

        internal static CliArgument<string> ShortNameArgument { get; } = new("template-short-name")
        {
            Description = SymbolStrings.Command_Instantiate_Argument_ShortName,
            Arity = new ArgumentArity(0, 1),
            Hidden = true
        };

        internal static CliArgument<string[]> RemainingArguments { get; } = new("template-args")
        {
            Description = SymbolStrings.Command_Instantiate_Argument_TemplateOptions,
            Arity = new ArgumentArity(0, 999),
            Hidden = true
        };

        internal IReadOnlyList<CliOption> PassByOptions { get; } = new CliOption[]
        {
            SharedOptions.ForceOption,
            SharedOptions.NameOption,
            SharedOptions.DryRunOption,
            SharedOptions.NoUpdateCheckOption
        };

        protected internal override IEnumerable<CompletionItem> GetCompletions(CompletionContext context, IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager)
        {
            if (context is not TextCompletionContext textCompletionContext)
            {
                foreach (CompletionItem completion in base.GetCompletions(context, environmentSettings, templatePackageManager))
                {
                    yield return completion;
                }
                yield break;
            }

            InstantiateCommandArgs instantiateCommandArgs = InstantiateCommandArgs.FromNewCommandArgs(ParseContext(context.ParseResult));
            HostSpecificDataLoader? hostSpecificDataLoader = new(environmentSettings);

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
            foreach (CompletionItem completion in base.GetCompletions(context, environmentSettings, templatePackageManager))
            {
                yield return completion;
            }
        }

        protected override Task<NewCommandStatus> ExecuteAsync(
            NewCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            ParseResult parseResult,
            CancellationToken cancellationToken)
        {
            return InstantiateCommand.ExecuteAsync(args, environmentSettings, templatePackageManager, parseResult, cancellationToken);
        }

        protected override NewCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }
}

