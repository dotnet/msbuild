// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace MSBuild.Benchmarks;

/// <summary>
///  Owns an expander and the benchmark project required by any items it references.
/// </summary>
internal sealed class ExpanderBenchmarkFixture : IDisposable
{
    private readonly BenchmarkProject? _benchmarkProject;

    /// <summary>
    ///  Initializes a fixture for the specified expander and project.
    /// </summary>
    /// <param name="expander">The configured expander.</param>
    /// <param name="benchmarkProject">
    ///  The project to keep alive for the fixture lifetime, or <see langword="null"/> when the expander has no items.
    /// </param>
    public ExpanderBenchmarkFixture(
        Expander<ProjectPropertyInstance, ProjectItemInstance> expander,
        BenchmarkProject? benchmarkProject)
    {
        Expander = expander;
        _benchmarkProject = benchmarkProject;
    }

    /// <summary>
    ///  Gets the configured expander.
    /// </summary>
    public Expander<ProjectPropertyInstance, ProjectItemInstance> Expander { get; }

    /// <summary>
    ///  Disposes the benchmark project, when present.
    /// </summary>
    public void Dispose()
        => _benchmarkProject?.Dispose();
}
