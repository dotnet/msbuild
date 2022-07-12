// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Components;
using NuGet.Configuration;

namespace Microsoft.TemplateEngine.MSBuildEvaluation
{

    /// <summary>
    /// Allows to bind symbols to MSBuild properties.
    /// </summary>
    internal class ProjectContextSymbolSource : IBindSymbolSource
    {
        private readonly object LockObj = new object();

        private IEngineEnvironmentSettings? _settings;
        private MSBuildEvaluationResult? _cachedEvaluationResult;

        public string DisplayName => "Project context";

        public string? SourcePrefix => "msbuild";

        public int Priority => 200;

        public Guid Id => Guid.Parse("{9A839474-CE00-4430-A352-98967542B56B}");

        public bool RequiresPrefixMatch => true;

        public Task<string?> GetBoundValueAsync(IEngineEnvironmentSettings settings, string bindname, CancellationToken cancellationToken)
        {
            MSBuildEvaluationResult evaluationResult = GetEvaluationResult(settings);
            if (evaluationResult.EvaluatedProject == null)
            {
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
                        Task.FromResult(propertyValue);
                    }
                }
            }
            return Task.FromResult(propertyValue);
        }

        internal void ResetCache()
        {
            lock (LockObj)
            {
                _settings = null;
                _cachedEvaluationResult = null;
            }
        }

        private MSBuildEvaluationResult GetEvaluationResult(IEngineEnvironmentSettings settings)
        {
            lock (LockObj)
            {
                if (_settings == null || _settings != settings || _cachedEvaluationResult == null)
                {
                    _settings = settings;
                    _cachedEvaluationResult = _settings.Components.OfType<MSBuildEvaluator>().Single().EvaluateProject(_settings);
                }
                return _cachedEvaluationResult;
            }
        }
    }
}
