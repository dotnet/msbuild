// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.Extensions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class InstantiateCommand : BaseCommand<InstantiateCommandArgs>
    {
        private NewCommand? _parentCommand;

        internal InstantiateCommand(ITemplateEngineHost host, ITelemetryLogger logger, NewCommandCallbacks callbacks) : base(host, logger, callbacks, "create", LocalizableStrings.CommandDescriptionCreate)
        {
            this.AddArgument(ShortNameArgument);
            this.AddArgument(RemainingArguments);
            this.AddOption(HelpOption);
            IsHidden = true;
        }

        private InstantiateCommand(NewCommand parentCommand, string name, string? description = null) : base(parentCommand, name, description)
        {
            _parentCommand = parentCommand;
            this.AddArgument(ShortNameArgument);
            this.AddArgument(RemainingArguments);
            this.AddOption(HelpOption);
        }

        internal Argument<string> ShortNameArgument { get; } = new Argument<string>("template-short-name")
        {
            Arity = new ArgumentArity(0, 1)
        };

        internal Argument<string[]> RemainingArguments { get; } = new Argument<string[]>("template-args")
        {
            Arity = new ArgumentArity(0, 999)
        };

        internal Option HelpOption { get; } = new Option(new string[] { "-h", "--help", "-?" })
        {
            IsHidden = true
        };

        internal static InstantiateCommand FromNewCommand(NewCommand parentCommand)
        {
            InstantiateCommand command = new InstantiateCommand(parentCommand, parentCommand.Name, parentCommand.Description);
            //subcommands are re-added just for the sake of proper help display
            foreach (var subcommand in parentCommand.Children.OfType<Command>())
            {
                command.Add(subcommand);
            }
            return command;
        }

        internal Task<NewCommandStatus> ExecuteAsync(ParseResult parseResult, IEngineEnvironmentSettings environmentSettings, InvocationContext context)
        {
            return ExecuteAsync(ParseContext(parseResult), environmentSettings, context);
        }

        internal HashSet<TemplateCommand> GetTemplateCommand(
                InstantiateCommandArgs args,
                IEngineEnvironmentSettings environmentSettings,
                TemplatePackageManager templatePackageManager,
                TemplateGroup templateGroup)
        {
            //groups templates in the group by precedence
            foreach (IGrouping<int, CliTemplateInfo> templateGrouping in templateGroup.Templates.GroupBy(g => g.Precedence).OrderByDescending(g => g.Key))
            {
                HashSet<TemplateCommand> candidates = ReparseForTemplate(
                    args,
                    environmentSettings,
                    templatePackageManager,
                    templateGroup,
                    templateGrouping,
                    out bool languageOptionSpecified);

                //if no candidates continue with next precedence
                if (!candidates.Any())
                {
                    continue;
                }
                //if language option is not specified, we do not need to do reparsing for default language
                if (languageOptionSpecified || string.IsNullOrWhiteSpace(environmentSettings.GetDefaultLanguage()))
                {
                    return candidates;
                }

                // try to reparse for default language
                return ReparseForDefaultLanguage(
                    args,
                    environmentSettings,
                    templatePackageManager,
                    templateGroup,
                    candidates);
            }
            return new HashSet<TemplateCommand>();
        }

        protected async override Task<NewCommandStatus> ExecuteAsync(InstantiateCommandArgs instantiateArgs, IEngineEnvironmentSettings environmentSettings, InvocationContext context)
        {
            //TODO: revise this output
            //help doesn't have argumentes defined at the moment though should
            //help for dotnet new should have both arguments and commands present
            if (string.IsNullOrWhiteSpace(instantiateArgs.ShortName) && instantiateArgs.HelpRequested)
            {
                context.HelpBuilder.Write(
                    instantiateArgs.ParseResult.CommandResult.Command,
                    StandardStreamWriter.Create(context.Console.Out),
                    instantiateArgs.ParseResult);
                return NewCommandStatus.Success;
            }

            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            var hostSpecificDataLoader = new HostSpecificDataLoader(environmentSettings);
            if (string.IsNullOrWhiteSpace(instantiateArgs.ShortName))
            {
                TemplateListCoordinator templateListCoordinator = new TemplateListCoordinator(
                    environmentSettings,
                    templatePackageManager,
                    hostSpecificDataLoader,
                    TelemetryLogger);

                return await templateListCoordinator.DisplayCommandDescriptionAsync(instantiateArgs, default).ConfigureAwait(false);
            }

            var templates = await templatePackageManager.GetTemplatesAsync(context.GetCancellationToken()).ConfigureAwait(false);
            var templateGroups = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templates, hostSpecificDataLoader));

            //TODO: decide what to do if there are more than 1 group.
            var selectedTemplateGroup = templateGroups.FirstOrDefault(template => template.ShortNames.Contains(instantiateArgs.ShortName));

            if (selectedTemplateGroup == null)
            {
                Reporter.Error.WriteLine(
                    string.Format(LocalizableStrings.NoTemplatesMatchingInputParameters, instantiateArgs.ShortName).Bold().Red());
                Reporter.Error.WriteLine();

                Reporter.Error.WriteLine(LocalizableStrings.ListTemplatesCommand);
                Reporter.Error.WriteCommand(CommandExamples.ListCommandExample(instantiateArgs.CommandName));

                Reporter.Error.WriteLine(LocalizableStrings.SearchTemplatesCommand);
                Reporter.Error.WriteCommand(CommandExamples.SearchCommandExample(instantiateArgs.CommandName, instantiateArgs.ShortName));
                Reporter.Error.WriteLine();
                return NewCommandStatus.NotFound;
            }
            return await HandleTemplateInstantationAsync(instantiateArgs, environmentSettings, templatePackageManager, selectedTemplateGroup).ConfigureAwait(false);
        }

        protected override InstantiateCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);

        private async Task<NewCommandStatus> HandleTemplateInstantationAsync(
            InstantiateCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            TemplateGroup templateGroup)
        {
            HashSet<TemplateCommand> candidates = GetTemplateCommand(args, environmentSettings, templatePackageManager, templateGroup);
            if (candidates.Count == 1)
            {
                Command commandToRun = _parentCommand is null ? this : _parentCommand;

                commandToRun.AddCommand(candidates.First());
                return (NewCommandStatus)await commandToRun.InvokeAsync(args.TokensToInvoke).ConfigureAwait(false);
            }
            else if (candidates.Any())
            {
                return HandleAmbuguousResult();
            }

            //TODO: handle it better
            Reporter.Error.WriteLine(
                string.Format(LocalizableStrings.NoTemplatesMatchingInputParameters, args.ShortName).Bold().Red());
            Reporter.Error.WriteLine();

            Reporter.Error.WriteLine(LocalizableStrings.ListTemplatesCommand);
            Reporter.Error.WriteCommand(CommandExamples.ListCommandExample(args.CommandName));

            Reporter.Error.WriteLine(LocalizableStrings.SearchTemplatesCommand);
            Reporter.Error.WriteCommand(CommandExamples.SearchCommandExample(args.CommandName, args.ShortName));
            Reporter.Error.WriteLine();
            return NewCommandStatus.NotFound;
        }

        private NewCommandStatus HandleAmbuguousResult() => throw new NotImplementedException();

        private HashSet<TemplateCommand> ReparseForTemplate(
            InstantiateCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            TemplateGroup templateGroup,
            IEnumerable<CliTemplateInfo> templatesToReparse,
            out bool languageOptionSpecified)
        {
            languageOptionSpecified = false;
            HashSet<TemplateCommand> candidates = new HashSet<TemplateCommand>();
            foreach (CliTemplateInfo template in templatesToReparse)
            {
                TemplateCommand command = new TemplateCommand(this, environmentSettings, templatePackageManager, templateGroup, template);
                Parser parser = ParserFactory.CreateTemplateParser(command);
                ParseResult parseResult = parser.Parse(args.RemainingArguments ?? Array.Empty<string>());

                languageOptionSpecified = command.LanguageOption != null
                    && parseResult.FindResultFor(command.LanguageOption) != null;
                if (!parseResult.Errors.Any())
                {
                    candidates.Add(command);
                }
            }
            return candidates;
        }

        private HashSet<TemplateCommand> ReparseForDefaultLanguage(
            InstantiateCommandArgs args,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            TemplateGroup templateGroup,
            HashSet<TemplateCommand> candidates)
        {
            HashSet<TemplateCommand> languageAwareCandidates = new HashSet<TemplateCommand>();
            foreach (var templateCommand in candidates)
            {
                TemplateCommand command = new TemplateCommand(
                    this,
                    environmentSettings,
                    templatePackageManager,
                    templateGroup,
                    templateCommand.Template,
                    buildDefaultLanguageValidation: true);
                Parser parser = ParserFactory.CreateTemplateParser(command);
                ParseResult parseResult = parser.Parse(args.RemainingArguments ?? Array.Empty<string>());

                if (!parseResult.Errors.Any())
                {
                    languageAwareCandidates.Add(command);
                }
            }
            return languageAwareCandidates.Any()
                ? languageAwareCandidates
                : candidates;
        }

    }

    internal class InstantiateCommandArgs : GlobalArgs
    {
        public InstantiateCommandArgs(InstantiateCommand command, ParseResult parseResult) : base(command, parseResult)
        {
            RemainingArguments = parseResult.GetValueForArgument(command.RemainingArguments) ?? Array.Empty<string>();
            ShortName = parseResult.GetValueForArgument(command.ShortNameArgument);
            HelpRequested = parseResult.GetValueForOption<bool>(command.HelpOption);

            var tokens = new List<string>();
            if (!string.IsNullOrWhiteSpace(ShortName))
            {
                tokens.Add(ShortName);
            }
            tokens.AddRange(RemainingArguments);
            if (HelpRequested)
            {
                tokens.Add(command.HelpOption.Aliases.First());
            }
            TokensToInvoke = tokens.ToArray();

        }

        internal string? ShortName { get; }

        internal string[] RemainingArguments { get; }

        internal bool HelpRequested { get; }

        internal string[] TokensToInvoke { get; }
    }
}
