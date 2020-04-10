using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Microsoft.TemplateEngine.Cli.HelpAndUsage;
using Microsoft.TemplateEngine.Cli.TemplateSearch;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Edge.Template;
using Microsoft.TemplateSearch.Common;
using Microsoft.TemplateSearch.Common.TemplateUpdate;
using static Microsoft.TemplateEngine.Cli.TemplateListResolutionResult;

namespace Microsoft.TemplateEngine.Cli
{
    internal class TemplateInvocationAndAcquisitionCoordinator
    {
        private readonly SettingsLoader _settingsLoader;
        private readonly IEngineEnvironmentSettings _environment;
        private readonly INewCommandInput _commandInput;
        private readonly TemplateCreator _templateCreator;
        private readonly IHostSpecificDataLoader _hostDataLoader;
        private readonly ITelemetryLogger _telemetryLogger;
        private readonly string _defaultLanguage;
        private readonly string _commandName;
        private readonly Func<string> _inputGetter;
        private readonly New3Callbacks _callbacks;

        private bool _resolutionResultInitialized = false;
        TemplateListResolutionResult _templateResolutionResult;
        ITemplateMatchInfo _templateToInvoke;
        SingularInvokableMatchCheckStatus _singleMatchStatus;

        public TemplateInvocationAndAcquisitionCoordinator(SettingsLoader settingsLoader, INewCommandInput commandInput, TemplateCreator templateCreator, IHostSpecificDataLoader hostDataLoader, ITelemetryLogger telemetryLogger, string defaultLanguage, string commandName, Func<string> inputGetter, New3Callbacks callbacks)
        {
            _settingsLoader = settingsLoader;
            _environment = _settingsLoader.EnvironmentSettings;
            _commandInput = commandInput;
            _templateCreator = templateCreator;
            _hostDataLoader = hostDataLoader;
            _telemetryLogger = telemetryLogger;
            _defaultLanguage = defaultLanguage;
            _commandName = commandName;
            _inputGetter = inputGetter;
            _callbacks = callbacks;
        }

        public async Task<CreationResultStatus> CoordinateInvocationOrAcquisitionAsync()
        {
            EnsureTemplateResolutionResult();

            if (_templateToInvoke != null)
            {
                // invoke and then check for updates
                CreationResultStatus creationResult = await InvokeTemplateAsync();
                // check for updates on this template (pack)
                await CheckForTemplateUpdateAsync();

                return creationResult;
            }
            else
            {
                // The command didn't resolve to an installed template. Search for something that does.
                bool anySearchMatches = await SearchForTemplateMatchesAsync();

                if (!anySearchMatches)
                {
                    return HelpForTemplateResolution.CoordinateHelpAndUsageDisplay(_templateResolutionResult, _environment, _commandInput, _hostDataLoader, _telemetryLogger, _templateCreator, _defaultLanguage);
                }
                else
                {
                    return CreationResultStatus.Success;
                }
            }
        }

        // Return true if there are any matches, false otherwise.
        private async Task<bool> SearchForTemplateMatchesAsync()
        {
            TemplateSearchCoordinator searchCoordinator = CliTemplateSearchCoordinatorFactory.CreateCliTemplateSearchCoordinator(_environment, _commandInput, _defaultLanguage);
            SearchResults searchResults = await searchCoordinator.SearchAsync();

            if (searchResults.AnySources)
            {
                // Only show the searching online message if there are sources to search.
                // It's a bit out of order to do the search first, then display the message.
                // But there's no way to know whether or not there are sources without searching.
                // ...theoretically the search source initialization is separate from the search, but the initialization is most of the work.
                Reporter.Output.WriteLine(LocalizableStrings.SearchingOnlineNotification.Bold().Red());
            }
            else
            {
                return false;
            }

            foreach (TemplateSourceSearchResult sourceResult in searchResults.MatchesBySource)
            {
                string sourceHeader = string.Format(LocalizableStrings.SearchResultSourceIndicator, sourceResult.SourceDisplayName);

                Reporter.Output.WriteLine(sourceHeader);
                Reporter.Output.WriteLine(new string('-', sourceHeader.Length));

                foreach (TemplatePackSearchResult matchesForPack in sourceResult.PacksWithMatches.Values)
                {
                    DisplayResultsForPack(matchesForPack);
                    Reporter.Output.WriteLine();
                }
            }

            return searchResults.MatchesBySource.Count > 0;
        }

        private void DisplayResultsForPack(TemplatePackSearchResult matchesForPack)
        {
            HashSet<string> seenGroupIdentities = new HashSet<string>();

            foreach (ITemplateMatchInfo templateMatch in matchesForPack.TemplateMatches)
            {
                // only display one template in the pack for each group.
                // if the group identity is blank, we assume it's a new template. 
                if (string.IsNullOrEmpty(templateMatch.Info.GroupIdentity) || seenGroupIdentities.Add(templateMatch.Info.GroupIdentity))
                {
                    // TODO: get the Pack authoring info plumbed through - this will require changes to the scraper output
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.SearchResultTemplateInfo, templateMatch.Info.Name, templateMatch.Info.ShortName, templateMatch.Info.Author, matchesForPack.PackInfo.Name));
                }
            }

            Reporter.Output.WriteLine(LocalizableStrings.SearchResultInstallHeader);
            string fullyQualifiedPackName = $"{matchesForPack.PackInfo.Name}::{matchesForPack.PackInfo.Version}";
            Reporter.Output.WriteLine(string.Format(LocalizableStrings.SearchResultInstallCommand, _commandInput.CommandName, fullyQualifiedPackName));
        }

        private async Task<CreationResultStatus> InvokeTemplateAsync()
        {
            TemplateInvoker invoker = new TemplateInvoker(_environment, _commandInput, _telemetryLogger, _commandName, _inputGetter, _callbacks);
            return await invoker.InvokeTemplate(_templateToInvoke);
        }

        // check for updates for the matched template, based on the Identity
        private async Task CheckForTemplateUpdateAsync()
        {
            if (!_settingsLoader.InstallUnitDescriptorCache.TryGetDescriptorForTemplate(_templateToInvoke.Info, out IInstallUnitDescriptor descriptor))
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.InstallDescriptor_NotFound, _templateToInvoke.Info.Identity));
                return;
            }

            List<IInstallUnitDescriptor> descriptorList = new List<IInstallUnitDescriptor>() { descriptor };
            TemplateUpdateChecker updateChecker = new TemplateUpdateChecker(_environment);
            IUpdateCheckResult updateCheckResult = await updateChecker.CheckForUpdatesAsync(descriptorList);

            if (updateCheckResult.Updates.Count == 0)
            {
                return;
            }
            else if (updateCheckResult.Updates.Count == 1)
            {
                DisplayUpdateMessage(updateCheckResult.Updates[0]);
            }
            else
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.UpdateCheck_UnknownError, descriptor.Identifier));
            }
        }

        private void DisplayUpdateMessage(IUpdateUnitDescriptor updateDescriptor)
        {
            Reporter.Output.WriteLine();
            Reporter.Output.WriteLine(string.Format(LocalizableStrings.UpdateAvailable, updateDescriptor.UpdateDisplayInfo));
            Reporter.Output.WriteLine(string.Format(LocalizableStrings.UpdateCheck_InstallCommand, _commandName, updateDescriptor.InstallString));
        }

        private void EnsureTemplateResolutionResult()
        {
            if (_resolutionResultInitialized)
            {
                return;
            }

            _templateResolutionResult = TemplateListResolver.GetTemplateResolutionResult(_settingsLoader.UserTemplateCache.TemplateInfo, _hostDataLoader, _commandInput, _defaultLanguage);
            _singleMatchStatus = SingularInvokableMatchCheckStatus.None;

            // If any template in the group has any ambiguous params, it's not invokable.
            // The check for HasAmbiguousParameterValueMatch is for an example like:
            // "dotnet new mvc -f netcore"
            //      - '-f netcore' is ambiguous in the 1.x version (2 begins-with matches)
            //      - '-f netcore' is not ambiguous in the 2.x version (1 begins-with match)
            if (!_templateResolutionResult.TryGetUnambiguousTemplateGroupToUse(out IReadOnlyList<ITemplateMatchInfo> unambiguousTemplateGroup)
                || !_templateResolutionResult.TryGetSingularInvokableMatch(out _templateToInvoke, out _singleMatchStatus)
                || unambiguousTemplateGroup.Any(x => x.HasParameterMismatch())
                || unambiguousTemplateGroup.Any(x => x.HasAmbiguousParameterValueMatch()))
            {
                _templateToInvoke = null;

                if (_singleMatchStatus == SingularInvokableMatchCheckStatus.AmbiguousChoice)
                {
                    _environment.Host.LogDiagnosticMessage(LocalizableStrings.Authoring_AmbiguousChoiceParameterValue, "Authoring");
                }
                else if (_singleMatchStatus == SingularInvokableMatchCheckStatus.AmbiguousPrecedence)
                {
                    _environment.Host.LogDiagnosticMessage(LocalizableStrings.Authoring_AmbiguousBestPrecedence, "Authoring");
                }
            }

            _resolutionResultInitialized = true;
        }
    }
}
