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

        internal NewCommand(string commandName, ITemplateEngineHost host, ITelemetryLogger telemetryLogger, NewCommandCallbacks callbacks) : base(host, telemetryLogger, callbacks, commandName, LocalizableStrings.CommandDescription)
        {
            _commandName = commandName;

            this.AddArgument(ShortNameArgument);
            this.AddArgument(RemainingArguments);
            this.AddOption(HelpOption);

            this.TreatUnmatchedTokensAsErrors = true;

            this.Add(new InstantiateCommand(host, telemetryLogger, callbacks));
            LegacyInstallCommand legacyInstall = new LegacyInstallCommand(this, host, telemetryLogger, callbacks);
            this.Add(legacyInstall);
            this.Add(new InstallCommand(legacyInstall, host, telemetryLogger, callbacks));
            //yield return (host, telemetryLogger, callbacks) => new ListCommand(host, telemetryLogger, callbacks);
            //yield return (host, telemetryLogger, callbacks) => new SearchCommand(host, telemetryLogger, callbacks);
            //yield return (host, telemetryLogger, callbacks) => new UninstallCommand(host, telemetryLogger, callbacks);
            //yield return (host, telemetryLogger, callbacks) => new UpdateCommand(host, telemetryLogger, callbacks);
            //yield return (host, telemetryLogger, callbacks) => new AliasCommand(host, telemetryLogger, callbacks);
        }

        internal Argument<string> ShortNameArgument { get; } = new Argument<string>("template-short-name")
        {
            Arity = new ArgumentArity(0, 1)
        };

        internal Argument<string[]> RemainingArguments { get; } = new Argument<string[]>("template-args")
        {
            Arity = new ArgumentArity(0, 999)
        };

        internal Option<bool> HelpOption { get; } = new Option<bool>(new string[] { "-h", "--help", "-?" });

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

        protected override async Task<NewCommandStatus> ExecuteAsync(NewCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context)
        {
            if (string.IsNullOrWhiteSpace(args.ShortName))
            {
                if (args.HelpRequested)
                {
                    HelpResult helpResult = new HelpResult();
                    helpResult.Apply(context);
                    return NewCommandStatus.Success;
                }
                //show curated list
                return NewCommandStatus.Success;
            }

            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            var templates = await templatePackageManager.GetTemplatesAsync(context.GetCancellationToken()).ConfigureAwait(false);
            var template = templates.FirstOrDefault(template => template.ShortNameList.Contains(args.ShortName));

            if (template == null)
            {
                Reporter.Error.WriteLine($"Template {args.ShortName} doesn't exist.");
                return NewCommandStatus.NotFound;
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

            return (NewCommandStatus)newC.Invoke(context.ParseResult.Tokens.Select(s => s.Value).ToArray());
        }

        protected override NewCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);

    }
}
