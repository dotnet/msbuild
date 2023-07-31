// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.Build.Evaluation;

namespace Microsoft.TemplateEngine.MSBuildEvaluation
{
    /// <summary>
    /// Represents the result of evaluation for multi-target project.
    /// </summary>
    internal class MultiTargetEvaluationResult : MSBuildEvaluationResult
    {
        private MultiTargetEvaluationResult(string projectPath) : base(EvalStatus.Succeeded, projectPath) { }

        internal IReadOnlyDictionary<string, Project?> EvaluatedProjects { get; private set; } = new Dictionary<string, Project?>();

        internal IEnumerable<string> TargetFrameworks => EvaluatedProjects.Keys;

        internal static MultiTargetEvaluationResult CreateSuccess(string path, Project project, IReadOnlyDictionary<string, Project?> frameworkBasedResults)
        {
            return new MultiTargetEvaluationResult(path)
            {
                EvaluatedProject = project,
                EvaluatedProjects = frameworkBasedResults,
            };
        }
    }

}
