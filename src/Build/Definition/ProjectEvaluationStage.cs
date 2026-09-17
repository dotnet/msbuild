// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Specifies how far project evaluation should proceed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MSBuild evaluation runs a fixed sequence of passes. Callers that only need data produced by
    /// an early pass (for example, a property value) can request a partial evaluation that stops
    /// after that pass, avoiding the cost of the later passes (item globbing, using-tasks, and
    /// target registration).
    /// </para>
    /// <para>
    /// The values are ordered by how much of evaluation is performed. A larger value performs strictly
    /// more work than a smaller one. The default is <see cref="Full"/>, which preserves the historical
    /// behavior of running every pass.
    /// </para>
    /// <para>
    /// Reading state that a partial evaluation did not produce (for example, reading items after
    /// stopping at <see cref="Properties"/>) throws <see cref="System.InvalidOperationException"/>.
    /// </para>
    /// </remarks>
    public enum ProjectEvaluationStage
    {
        /// <summary>
        /// Evaluate initial properties, properties, and imports (passes 0 and 1), then stop.
        /// Property values are final and equivalent to a full evaluation. Items, item definitions,
        /// using-tasks, and targets are not available.
        /// </summary>
        Properties = 1,

        /// <summary>
        /// Evaluate through item definitions (pass 2), then stop. Includes everything from
        /// <see cref="Properties"/>. Items, using-tasks, and targets are not available.
        /// </summary>
        ItemDefinitions = 2,

        /// <summary>
        /// Evaluate through items (passes 3 and 3.1), then stop. Includes everything from
        /// <see cref="ItemDefinitions"/>. Using-tasks and targets are not available.
        /// </summary>
        Items = 3,

        /// <summary>
        /// Evaluate through using-tasks (pass 4), then stop. Includes everything from
        /// <see cref="Items"/>. Targets are not available.
        /// </summary>
        UsingTasks = 4,

        /// <summary>
        /// Perform a complete evaluation, including target registration (pass 5). This is the default.
        /// </summary>
        Full = int.MaxValue,
    }
}
