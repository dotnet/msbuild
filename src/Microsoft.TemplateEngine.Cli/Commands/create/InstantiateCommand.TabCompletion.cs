// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.CommandLine.Completions;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal partial class InstantiateCommand : BaseCommand<InstantiateCommandArgs>
    {
        internal static IEnumerable<CompletionItem> GetTemplateNameCompletions(string? tempalteName, IEnumerable<TemplateGroup> templateGroups, IEngineEnvironmentSettings environmentSettings)
        {
            if (string.IsNullOrWhiteSpace(tempalteName))
            {
                return templateGroups
                    .SelectMany(g => g.ShortNames, (g, shortName) => new CompletionItem(shortName, documentation: g.Description))
                    .Distinct()
                    .OrderBy(c => c.Label, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            return templateGroups
                .SelectMany(g => g.ShortNames, (g, shortName) => new CompletionItem(shortName, documentation: g.Description))
                .Where(c => c.Label.StartsWith(tempalteName))
                .Distinct()
                .OrderBy(c => c.Label, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        internal static IEnumerable<CompletionItem> GetTemplateCompletions(
            InstantiateCommandArgs args,
            IEnumerable<TemplateGroup> templateGroups,
            IEngineEnvironmentSettings environmentSettings,
            TemplatePackageManager templatePackageManager,
            TextCompletionContext context)
        {
            HashSet<CompletionItem> distinctCompletions = new HashSet<CompletionItem>();
            foreach (TemplateGroup templateGroup in templateGroups.Where(template => template.ShortNames.Contains(args.ShortName)))
            {
                foreach (IGrouping<int, CliTemplateInfo> templateGrouping in templateGroup.Templates.GroupBy(g => g.Precedence).OrderByDescending(g => g.Key))
                {
                    foreach (CliTemplateInfo template in templateGrouping)
                    {
                        try
                        {
                            TemplateCommand command = new TemplateCommand(
                                args.Command,
                                environmentSettings,
                                templatePackageManager,
                                templateGroup,
                                template);

                            Parser parser = ParserFactory.CreateParser(command);

                            //it is important to pass raw text to get the completion
                            //completions for args passed as array are not supported
                            ParseResult parseResult = parser.Parse(context.CommandLineText);
                            foreach (CompletionItem completion in parseResult.GetCompletions(context.CursorPosition))
                            {
                                distinctCompletions.Add(completion);
                            }
                        }
                        catch (InvalidTemplateParametersException e)
                        {
                            Reporter.Error.WriteLine(string.Format(LocalizableStrings.GenericWarning, e.Message));
                        }
                    }
                }
            }
            return distinctCompletions.OrderBy(c => c.Label, StringComparer.OrdinalIgnoreCase);
        }

        protected internal override IEnumerable<CompletionItem> GetCompletions(CompletionContext context, IEngineEnvironmentSettings environmentSettings)
        {
            if (context is not TextCompletionContext textCompletionContext)
            {
                foreach (CompletionItem completion in base.GetCompletions(context, environmentSettings))
                {
                    yield return completion;
                }
                yield break;
            }

            InstantiateCommandArgs instantiateArgs = ParseContext(context.ParseResult);

            using TemplatePackageManager templatePackageManager = new TemplatePackageManager(environmentSettings);
            HostSpecificDataLoader? hostSpecificDataLoader = new HostSpecificDataLoader(environmentSettings);

            //TODO: consider new API to get templates only from cache (non async)
            IReadOnlyList<ITemplateInfo> templates =
                Task.Run(async () => await templatePackageManager.GetTemplatesAsync(default).ConfigureAwait(false)).GetAwaiter().GetResult();

            IEnumerable<TemplateGroup> templateGroups = TemplateGroup.FromTemplateList(CliTemplateInfo.FromTemplateInfo(templates, hostSpecificDataLoader));

            if (templateGroups.Any(template => template.ShortNames.Contains(instantiateArgs.ShortName)))
            {
                foreach (CompletionItem completion in GetTemplateCompletions(instantiateArgs, templateGroups, environmentSettings, templatePackageManager, textCompletionContext))
                {
                    yield return completion;
                }
                yield break;
            }
            foreach (CompletionItem completion in GetTemplateNameCompletions(instantiateArgs.ShortName, templateGroups, environmentSettings))
            {
                yield return completion;
            }
            foreach (CompletionItem completion in base.GetCompletions(context, environmentSettings))
            {
                yield return completion;
            }
        }
    }
}
