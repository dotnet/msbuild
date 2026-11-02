// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.FileSystem;

#nullable disable

namespace Microsoft.Build.Definition
{
    /// <summary>
    ///     Common <see cref="Project" /> constructor arguments.
    /// </summary>
    public class ProjectOptions
    {
        /// <summary>
        /// Global properties to evaluate with.
        /// </summary>
        public IDictionary<string, string> GlobalProperties { get; set; }

        /// <summary>
        /// Tools version to evaluate with
        /// </summary>
        public string ToolsVersion { get; set; }

        /// <summary>
        /// Sub-toolset version to explicitly evaluate the toolset with.
        /// </summary>
        public string SubToolsetVersion { get; set; }

        /// <summary>
        /// The <see cref="ProjectCollection"/> the project is added to. Default is <see cref="ProjectCollection.GlobalProjectCollection"/>.
        /// </summary>
        public ProjectCollection ProjectCollection { get; set; }

        /// <summary>
        /// The <see cref="ProjectLoadSettings"/> to use for evaluation.
        /// </summary>
        public ProjectLoadSettings LoadSettings { get; set; } = ProjectLoadSettings.Default;

        /// <summary>
        /// The <see cref="EvaluationContext"/> to use for evaluation.
        /// </summary>
        public EvaluationContext EvaluationContext { get; set; }

        /// <summary>
        /// Provides <see cref="IDirectoryCache"/> to be used for evaluation.
        /// </summary>
        public IDirectoryCacheFactory DirectoryCacheFactory { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if loading the project is allowed to interact with the user.
        /// </summary>
        public bool Interactive { get; set; }

        /// <summary>
        /// The <see cref="ProjectEvaluationStage"/> controlling how far evaluation should proceed.
        /// Defaults to <see cref="ProjectEvaluationStage.Full"/> (a complete evaluation).
        /// A non-<see cref="ProjectEvaluationStage.Full"/> (partial) stage is only honored when creating a
        /// <see cref="Microsoft.Build.Execution.ProjectInstance"/>; passing a partial stage to a <see cref="Project"/> factory
        /// (for example <see cref="Project.FromFile(string, ProjectOptions)"/>) throws
        /// <see cref="System.ArgumentException"/>.
        /// </summary>
        public ProjectEvaluationStage EvaluationStage
        {
            get => _evaluationStage;
            set
            {
                // The enum intentionally leaves a large numeric gap between UsingTasks and Full. Reject any
                // undefined value so a stray stage cannot slip through and cause the evaluator to run every
                // pass while the object-model guards still (incorrectly) report the state as unavailable.
                if (!Enum.IsDefined(typeof(ProjectEvaluationStage), value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
                }

                _evaluationStage = value;
            }
        }

        private ProjectEvaluationStage _evaluationStage = ProjectEvaluationStage.Full;
    }
}
