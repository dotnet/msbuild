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
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Constraints;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.MSBuildEvaluation
{
    internal class ProjectCapabilityConstraintFactory : ITemplateConstraintFactory
    {
        public string Type => "project-capability";

        public Guid Id => Guid.Parse("{85F3EFFB-315F-4F2F-AA0A-99EABE3B6FB3}");

        public Task<ITemplateConstraint> CreateTemplateConstraintAsync(IEngineEnvironmentSettings environmentSettings, CancellationToken cancellationToken)
        {
            MSBuildEvaluator evaluator = environmentSettings.Components.OfType<MSBuildEvaluator>().First();

            MSBuildEvaluationResult evaluationResult = evaluator.EvaluateProject(environmentSettings);
            return Task.FromResult((ITemplateConstraint)new ProjectCapabilityConstraint(environmentSettings, this, evaluationResult));

        }

        internal class ProjectCapabilityConstraint : ITemplateConstraint
        {
            private readonly IEngineEnvironmentSettings _environmentSettings;
            private readonly ITemplateConstraintFactory _factory;
            private readonly MSBuildEvaluationResult _evaluationResult;
            private readonly IReadOnlyList<string> _projectCapabilities;

            internal ProjectCapabilityConstraint(IEngineEnvironmentSettings environmentSettings, ITemplateConstraintFactory factory, MSBuildEvaluationResult evaluationResult)
            {
                _environmentSettings = environmentSettings;
                _factory = factory;
                _evaluationResult = evaluationResult;
                _projectCapabilities = GetProjectCapabilities(evaluationResult);
            }

            public string Type => _factory.Type;

            public string DisplayName => "Project capabiltities";

            public TemplateConstraintResult Evaluate(string? args)
            {
                JToken? token;
                try
                {
                    token = JToken.Parse(args!);
                }
                catch (Exception e)
                {
                    throw new Exception("Invalid constraint configuration.", e);
                }

                IEnumerable<string> configuredCapabilties;

                if (token.Type == JTokenType.String)
                {
                    string? configuredCapability = token.Value<string>();
                    if (string.IsNullOrWhiteSpace(configuredCapability))
                    {
                        throw new Exception("Invalid constraint configuration: arguments should not contain empty values.");
                    }
                    configuredCapabilties = new[] { configuredCapability };
                }
                else
                {
                    if (token is not JArray array)
                    {
                        throw new Exception("Invalid constraint configuration: arguments should be either a JSON array or a string.");
                    }

                    configuredCapabilties = array.Values<string>().Select(value =>
                    {
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            throw new Exception("Invalid constraint configuration: arguments should not contain empty values.");
                        }
                        return value;
                    });
                }

                if (_evaluationResult.Status == MSBuildEvaluationResult.EvalStatus.NoProjectFound)
                {
                    return TemplateConstraintResult.CreateRestricted(this, "No project found.", "This template can only be created inside the project.");
                }
                if (_evaluationResult.Status == MSBuildEvaluationResult.EvalStatus.MultipleProjectFound)
                {
                    return TemplateConstraintResult.CreateRestricted(this, $"Multiple project(s) found: {string.Join (", ", _evaluationResult.ProjectPath)}", "Specify the project to use.");
                }
                if (_evaluationResult.Status == MSBuildEvaluationResult.EvalStatus.NoRestore)
                {
                    return TemplateConstraintResult.CreateRestricted(this, $"The project is not restored.", $"Run 'dotnet restore {_evaluationResult.ProjectPath}' to restore the project.");
                }
                if (_evaluationResult.Status == MSBuildEvaluationResult.EvalStatus.Failed || _evaluationResult.Status == MSBuildEvaluationResult.EvalStatus.NotEvaluated || _evaluationResult.EvaluatedProject == null)
                {
                    return TemplateConstraintResult.CreateRestricted(this, $"Failed to evaluate project context: {_evaluationResult.ErrorMessage}");
                }

                foreach (string capability in configuredCapabilties)
                {
                    if (!_projectCapabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))
                    {
                        return TemplateConstraintResult.CreateRestricted(this, $"The item needs '{capability}' project capability, and current project ({_evaluationResult.ProjectPath}) does not define it.");
                    }
                }
                return TemplateConstraintResult.CreateAllowed(this);
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
