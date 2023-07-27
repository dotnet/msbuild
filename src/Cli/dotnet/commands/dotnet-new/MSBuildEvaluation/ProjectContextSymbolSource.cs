// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.Build.Evaluation;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Components;
using LocalizableStrings = Microsoft.DotNet.Tools.New.LocalizableStrings;

namespace Microsoft.TemplateEngine.MSBuildEvaluation
{

    /// <summary>
    /// Allows to bind symbols to MSBuild properties.
    /// </summary>
    internal class ProjectContextSymbolSource : IBindSymbolSource
    {
        public string DisplayName => LocalizableStrings.ProjectContextSymbolSource_DisplayName;

        public string? SourcePrefix => "msbuild";

        public int Priority => 200;

        public Guid Id => Guid.Parse("{9A839474-CE00-4430-A352-98967542B56B}");

        public bool RequiresPrefixMatch => true;

        public Task<string?> GetBoundValueAsync(IEngineEnvironmentSettings settings, string bindname, CancellationToken cancellationToken)
        {
            try
            {
                MSBuildEvaluator? evaluator = settings.Components.OfType<MSBuildEvaluator>().FirstOrDefault();

                if (evaluator == null)
                {
                    settings.Host.Logger.LogDebug("{0}: {1} component is not available, exiting.", nameof(ProjectContextSymbolSource), nameof(MSBuildEvaluator));
                    return Task.FromResult((string?)null);
                }

                MSBuildEvaluationResult evaluationResult = evaluator.EvaluateProject(settings);
                if (evaluationResult.EvaluatedProject == null)
                {
                    settings.Host.Logger.LogDebug("{0}: evaluation did not succeed, status: {1}, exiting.", nameof(ProjectContextSymbolSource), evaluationResult.Status);
                    return Task.FromResult((string?)null);
                }
                if (evaluationResult is NonSDKStyleEvaluationResult)
                {
                    settings.Host.Logger.LogDebug("The project {0} is not an SDK style project, and is not supported for evaluation.", evaluationResult.ProjectPath);
                    return Task.FromResult((string?)null);
                }

                string? propertyValue = evaluationResult.EvaluatedProject.GetProperty(bindname)?.EvaluatedValue;
                //we check only for null as property may exist with empty value
                if (propertyValue == null && evaluationResult is MultiTargetEvaluationResult multiTargetResult)
                {
                    foreach (Project? tfmBasedProject in multiTargetResult.EvaluatedProjects.Values)
                    {
                        propertyValue = evaluationResult.EvaluatedProject.GetProperty(bindname)?.EvaluatedValue;
                        if (propertyValue != null)
                        {
                            settings.Host.Logger.LogDebug("{0}: value for {1}: {2}.", nameof(ProjectContextSymbolSource), bindname, propertyValue);
                            Task.FromResult(propertyValue);
                        }
                    }
                }
                settings.Host.Logger.LogDebug("{0}: value for {1}: {2}.", nameof(ProjectContextSymbolSource), bindname, propertyValue ?? "<null>");
                return Task.FromResult(propertyValue);
            }
            catch (Exception e)
            {
                settings.Host.Logger.LogDebug("{0}: unexpected error during evaluation: {1}.", nameof(ProjectContextSymbolSource), e.Message);
                return Task.FromResult((string?)null);
            }
        }
    }
}
