using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;

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
        /// The <see cref="ProjectCollection"/> the project is added to. Default is <see cref="ProjectCollection.GlobalProjectCollection"/>/>
        /// </summary>
        public ProjectCollection ProjectCollection { get; set; }

        /// <summary>
        /// The <see cref="ProjectLoadSettings"/> to use for evaluation.
        /// </summary>
        public ProjectLoadSettings LoadSettings { get; set; } = ProjectLoadSettings.Default;

        /// <summary>
        /// The <see cref="EvaluationContext"/> to use for evaluation.
        /// The <see cref="Project"/> will keep the reference to the context because
        /// some of its methods trigger hidden reevaluations, and those hidden reevaluations need the initial context.
        /// The stored context can be overridden via <see cref="Project.ReevaluateIfNecessary(EvaluationContext)"/>
        /// </summary>
        public EvaluationContext EvaluationContext { get; set; }
    }
}
