// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateSearch.Common;

namespace Microsoft.TemplateEngine.Cli.TemplateSearch
{
    internal static class CliTemplateSearchCoordinatorFactory
    {
        internal static TemplateSearchCoordinator CreateCliTemplateSearchCoordinator(IEngineEnvironmentSettings environmentSettings, TemplatePackageManager templatePackageManager, INewCommandInput commandInput, string defaultLanguage)
        {
            return new TemplateSearchCoordinator(
                environmentSettings,
                templatePackageManager,
                commandInput.TemplateName,
                defaultLanguage,
                new CliHostSpecificDataMatchFilterFactory(commandInput).MatchFilter);
        }
    }
}
