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

        internal NewCommand(string commandName, ITemplateEngineHost host, ITelemetryLogger telemetryLogger, NewCommandCallbacks callbacks) : base(host, telemetryLogger, callbacks, commandName)
        {
            _commandName = commandName;
            NewCommandArgs.AddToCommand(this);
            this.TreatUnmatchedTokensAsErrors = true;
            this.Description = LocalizableStrings.CommandDescription;

            this.Add(new InstantiateCommand(host, telemetryLogger, callbacks));
            this.Add(new InstallCommand(host, telemetryLogger, callbacks));
            this.Add(new LegacyInstallCommand(host, telemetryLogger, callbacks));
            //yield return (host, telemetryLogger, callbacks) => new ListCommand(host, telemetryLogger, callbacks);
            //yield return (host, telemetryLogger, callbacks) => new SearchCommand(host, telemetryLogger, callbacks);
            //yield return (host, telemetryLogger, callbacks) => new UninstallCommand(host, telemetryLogger, callbacks);
            //yield return (host, telemetryLogger, callbacks) => new UpdateCommand(host, telemetryLogger, callbacks);
            //yield return (host, telemetryLogger, callbacks) => new AddAliasCommand(host, telemetryLogger, callbacks);
            //yield return (host, telemetryLogger, callbacks) => new ShowAliasCommand(host, telemetryLogger, callbacks);
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

        protected override NewCommandArgs ParseContext(ParseResult parseResult) => new(parseResult);

    }
}
