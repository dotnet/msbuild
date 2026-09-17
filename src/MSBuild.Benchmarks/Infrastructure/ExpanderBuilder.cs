// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared.FileSystem;

namespace MSBuild.Benchmarks;

/// <summary>
///  Builds an expander from explicitly supplied properties, items, and metadata.
/// </summary>
internal sealed class ExpanderBuilder : IDisposable
{
    private readonly ItemDictionary<ProjectItemInstance> _items = new();
    private readonly Dictionary<string, string> _metadata = new(MSBuildNameIgnoreCaseComparer.Default);
    private readonly string _projectPath;
    private readonly PropertyDictionary<ProjectPropertyInstance> _properties = new();

    private BenchmarkProject? _benchmarkProject;
    private ProjectInstance? _projectInstance;
    private bool _isComplete;

    /// <summary>
    ///  Initializes an empty expander builder.
    /// </summary>
    /// <param name="projectPath">
    ///  The project path assigned to items created by this builder, or <see langword="null"/> to use a temporary path.
    /// </param>
    public ExpanderBuilder(string? projectPath = null)
    {
        _projectPath = projectPath
            ?? Path.Combine(Path.GetTempPath(), $"MSBuild.Benchmarks.Expander.{Guid.NewGuid():N}.proj");
    }

    /// <summary>
    ///  Adds or replaces a property.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The property value.</param>
    /// <param name="mayBeReserved">
    ///  <see langword="true"/> when the property may be reserved; otherwise, <see langword="false"/>.
    /// </param>
    /// <returns>
    ///  This builder.
    /// </returns>
    public ExpanderBuilder AddProperty(string name, string value, bool mayBeReserved = false)
    {
        VerifyNotComplete();
        _properties.Set(ProjectPropertyInstance.Create(name, value, mayBeReserved));
        return this;
    }

    /// <summary>
    ///  Adds an item and returns it so callers can populate scenario-specific metadata.
    /// </summary>
    /// <param name="itemType">The item type.</param>
    /// <param name="evaluatedInclude">The evaluated item include.</param>
    /// <returns>
    ///  The added item.
    /// </returns>
    public ProjectItemInstance AddItem(string itemType, string evaluatedInclude)
    {
        VerifyNotComplete();

        _benchmarkProject ??= new BenchmarkProject(_projectPath);
        _projectInstance ??= _benchmarkProject.CreateProjectInstance();

        ProjectItemInstance item = _benchmarkProject.CreateItem(
            _projectInstance,
            itemType,
            evaluatedInclude);

        _items.Add(item);
        return item;
    }

    /// <summary>
    ///  Adds or replaces an unqualified or qualified metadata value.
    /// </summary>
    /// <param name="name">The metadata name.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>
    ///  This builder.
    /// </returns>
    public ExpanderBuilder AddMetadata(string name, string value)
    {
        VerifyNotComplete();
        _metadata[name] = value;
        return this;
    }

    /// <summary>
    ///  Builds an expander and transfers project ownership to the returned fixture.
    /// </summary>
    /// <returns>
    ///  A fixture containing the configured expander.
    /// </returns>
    public ExpanderBenchmarkFixture Build()
    {
        VerifyNotComplete();
        _isComplete = true;

        Expander<ProjectPropertyInstance, ProjectItemInstance> expander;

        if (_metadata.Count > 0)
        {
            expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
                _properties,
                _items,
                new StringMetadataTable(_metadata),
                FileSystems.Default);
        }
        else if (_items.Count > 0)
        {
            expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
                _properties,
                _items,
                FileSystems.Default,
                loggingContext: null);
        }
        else
        {
            expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(
                _properties,
                FileSystems.Default);
        }

        BenchmarkProject? benchmarkProject = _benchmarkProject;
        _benchmarkProject = null;
        _projectInstance = null;

        return new ExpanderBenchmarkFixture(expander, benchmarkProject);
    }

    /// <summary>
    ///  Disposes project state that has not been transferred to a fixture.
    /// </summary>
    public void Dispose()
    {
        _isComplete = true;
        _benchmarkProject?.Dispose();
        _benchmarkProject = null;
        _projectInstance = null;
    }

    private void VerifyNotComplete()
    {
        if (_isComplete)
        {
            throw new InvalidOperationException("The expander builder has already been built or disposed.");
        }
    }
}
