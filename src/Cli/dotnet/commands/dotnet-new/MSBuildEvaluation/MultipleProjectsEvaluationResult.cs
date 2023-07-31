// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using LocalizableStrings = Microsoft.DotNet.Tools.New.LocalizableStrings;

namespace Microsoft.TemplateEngine.MSBuildEvaluation
{
    internal class MultipleProjectsEvaluationResult : MSBuildEvaluationResult
    {
        private MultipleProjectsEvaluationResult() : base(EvalStatus.MultipleProjectFound) { }

        internal IReadOnlyList<string> ProjectPaths { get; private set; } = Array.Empty<string>();

        internal static MultipleProjectsEvaluationResult Create(IReadOnlyList<string> projectPaths)
        {
            return new MultipleProjectsEvaluationResult()
            {
                ProjectPaths = projectPaths,
                ErrorMessage = string.Format(LocalizableStrings.MultipleProjectsEvaluationResult_Error, string.Join("; ", projectPaths))
            };
        }
    }

}
