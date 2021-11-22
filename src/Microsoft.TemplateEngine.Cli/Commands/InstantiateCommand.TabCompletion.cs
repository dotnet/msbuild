// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal partial class InstantiateCommand : BaseCommand<InstantiateCommandArgs>
    {
        internal IEnumerable<string> GetSuggestions(
            InstantiateCommandArgs args,
            IEnumerable<TemplateGroup> templateGroups,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            string? textToMatch)
        {
            if (string.IsNullOrWhiteSpace(args.ShortName))
            {
                return templateGroups.SelectMany(g => g.ShortNames).Distinct();
            }
            else
            {
                HashSet<string> distinctSuggestions = new HashSet<string>();
                foreach (TemplateGroup templateGroup in templateGroups.Where(template => template.ShortNames.Contains(args.ShortName)))
                {
                    foreach (IGrouping<int, CliTemplateInfo> templateGrouping in templateGroup.Templates.GroupBy(g => g.Precedence).OrderByDescending(g => g.Key))
                    {
                        foreach (CliTemplateInfo template in templateGrouping)
                        {
                            TemplateCommand command = new TemplateCommand(this, environmentSettings, templatePackageManager, templateGroup, template);
                            Parser parser = ParserFactory.CreateTemplateParser(command);
                            ParseResult templateParseResult = parser.Parse(args.RemainingArguments ?? Array.Empty<string>());

                            //TODO:
                            // command.GetSuggestions does not work for arguments
                            // parseResult.GetSuggestions does not work with textToMatch, only position
                            // discuss options here
                            //foreach (string? suggestion in command.GetSuggestions(templateParseResult, textToMatch))
                            foreach (string? suggestion in templateParseResult.GetSuggestions())
                            {
                                if (!string.IsNullOrWhiteSpace(suggestion))
                                {
                                    distinctSuggestions.Add(suggestion);
                                }
                            }
                        }
                    }
                }
                return distinctSuggestions;
            }
        }

        protected internal override IEnumerable<string> GetSuggestions(ParseResult parseResult, IEngineEnvironmentSettings environmentSettings, string? textToMatch)
        {
            InstantiateCommandArgs args = ParseContext(parseResult);

            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            HostSpecificDataLoader? hostSpecificDataLoader = new HostSpecificDataLoader(environmentSettings);

            //TODO: consider new API to get templates only from cache (non async)
            IReadOnlyList<ITemplateInfo> templates =
                Task.Run(async () => await templatePackageManager.GetTemplatesAsync(default).ConfigureAwait(false)).GetAwaiter().GetResult();

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templates, hostSpecificDataLoader));

            return GetSuggestions(args, templateGroups, environmentSettings, templatePackageManager, textToMatch)
                .Concat(base.GetSuggestions(parseResult, environmentSettings, textToMatch));
        }
    }
}
