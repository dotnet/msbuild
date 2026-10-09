// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace MSBuild.Benchmarks;

/// <summary>
///  Owns a project collection and project root used to create benchmark project instances and items.
/// </summary>
internal sealed class BenchmarkProject : IDisposable
{
    /// <summary>
    ///  Initializes a benchmark project with the specified full path.
    /// </summary>
    /// <param name="projectPath">The full path assigned to the project root.</param>
    public BenchmarkProject(string projectPath)
    {
        ProjectCollection = new ProjectCollection();
        RootElement = ProjectRootElement.Create(ProjectCollection);
        RootElement.FullPath = projectPath;
    }

    /// <summary>
    ///  Gets the project collection owned by this instance.
    /// </summary>
    public ProjectCollection ProjectCollection { get; }

    /// <summary>
    ///  Gets the project root associated with the owned collection.
    /// </summary>
    public ProjectRootElement RootElement { get; }

    /// <summary>
    ///  Creates a project instance from the current project root.
    /// </summary>
    /// <returns>
    ///  A project instance associated with the owned project collection.
    /// </returns>
    public ProjectInstance CreateProjectInstance()
        => new(RootElement, globalProperties: null, toolsVersion: null, ProjectCollection);

    /// <summary>
    ///  Creates a project item instance associated with the specified project.
    /// </summary>
    /// <param name="project">The project that owns the item.</param>
    /// <param name="itemType">The item type.</param>
    /// <param name="evaluatedInclude">The evaluated item include.</param>
    /// <returns>
    ///  The created project item instance.
    /// </returns>
    public ProjectItemInstance CreateItem(
        ProjectInstance project,
        string itemType,
        string evaluatedInclude)
        => new(project, itemType, evaluatedInclude, project.FullPath);

    /// <summary>
    ///  Disposes the owned project collection.
    /// </summary>
    public void Dispose()
        => ProjectCollection.Dispose();
}
