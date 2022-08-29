// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Invocation;
using System.Reflection;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Cli.TabularOutput;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;
using Command = System.CommandLine.Command;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal abstract class BaseCommand : Command
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
            IEngineEnvironmentSettings environmentSettings = new EngineEnvironmentSettings(
                _hostBuilder(parseResult),
                settingsLocation: args.DebugCustomSettingsLocation,
                virtualizeSettings: args.DebugVirtualizeSettings,
                environment: new CliEnvironment());
            return environmentSettings;
        }
    }

    internal abstract class BaseCommand<TArgs> : BaseCommand, ICommandHandler where TArgs : GlobalArgs
    {
        internal BaseCommand(
            Func<ParseResult, ITemplateEngineHost> hostBuilder,
            string name,
            string description)
            : base(hostBuilder, name, description)
        {
            this.Handler = this;
        }

        public async Task<int> InvokeAsync(InvocationContext context)
        {
            TArgs args = ParseContext(context.ParseResult);
            using IEngineEnvironmentSettings environmentSettings = CreateEnvironmentSettings(args, context.ParseResult);
            using TemplatePackageManager templatePackageManager = new(environmentSettings);
            CancellationToken cancellationToken = context.GetCancellationToken();

            NewCommandStatus returnCode;

            try
            {
                using (Timing.Over(environmentSettings.Host.Logger, "Execute"))
                {
                    await HandleGlobalOptionsAsync(args, environmentSettings, templatePackageManager, cancellationToken).ConfigureAwait(false);
                    returnCode = await ExecuteAsync(args, environmentSettings, templatePackageManager, context).ConfigureAwait(false);
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

        public int Invoke(InvocationContext context) => InvokeAsync(context).GetAwaiter().GetResult();

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

        protected abstract Task<NewCommandStatus> ExecuteAsync(TArgs args, IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager, InvocationContext context);

        protected abstract TArgs ParseContext(ParseResult parseResult);

        protected virtual Option GetFilterOption(FilterOptionDefinition def)
        {
            return def.OptionFactory();
        }

        protected IReadOnlyDictionary<FilterOptionDefinition, Option> SetupFilterOptions(IReadOnlyList<FilterOptionDefinition> filtersToSetup)
        {
            Dictionary<FilterOptionDefinition, Option> options = new();
            foreach (FilterOptionDefinition filterDef in filtersToSetup)
            {
                Option newOption = GetFilterOption(filterDef);
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

        protected static void PrintDeprecationMessage<TDepr, TNew>(ParseResult parseResult, Option? additionalOption = null) where TDepr : Command where TNew : Command
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
    }
}
