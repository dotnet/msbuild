// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#nullable enable

using System.Collections.Generic;
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
