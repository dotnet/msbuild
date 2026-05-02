// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Represents a single import relationship discovered during project evaluation.
    /// Each edge connects an importing project file to the file it imported.
    /// </summary>
    /// <remarks>
    /// A collection of these edges forms the import tree for a project.
    /// Each imported file appears at most once — when multiple files import the same
    /// file, only the first occurrence (in depth-first evaluation order) is recorded.
    /// The root project itself is not represented as an edge; only actual
    /// import relationships are included.
    /// <para>
    /// Obtain import edges at task execution time via
    /// <see cref="EngineServices.ImportEdges"/>.
    /// </para>
    /// </remarks>
    /// <param name="ImportedProjectPath">Full path of the imported project file.</param>
    /// <param name="ImportingProjectPath">Full path of the project file that contains the <c>&lt;Import&gt;</c> element, or <see langword="null"/> if this is a direct import from the root project.</param>
    /// <param name="SdkName">The SDK name if this import was resolved via an SDK reference (e.g. <c>"Microsoft.NET.Sdk"</c>); otherwise <see langword="null"/>.</param>
    public readonly record struct ProjectImportEdge(
        string ImportedProjectPath,
        string? ImportingProjectPath,
        string? SdkName = null)
    {
        /// <inheritdoc/>
        public override string ToString()
        {
            string arrow = ImportingProjectPath is null
                ? $"[root] -> {ImportedProjectPath}"
                : $"{ImportingProjectPath} -> {ImportedProjectPath}";

            return SdkName is null ? arrow : $"{arrow} (SDK: {SdkName})";
        }
    }
}
