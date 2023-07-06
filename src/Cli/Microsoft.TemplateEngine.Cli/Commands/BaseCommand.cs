// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Completions;
using System.Reflection;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Cli.TabularOutput;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal abstract class BaseCommand : CliCommand
    {
        private readonly Func<ParseResult, ITemplateEngineHost> _hostBuilder;

        protected BaseCommand(
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            string name,
            string description)
            : base(name, description)
        {
            _hostBuilder = hostBuilder;
        }

        protected internal virtual IEnumerable<CompletionItem> GetCompletions(CompletionContext context, IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager)
        {
#pragma warning disable SA1100 // Do not prefix calls with base unless local implementation exists
            return base.GetCompletions(context);
#pragma warning restore SA1100 // Do not prefix calls with base unless local implementation exists
        }

        protected IEngineEnvironmentSettings CreateEnvironmentSettings(GlobalArgs args, ParseResult parseResult)
        {
            ITemplateEngineHost host = _hostBuilder(parseResult);
            IEnvironment environment = new CliEnvironment();

            return new EngineEnvironmentSettings(
                host,
                virtualizeSettings: args.DebugVirtualizeSettings,
                environment: environment,
                pathInfo: new CliPathInfo(host, environment, args.DebugCustomSettingsLocation));
        }
    }

    internal abstract class BaseCommand<TArgs> : BaseCommand where TArgs : GlobalArgs
    {
        internal BaseCommand(
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            string name,
            string description)
            : base(hostBuilder, name, description)
        {
            Action = new CommandAction(this);
        }

        public override IEnumerable<CompletionItem> GetCompletions(CompletionContext context)
        {
            if (context.ParseResult == null)
            {
                return base.GetCompletions(context);
            }
            GlobalArgs args = new(this, context.ParseResult);
            using IEngineEnvironmentSettings environmentSettings = CreateEnvironmentSettings(args, context.ParseResult);
            using TemplatePackageManager templatePackageManager = new(environmentSettings);
            return GetCompletions(context, environmentSettings, templatePackageManager).ToList();
        }

        /// <summary>
        /// Checks if the template with same short name as used command alias exists, and if so prints the example on how to run the template using dotnet new create.
        /// </summary>
        /// <remarks>
        /// This method uses <see cref="TemplatePackageManager.GetTemplatesAsync(CancellationToken)"/>, however this should not take long as templates normally at least once
        /// are queried before and results are cached.
        /// Alternatively we can think of caching template groups early in <see cref="BaseCommand{TArgs}"/> later on.
        /// </remarks>
        protected internal static async Task CheckTemplatesWithSubCommandName(
            TArgs args,
            TemplatePackageManager templatePackageManager,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<ITemplateInfo> availableTemplates = await templatePackageManager.GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
            string usedCommandAlias = args.ParseResult.CommandResult.IdentifierToken.Value;
            if (!availableTemplates.Any(t => t.ShortNameList.Any(sn => string.Equals(sn, usedCommandAlias, StringComparison.OrdinalIgnoreCase))))
            {
                return;
            }

            Reporter.Output.WriteLine(LocalizableStrings.Commands_TemplateShortNameCommandConflict_Info, usedCommandAlias);
            Reporter.Output.WriteCommand(Example.For<InstantiateCommand>(args.ParseResult).WithArgument(InstantiateCommand.ShortNameArgument, usedCommandAlias));
            Reporter.Output.WriteLine();
        }

        protected static void PrintDeprecationMessage<TDepr, TNew>(ParseResult parseResult, CliOption? additionalOption = null)
            where TDepr : CliCommand
            where TNew : CliCommand
        {
            var newCommandExample = Example.For<TNew>(parseResult);
            if (additionalOption != null)
            {
                newCommandExample.WithOption(additionalOption);
            }

            Reporter.Output.WriteLine(string.Format(
             LocalizableStrings.Commands_Warning_DeprecatedCommand,
             Example.For<TDepr>(parseResult),
             newCommandExample).Yellow());

            Reporter.Output.WriteLine(LocalizableStrings.Commands_Warning_DeprecatedCommand_Info.Yellow());
            Reporter.Output.WriteCommand(Example.For<TNew>(parseResult).WithHelpOption().ToString().Yellow());
            Reporter.Output.WriteLine();
        }

        protected abstract Task<NewCommandStatus> ExecuteAsync(TArgs args, IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager, ParseResult parseResult, CancellationToken cancellationToken);

        protected abstract TArgs ParseContext(ParseResult parseResult);

        protected virtual CliOption GetFilterOption(FilterOptionDefinition def)
        {
            return def.OptionFactory();
        }

        protected IReadOnlyDictionary<FilterOptionDefinition, CliOption> SetupFilterOptions(IReadOnlyList<FilterOptionDefinition> filtersToSetup)
        {
            Dictionary<FilterOptionDefinition, CliOption> options = new();
            foreach (FilterOptionDefinition filterDef in filtersToSetup)
            {
                CliOption newOption = GetFilterOption(filterDef);
                this.Options.Add(newOption);
                options[filterDef] = newOption;
            }
            return options;
        }

        /// <summary>
        /// Adds the tabular output settings options for the command from <paramref name="command"/>.
        /// </summary>
        protected void SetupTabularOutputOptions(ITabularOutputCommand command)
        {
            this.Options.Add(command.ColumnsAllOption);
            this.Options.Add(command.ColumnsOption);
        }

        private static async Task HandleGlobalOptionsAsync(
            TArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            CancellationToken cancellationToken)
        {
            HandleDebugAttach(args);
            HandleDebugReinit(args, environmentSettings);
            await HandleDebugRebuildCacheAsync(args, templatePackageManager, cancellationToken).ConfigureAwait(false);
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

        private static Task HandleDebugRebuildCacheAsync(TArgs args, TemplatePackageManager templatePackageManager, CancellationToken cancellationToken)
        {
            if (!args.DebugRebuildCache)
            {
                return Task.CompletedTask;
            }
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

            TabularOutput<IMountPointFactory> mountPointsFormatter =
                    TabularOutput.TabularOutput
                        .For(
                            new TabularOutputSettings(environmentSettings.Environment),
                            environmentSettings.Components.OfType<IMountPointFactory>())
                        .DefineColumn(mp => mp.Id.ToString(), LocalizableStrings.MountPointFactories, showAlways: true)
                        .DefineColumn(mp => mp.GetType().FullName ?? string.Empty, LocalizableStrings.Type, showAlways: true)
                        .DefineColumn(mp => mp.GetType().GetTypeInfo().Assembly.FullName ?? string.Empty, LocalizableStrings.Assembly, showAlways: true);
            Reporter.Output.WriteLine(mountPointsFormatter.Layout());
            Reporter.Output.WriteLine();

            TabularOutput<IGenerator> generatorsFormatter =
              TabularOutput.TabularOutput
                  .For(
                      new TabularOutputSettings(environmentSettings.Environment),
                      environmentSettings.Components.OfType<IGenerator>())
                  .DefineColumn(g => g.Id.ToString(), LocalizableStrings.Generators, showAlways: true)
                  .DefineColumn(g => g.GetType().FullName ?? string.Empty, LocalizableStrings.Type, showAlways: true)
                  .DefineColumn(g => g.GetType().GetTypeInfo().Assembly.FullName ?? string.Empty, LocalizableStrings.Assembly, showAlways: true);
            Reporter.Output.WriteLine(generatorsFormatter.Layout());
            Reporter.Output.WriteLine();
        }

        private sealed class CommandAction : CliAction
        {
            private readonly BaseCommand<TArgs> _command;

            public CommandAction(BaseCommand<TArgs> command) => _command = command;

            public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken)
            {
                TArgs args = _command.ParseContext(parseResult);
                using IEngineEnvironmentSettings environmentSettings = _command.CreateEnvironmentSettings(args, parseResult);
                using TemplatePackageManager templatePackageManager = new(environmentSettings);

                NewCommandStatus returnCode;

                try
                {
                    using (Timing.Over(environmentSettings.Host.Logger, "Execute"))
                    {
                        await HandleGlobalOptionsAsync(args, environmentSettings, templatePackageManager, cancellationToken).ConfigureAwait(false);
                        returnCode = await _command.ExecuteAsync(args, environmentSettings, templatePackageManager, parseResult, cancellationToken).ConfigureAwait(false);
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

                    if (!string.IsNullOrWhiteSpace(ex.StackTrace))
                    {
                        Reporter.Error.WriteLine(ex.StackTrace.Bold().Red());
                    }
                    returnCode = NewCommandStatus.Unexpected;
                }

                if (returnCode != NewCommandStatus.Success)
                {
                    Reporter.Error.WriteLine();
                    Reporter.Error.WriteLine(LocalizableStrings.BaseCommand_ExitCodeHelp, (int)returnCode);
                }

                return (int)returnCode;
            }

            public override int Invoke(ParseResult parseResult) => InvokeAsync(parseResult, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
