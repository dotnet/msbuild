// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal partial class NewCommand : BaseCommand<NewCommandArgs>
    {
        internal NewCommand(string commandName, ITemplateEngineHost host, ITelemetryLogger telemetryLogger, NewCommandCallbacks callbacks) : base(host, telemetryLogger, callbacks, commandName, LocalizableStrings.CommandDescription)
        {
            this.TreatUnmatchedTokensAsErrors = true;

            //it is important that legacy commands are built before non-legacy, as non legacy commands are building validators that rely on legacy stuff
            BuildLegacySymbols(host, telemetryLogger, callbacks);

            this.Add(new InstantiateCommand(host, telemetryLogger, callbacks));
            this.Add(new InstallCommand(this, host, telemetryLogger, callbacks));
            this.Add(new UninstallCommand(this, host, telemetryLogger, callbacks));
            this.Add(new UpdateCommand(this, host, telemetryLogger, callbacks));
            this.Add(new SearchCommand(this, host, telemetryLogger, callbacks));
            this.Add(new ListCommand(this, host, telemetryLogger, callbacks));
        }

        //TODO: this option is needed to intercept help. Discuss if there is a better option to do it.
        internal Option HelpOption { get; } = new Option(new string[] { "-h", "--help", "-?" })
        {
            IsHidden = true
        };

        protected override IEnumerable<string> GetSuggestions(NewCommandArgs args, IEngineEnvironmentSettings environmentSettings, string? textToMatch)
        {
            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            var templates = templatePackageManager.GetTemplatesAsync(CancellationToken.None).Result;

            return Array.Empty<string>();

            //TODO: implement correct logic
            //if (!string.IsNullOrEmpty(args.ShortName))
            //{
            //    var matchingTemplates = templates.Where(template => template.ShortNameList.Contains(args.ShortName));
            //    HashSet<string> distinctSuggestions = new HashSet<string>();

            //    foreach (var template in matchingTemplates)
            //    {
            //        var templateGroupCommand = new TemplateGroupCommand(this, environmentSettings, template);
            //        var parsed = templateGroupCommand.Parse(args.Arguments ?? Array.Empty<string>());
            //        foreach (var suggestion in templateGroupCommand.GetSuggestions(parsed, textToMatch))
            //        {
            //            if (distinctSuggestions.Add(suggestion))
            //            {
            //                yield return suggestion;
            //            }
            //        }
            //    }
            //    yield break;
            //}
            //else
            //{
            //    foreach (var template in templates)
            //    {
            //        foreach (var suggestion in template.ShortNameList)
            //        {
            //            yield return suggestion;
            //        }
            //    }
            //}

            //foreach (var suggestion in base.GetSuggestions(args, environmentSettings, textToMatch))
            //{
            //    yield return suggestion;
            //}
        }

        protected override Task<NewCommandStatus> ExecuteAsync(NewCommandArgs args, IEngineEnvironmentSettings environmentSettings, InvocationContext context)
        {
            InstantiateCommand command = InstantiateCommand.FromNewCommand(this);
            ParseResult reparseResult = ParserFactory.CreateTemplateParser(command).Parse(args.Tokens);
            return command.ExecuteAsync(reparseResult, environmentSettings, context);
        }

        protected override NewCommandArgs ParseContext(ParseResult parseResult) => new(this, parseResult);
    }
}

