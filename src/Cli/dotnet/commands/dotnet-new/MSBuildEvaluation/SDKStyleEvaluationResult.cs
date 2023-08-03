// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.Build.Evaluation;

namespace Microsoft.TemplateEngine.MSBuildEvaluation
{
    /// <summary>
    /// Represents the result of evaluation for single-target SDK style project.
    /// </summary>
    internal class SDKStyleEvaluationResult : MSBuildEvaluationResult
    {
        private SDKStyleEvaluationResult(string projectPath, string targetFramework) : base(EvalStatus.Succeeded, projectPath)
        {
            TargetFramework = targetFramework;
        }

        internal string TargetFramework { get; }

        internal static SDKStyleEvaluationResult CreateSuccess(string path, string targetFramework, Project project)
        {
            return new SDKStyleEvaluationResult(path, targetFramework)
            {
                EvaluatedProject = project,
            };
        }
    }

}
