using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal static class CliTemplateSearchCoordinatorFactory
    {
        internal static TemplateSearchCoordinator CreateCliTemplateSearchCoordinator(IEngineEnvironmentSettings environmentSettings, INewCommandInput commandInput, string defaultLanguage)
        {
            return new TemplateSearchCoordinator(environmentSettings,
                                            commandInput.TemplateName,
                                            defaultLanguage,
                                            new CliHostSpecificDataMatchFilterFactory(commandInput).MatchFilter);
        }
    }
}
