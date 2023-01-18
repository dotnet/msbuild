// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#nullable enable

using System;
using System.Collections.Generic;
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
