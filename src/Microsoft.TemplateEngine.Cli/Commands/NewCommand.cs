// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal class NewCommand : BaseCommand<NewCommandArgs>
    {
        private readonly string _commandName;

        internal NewCommand(string commandName, ITemplateEngineHost host, ITelemetryLogger logger, New3Callbacks callbacks)
            : base(host, logger, callbacks, commandName)
        {
            _commandName = commandName;
            NewCommandArgs.AddToCommand(this);
            this.TreatUnmatchedTokensAsErrors = true;
            this.Description = LocalizableStrings.CommandDescription;
        }

        protected override IEnumerable<string> GetSuggestions(NewCommandArgs args, IEngineEnvironmentSettings environmentSettings, string? textToMatch)
        {
            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            var templates = templatePackageManager.GetTemplatesAsync(CancellationToken.None).Result;

            if (!string.IsNullOrEmpty(args.ShortName))
            {
                var template = templates.FirstOrDefault(template => template.ShortNameList.Contains(args.ShortName));

                if (template != null)
                {
                    var templateGroupCommand = new TemplateGroupCommand(this, environmentSettings, template);
                    var parsed = templateGroupCommand.Parse(args.Arguments ?? Array.Empty<string>());
                    return templateGroupCommand.GetSuggestions(parsed, textToMatch);
                }
            }

            return base.GetSuggestions(args, environmentSettings, textToMatch);
        }

        protected override async Task<New3CommandStatus> ExecuteAsync(NewCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context)
        {
            if (string.IsNullOrWhiteSpace(args.ShortName))
            {
                if (args.HelpRequested)
                {
                    HelpResult helpResult = new HelpResult();
                    helpResult.Apply(context);
                    return New3CommandStatus.Success;
                }
                //show curated list
                return New3CommandStatus.Success;
            }

            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            var templates = await templatePackageManager.GetTemplatesAsync(context.GetCancellationToken()).ConfigureAwait(false);
            var template = templates.FirstOrDefault(template => template.ShortNameList.Contains(args.ShortName));

            if (template == null)
            {
                Reporter.Error.WriteLine($"Template {args.ShortName} doesn't exist.");
                return New3CommandStatus.NotFound;
            }

            //var dotnet = new Command("dotnet")
            //{
            //    TreatUnmatchedTokensAsErrors = false
            //};
            var newC = new Command(_commandName)
            {
                TreatUnmatchedTokensAsErrors = false
            };
            //dotnet.AddCommand(newC);
            newC.AddCommand(new TemplateGroupCommand(this, environmentSettings, template));

            return (New3CommandStatus)newC.Invoke(context.ParseResult.Tokens.Select(s => s.Value).ToArray());
        }

        protected override NewCommandArgs ParseContext(ParseResult parseResult) => new(parseResult);
    }

    internal partial class NewCommandArgs : GlobalArgs
    {
        public NewCommandArgs(ParseResult parseResult) : base(parseResult)
        {
            Arguments = parseResult.ValueForArgument(RemainingArguments);
            ShortName = parseResult.ValueForArgument(ShortNameArgument);
            HelpRequested = parseResult.ValueForOption(HelpOption);
        }

        internal string? ShortName { get; }

        internal string[]? Arguments { get; }

        internal bool HelpRequested { get; }

        private static Argument<string> ShortNameArgument { get; } = new Argument<string>("template-short-name")
        {
            Arity = new ArgumentArity(0, 1)
        };

        private static Argument<string[]> RemainingArguments { get; } = new Argument<string[]>("template-args")
        {
            Arity = new ArgumentArity(0, 999)
        };

        private static Option<bool> HelpOption { get; } = new Option<bool>(new string[] { "-h", "--help", "-?" });

        internal static void AddToCommand(Command command)
        {
            command.AddArgument(ShortNameArgument);
            command.AddArgument(RemainingArguments);
            LegacyInstallCommandArgs.AddOptionsToCommand(command);
            command.AddOption(HelpOption);
        }
    }
}
