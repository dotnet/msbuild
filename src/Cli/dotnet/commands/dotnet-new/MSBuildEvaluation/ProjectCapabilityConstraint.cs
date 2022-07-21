// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;

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
                throw new Exception($"Failed to create constraint '{Type}': {nameof(MSBuildEvaluator)} component is not available.");
            }

            try
            {
                MSBuildEvaluationResult evaluationResult = evaluator.EvaluateProject(environmentSettings);
                return Task.FromResult((ITemplateConstraint)new ProjectCapabilityConstraint(environmentSettings, this, evaluationResult));
            }
            catch (Exception e)
            {
                environmentSettings.Host.Logger.LogDebug("{0}: Failed to evaluate the project: {1}.", nameof(ProjectCapabilityConstraintFactory), e.Message);
                throw new Exception($"Failed to create constraint '{Type}': failed to evaluate the project: {e.Message}", e);
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

            public string DisplayName => "Project capabiltities";

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
                    throw new Exception("Invalid constraint configuration.", e);
                }

                string configuredCapabiltiesExpression;

                if (token.Type == JTokenType.String)
                {
                    string? configuredCapability = token.Value<string>();
                    if (string.IsNullOrWhiteSpace(configuredCapability))
                    {
                        _logger.LogDebug("Invalid configuration: '{0}', reason: arguments should not contain empty values.", args);
                        throw new Exception("Invalid constraint configuration: arguments should not contain empty values.");
                    }
                    configuredCapabiltiesExpression = configuredCapability;
                }
                else
                {
                    _logger.LogDebug("Invalid configuration: '{0}', reason: arguments be a string.", args);
                    throw new Exception("Invalid constraint configuration: arguments should be a string.");
                }

                if (_evaluationResult.Status == MSBuildEvaluationResult.EvalStatus.NoProjectFound)
                {
                    _logger.LogDebug("No project found. This template can only be created inside the project.");
                    return TemplateConstraintResult.CreateRestricted(this, "No project found.", "This template can only be created inside the project.");
                }
                if (_evaluationResult.Status == MSBuildEvaluationResult.EvalStatus.MultipleProjectFound)
                {
                    string foundProjects = string.Join("; ", (_evaluationResult as MultipleProjectsEvaluationResult)?.ProjectPaths ?? (IReadOnlyList<string?>)new[] { _evaluationResult.ProjectPath });
                    _logger.LogDebug("Multiple projects found: {0}, specify the project to use.", foundProjects);
                    return TemplateConstraintResult.CreateRestricted(this, $"Multiple projects found: {foundProjects}.", $"Specify the project to use using {NewCommandParser.ProjectPathOption.Aliases.First()} option.");
                }
                if (_evaluationResult.Status == MSBuildEvaluationResult.EvalStatus.NoRestore)
                {
                    _logger.LogDebug("The project is not restored. Run 'dotnet restore {0}' to restore the project.", _evaluationResult.ProjectPath);
                    return TemplateConstraintResult.CreateRestricted(this, $"The project is not restored.", $"Run 'dotnet restore {_evaluationResult.ProjectPath}' to restore the project.");
                }
                if (_evaluationResult.Status == MSBuildEvaluationResult.EvalStatus.Failed || _evaluationResult.Status == MSBuildEvaluationResult.EvalStatus.NotEvaluated || _evaluationResult.EvaluatedProject == null)
                {
                    _logger.LogDebug("Failed to evaluate project context: {0}", _evaluationResult.ErrorMessage);
                    return TemplateConstraintResult.CreateRestricted(this, $"Failed to evaluate project context: {_evaluationResult.ErrorMessage}");
                }

                try
                {
                    _logger.LogDebug("Evaluating '{0}' on '{1}' set.", configuredCapabiltiesExpression, string.Join(", ", _projectCapabilities));
                    if (!CapabilityExpressionEvaluator.Evaluate(configuredCapabiltiesExpression, _projectCapabilities))
                    {
                        _logger.LogDebug("Expression evaluated to 'false'.");
                        return TemplateConstraintResult.CreateRestricted(this, $"The item needs '{configuredCapabiltiesExpression}', and current project ({_evaluationResult.ProjectPath}) does not satisfy it.");
                    }
                    _logger.LogDebug("Expression evaluated to 'true'.");
                    return TemplateConstraintResult.CreateAllowed(this);
                }
                catch (ArgumentException ae)
                {
                    _logger.LogDebug("Invalid expression '{0}'.", configuredCapabiltiesExpression);
                    throw new Exception("Invalid constraint configuration: invalid expression.", ae);
                }
            }

            private IReadOnlyList<string> GetProjectCapabilities(MSBuildEvaluationResult result)
            {
                HashSet<string> capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

                void AddProjectCapabilities (HashSet<string> collection, Project? evaluatedProject)
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
