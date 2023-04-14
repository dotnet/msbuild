// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#nullable enable

using Microsoft.Build.Evaluation;

namespace Microsoft.TemplateEngine.MSBuildEvaluation
{
    /// <summary>
    /// Represents the result of evaluation for mon-SDK style project.
    /// </summary>
    internal class NonSDKStyleEvaluationResult : MSBuildEvaluationResult
    {
        private NonSDKStyleEvaluationResult(string projectPath) : base(EvalStatus.Succeeded, projectPath) { }

        internal string? TargetFrameworkVersion => EvaluatedProject?.GetProperty("TargetFrameworkVersion").EvaluatedValue;

        internal string? PlatformTarget => EvaluatedProject?.GetProperty("PlatformTarget").EvaluatedValue;

        internal static NonSDKStyleEvaluationResult CreateSuccess(string path, Project project)
        {
            return new NonSDKStyleEvaluationResult(path)
            {
                EvaluatedProject = project,
            };
        }
    }

}
