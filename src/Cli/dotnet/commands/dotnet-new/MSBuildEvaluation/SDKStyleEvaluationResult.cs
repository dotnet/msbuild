// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#nullable enable

using System;
using System.Collections.Generic;
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
