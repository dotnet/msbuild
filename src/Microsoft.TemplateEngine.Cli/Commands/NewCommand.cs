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
    internal class NewCommand : BaseCommandHandler<NewCommandArgs>
    {
        private readonly string _commandName;

        internal NewCommand(string commandName, ITemplateEngineHost host, ITelemetryLogger logger, New3Callbacks callbacks) : base(host, logger, callbacks)
        {
            _commandName = commandName;
        }

        protected override Command CreateCommandAbstract()
        {
            Command command = new NewCommandWithSuggestions(_commandName, LocalizableStrings.CommandDescription);
            NewCommandArgs.AddToCommand(command);
            command.TreatUnmatchedTokensAsErrors = true;
            return command;
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

            TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);

            var templates = await templatePackageManager.GetTemplatesAsync(context.GetCancellationToken()).ConfigureAwait(false);
            var template = templates.FirstOrDefault(template => template.ShortNameList.Contains(args.ShortName));

            if (template == null)
            {
                Reporter.Error.WriteLine($"Template {args.ShortName} doesn't exist.");
                return New3CommandStatus.NotFound;
            }

            var dotnet = new Command("dotnet");
            var newC = new Command("new");
            var command = new Command(args.ShortName);
            command.Description = template.Name + Environment.NewLine + template.Description;
            command.AddOption(new Option<string>("-o"));
            foreach (var p in template.Parameters)
            {
                command.AddOption(new Option<string>($"--{p.Name}")
                {
                    Description = p.Description
                });
            }

            command.Handler = CommandHandler.Create<InvocationContext>((c) => Reporter.Output.WriteLine("Template is created!"));

            dotnet.AddCommand(newC);
            newC.AddCommand(command);

            string[] newArgs = new[] { "dotnet", "new", args.ShortName };
            newArgs = newArgs.Concat(args.Arguments?.ToArray() ?? Array.Empty<string>()).ToArray();

            return (New3CommandStatus)dotnet.Invoke(newArgs);
        }

        protected override NewCommandArgs ParseContext(ParseResult parseResult) => new(parseResult);

        /// <summary>
        /// Implements suggestions for "new" command.
        /// </summary>
        internal class NewCommandWithSuggestions : Command
        {
            public NewCommandWithSuggestions(string name, string? description = null) : base(name, description) { }

            public override IEnumerable<string> GetSuggestions(ParseResult? parseResult = null, string? textToMatch = null)
            {
                return base.GetSuggestions(parseResult, textToMatch);
            }
        }
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

        internal IReadOnlyList<string>? Arguments { get; }

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
            ShortNameArgument.AddSuggestions((parseResult, textToMatch) =>
            {
                // get list of all short names
                return Array.Empty<string>();
            });

            RemainingArguments.AddSuggestions((parseResult, textToMatch) =>
            {
                //decide if we do it here or in child class
                var shortName = parseResult?.ValueForArgument(ShortNameArgument);

                return Array.Empty<string>();
            });

            command.AddArgument(ShortNameArgument);
            command.AddArgument(RemainingArguments);
            LegacyInstallCommandArgs.AddOptionsToCommand(command);
            command.AddOption(HelpOption);
        }
    }
}
