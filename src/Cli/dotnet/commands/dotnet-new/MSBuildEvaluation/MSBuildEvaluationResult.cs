// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.Build.Evaluation;
using LocalizableStrings = Microsoft.DotNet.Tools.New.LocalizableStrings;

namespace Microsoft.TemplateEngine.MSBuildEvaluation
{
    /// <summary>
    /// Represents MSBuild evaluation result. 
    /// For success results, <see cref="SDKStyleEvaluationResult"/>, <see cref="NonSDKStyleEvaluationResult"/>, <see cref="MultiTargetEvaluationResult"/> are used depending on the evaluated project.
    /// </summary>
    internal class MSBuildEvaluationResult
    {
        protected MSBuildEvaluationResult(EvalStatus status)
        {
            Status = status;
        }

        protected MSBuildEvaluationResult(EvalStatus status, string projectPath)
        {
            Status = status;
            ProjectPath = projectPath;

            string extension = Path.GetExtension(projectPath);
            Language = extension.ToLowerInvariant() switch
            {
                "csproj" => DotNetLanguage.CSharp,
                "fsproj" => DotNetLanguage.FSharp,
                "vbproj" => DotNetLanguage.VB,
                _ => DotNetLanguage.NotEvaluated
            };
        }

        internal enum EvalStatus { NotEvaluated, NoProjectFound, MultipleProjectFound, NoRestore, Succeeded, Failed }

        internal enum DotNetLanguage { NotEvaluated, CSharp, VB, FSharp }

        internal EvalStatus Status { get; }

        internal DotNetLanguage Language { get; private set; }

        internal string? ProjectPath { get; }

        public Project? EvaluatedProject { get; protected set; }

        public string? ErrorMessage { get; protected set; }

        internal static MSBuildEvaluationResult CreateNoProjectFound(string path)
        {
            return new MSBuildEvaluationResult(EvalStatus.NoProjectFound)
            {
                ErrorMessage = string.Format(LocalizableStrings.MSBuildEvaluationResult_Error_NoProjectFound, path)
            };
        }

        internal static MSBuildEvaluationResult CreateNoRestore(string path)
        {
            return new MSBuildEvaluationResult(EvalStatus.NoRestore, path)
            {
                ErrorMessage = string.Format(LocalizableStrings.MSBuildEvaluationResult_Error_NotRestored, path)
            };
        }

        internal static MSBuildEvaluationResult CreateFailure(string path, string errorMessage)
        {
            return new MSBuildEvaluationResult(EvalStatus.Failed, path)
            {
                ErrorMessage = errorMessage,
            };
        }
    }

}
