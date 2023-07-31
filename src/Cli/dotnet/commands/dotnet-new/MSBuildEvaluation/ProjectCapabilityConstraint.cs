// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.Build.Evaluation;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Microsoft.TemplateEngine.Cli.Commands;
using Newtonsoft.Json.Linq;
using LocalizableStrings = Microsoft.DotNet.Tools.New.LocalizableStrings;

namespace Microsoft.TemplateEngine.MSBuildEvaluation
{
    internal class ProjectCapabilityConstraintFactory : ITemplateConstraintFactory
    {
        public string Type => "project-capability";

        public Guid Id => Guid.Parse("{85F3EFFB-315F-4F2F-AA0A-99EABE3B6FB3}");

        public Task<ITemplateConstraint> CreateTemplateConstraintAsync(IEngineEnvironmentSettings environmentSettings, CancellationToken cancellationToken)
        {
            MSBuildEvaluator? evaluator = environmentSettings.Components.OfType<MSBuildEvaluator>().FirstOrDefault();

            if (evaluator == null)
            {
                environmentSettings.Host.Logger.LogDebug("{0}: '{1}' component is not available.", nameof(ProjectCapabilityConstraintFactory), nameof(MSBuildEvaluator));
                throw new Exception(string.Format(LocalizableStrings.ProjectCapabilityConstraintFactory_Exception_NoEvaluator, Type, nameof(MSBuildEvaluator)));
            }

            try
            {
                MSBuildEvaluationResult evaluationResult = evaluator.EvaluateProject(environmentSettings);
                return Task.FromResult((ITemplateConstraint)new ProjectCapabilityConstraint(environmentSettings, this, evaluationResult));
            }
            catch (Exception e)
            {
                environmentSettings.Host.Logger.LogDebug("{0}: Failed to evaluate the project: {1}.", nameof(ProjectCapabilityConstraintFactory), e.Message);
                throw new Exception(string.Format(LocalizableStrings.ProjectCapabilityConstraintFactory_Exception_EvaluationFailed, Type, e.Message), e);
            }

        }

        internal class ProjectCapabilityConstraint : ITemplateConstraint
        {
            private readonly IEngineEnvironmentSettings _environmentSettings;
            private readonly ITemplateConstraintFactory _factory;
            private readonly MSBuildEvaluationResult _evaluationResult;
            private readonly IReadOnlyList<string> _projectCapabilities;
            private readonly ILogger _logger;

            internal ProjectCapabilityConstraint(IEngineEnvironmentSettings environmentSettings, ITemplateConstraintFactory factory, MSBuildEvaluationResult evaluationResult)
            {
                _environmentSettings = environmentSettings;
                _logger = _environmentSettings.Host.LoggerFactory.CreateLogger(nameof(MSBuildEvaluator));
                _factory = factory;
                _evaluationResult = evaluationResult;
                _projectCapabilities = GetProjectCapabilities(evaluationResult);
            }

            public string Type => _factory.Type;

            public string DisplayName => LocalizableStrings.ProjectCapabilityConstraint_DisplayName;

            public TemplateConstraintResult Evaluate(string? args)
            {
                if (string.IsNullOrWhiteSpace(args))
                {
                    throw new ArgumentException($"{nameof(args)} cannot be null or whitespace.", nameof(args));
                }

                _logger.LogDebug("Configuration: '{0}'", args);
                JToken? token;
                try
                {
                    token = JToken.Parse(args!);
                }
                catch (Exception e)
                {
                    _logger.LogDebug("Failed to parse configuration: '{0}', reason: {1}", args, e.Message);
                    throw new Exception($"{LocalizableStrings.ProjectCapabilityConstraint_Error_InvalidConstraintConfiguration}:{LocalizableStrings.ProjectCapabilityConstraint_Error_InvalidJson}.", e);
                }

                string configuredCapabiltiesExpression;

                if (token.Type == JTokenType.String)
                {
                    string? configuredCapability = token.Value<string>();
                    if (string.IsNullOrWhiteSpace(configuredCapability))
                    {
                        _logger.LogDebug("Invalid configuration: '{0}', reason: arguments should not contain empty values.", args);
                        throw new Exception($"{LocalizableStrings.ProjectCapabilityConstraint_Error_InvalidConstraintConfiguration}: {LocalizableStrings.ProjectCapabilityConstraint_Error_ArgumentShouldNotBeEmpty}.");
                    }
                    configuredCapabiltiesExpression = configuredCapability;
                }
                else
                {
                    _logger.LogDebug("Invalid configuration: '{0}', reason: argument should be a string.", args);
                    throw new Exception($"{LocalizableStrings.ProjectCapabilityConstraint_Error_InvalidConstraintConfiguration}: {LocalizableStrings.ProjectCapabilityConstraint_Error_ArgumentShouldBeString}.");
                }

                if (_evaluationResult.Status == MSBuildEvaluationResult.EvalStatus.NoProjectFound)
                {
                    _logger.LogDebug("No project found. This template can only be created inside the project.");
                    return TemplateConstraintResult.CreateRestricted(
                        this,
                        _evaluationResult.ErrorMessage ?? LocalizableStrings.MSBuildEvaluationResult_Error_NoProjectFound,
                        LocalizableStrings.ProjectCapabilityConstraint_Restricted_NoProjectFound_CTA);
                }
                if (_evaluationResult.Status == MSBuildEvaluationResult.EvalStatus.MultipleProjectFound)
                {
                    string foundProjects = string.Join("; ", (_evaluationResult as MultipleProjectsEvaluationResult)?.ProjectPaths ?? (IReadOnlyList<string?>)new[] { _evaluationResult.ProjectPath });
                    _logger.LogDebug("Multiple projects found: {0}, specify the project to use.", foundProjects);
                    return TemplateConstraintResult.CreateRestricted(
                        this,
                        _evaluationResult.ErrorMessage ?? string.Format(LocalizableStrings.MultipleProjectsEvaluationResult_Error, foundProjects),
                        string.Format(LocalizableStrings.ProjectCapabilityConstraint_Restricted_MultipleProjectsFound_CTA, SharedOptions.ProjectPathOption.Name));
                }
                if (_evaluationResult.Status == MSBuildEvaluationResult.EvalStatus.NoRestore)
                {
                    _logger.LogDebug("The project is not restored. Run 'dotnet restore {0}' to restore the project.", _evaluationResult.ProjectPath);
                    return TemplateConstraintResult.CreateRestricted(
                        this,
                        _evaluationResult.ErrorMessage ?? string.Format(LocalizableStrings.MSBuildEvaluationResult_Error_NotRestored, _evaluationResult.ProjectPath),
                        string.Format(LocalizableStrings.ProjectCapabilityConstraint_Restricted_NotRestored_CTA, _evaluationResult.ProjectPath));
                }
                if (_evaluationResult.Status == MSBuildEvaluationResult.EvalStatus.Failed || _evaluationResult.Status == MSBuildEvaluationResult.EvalStatus.NotEvaluated || _evaluationResult.EvaluatedProject == null)
                {
                    _logger.LogDebug("Failed to evaluate project context: {0}", _evaluationResult.ErrorMessage);
                    return TemplateConstraintResult.CreateRestricted(this, string.Format(LocalizableStrings.ProjectCapabilityConstraint_Restricted_EvaluationFailed_Message, _evaluationResult.ErrorMessage));
                }
                if (_evaluationResult is NonSDKStyleEvaluationResult)
                {
                    _logger.LogDebug("The project {0} is not an SDK style project, and is not supported for evaluation.", _evaluationResult.ProjectPath);
                    return TemplateConstraintResult.CreateRestricted(this, string.Format(LocalizableStrings.ProjectCapabilityConstraint_Restricted_NonSDKStyle_Message, _evaluationResult.ProjectPath));
                }

                try
                {
                    _logger.LogDebug("Evaluating '{0}' on '{1}' set.", configuredCapabiltiesExpression, string.Join(", ", _projectCapabilities));
                    if (!CapabilityExpressionEvaluator.Evaluate(configuredCapabiltiesExpression, _projectCapabilities))
                    {
                        _logger.LogDebug("Expression evaluated to 'false'.");
                        return TemplateConstraintResult.CreateRestricted(
                            this,
                            string.Format(LocalizableStrings.ProjectCapabilityConstraint_Restricted_Message, configuredCapabiltiesExpression, _evaluationResult.ProjectPath));
                    }
                    _logger.LogDebug("Expression evaluated to 'true'.");
                    return TemplateConstraintResult.CreateAllowed(this);
                }
                catch (ArgumentException ae)
                {
                    _logger.LogDebug("Invalid expression '{0}'.", configuredCapabiltiesExpression);
                    throw new Exception($"{LocalizableStrings.ProjectCapabilityConstraint_Error_InvalidConstraintConfiguration}:{ae.Message}.", ae);
                }
            }

            private static IReadOnlyList<string> GetProjectCapabilities(MSBuildEvaluationResult result)
            {
                HashSet<string> capabilities = new(StringComparer.OrdinalIgnoreCase);
                AddProjectCapabilities(capabilities, result.EvaluatedProject);

                //in case of multi-target project, consider project capabilities for all target frameworks
                if (result is MultiTargetEvaluationResult multiTargetResult)
                {
                    foreach (Project? tfmBasedEvaluation in multiTargetResult.EvaluatedProjects.Values)
                    {
                        AddProjectCapabilities(capabilities, tfmBasedEvaluation);
                    }
                }
                return capabilities.ToArray();

                static void AddProjectCapabilities(HashSet<string> collection, Project? evaluatedProject)
                {
                    if (evaluatedProject == null)
                    {
                        return;
                    }
                    foreach (ProjectItem capability in evaluatedProject.GetItems("ProjectCapability"))
                    {
                        if (!string.IsNullOrWhiteSpace(capability.EvaluatedInclude))
                        {
                            collection.Add(capability.EvaluatedInclude);
                        }
                    }
                }
            }
        }
    }
}
