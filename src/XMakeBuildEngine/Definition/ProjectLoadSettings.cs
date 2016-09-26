using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Flags for controlling the project load.
    /// </summary>
    /// <remarks>
    /// This is a "flags" enum, allowing future settings to be added
    /// in an additive, non breaking fashion.
    /// </remarks>
    [Flags]
    [SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue", Justification = "Public API.  'Default' is roughly equivalent to 'None'. ")]
    public enum ProjectLoadSettings
    {
        /// <summary>
        /// Normal load. This is the default.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Ignore nonexistent targets files when evaluating the project
        /// </summary>
        IgnoreMissingImports = 1,

        /// <summary>
        /// Record imports including duplicate, but not circular, imports on the ImportsIncludingDuplicates property
        /// </summary>
        RecordDuplicateButNotCircularImports = 2,

        /// <summary>
        /// Throw an exception and stop the evaluation of a project if any circular imports are detected
        /// </summary>
        RejectCircularImports = 4,

        /// <summary>
        /// Record the item elements that got evaluated
        /// </summary>
        RecordEvaluatedItemElements = 8
    }
}