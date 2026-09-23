// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.v3;

namespace Microsoft.Build.UnitTests.Shared;

/// <summary>
/// Marks a test that exercises real MSBuild node processes (worker nodes, TaskHosts, the server, node reuse) through a
/// <see cref="NodeScenario"/>. Adds the trait <c>Category=NodeScenario</c> so scenario tests can be selected, run
/// separately and retried as a group.
/// </summary>
/// <remarks>
/// Scenarios must not run concurrently with each other: they share machine-wide state (node processes, named
/// pipes and mutexes). Test assemblies already run serially (<c>CollectionPerAssembly</c> with collection
/// parallelization disabled), and <see cref="NodeScenario.Create"/> fails if two scenarios overlap.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class NodeScenarioFactAttribute(
    [CallerFilePath] string? sourceFilePath = null,
    [CallerLineNumber] int sourceLineNumber = -1)
    : FactAttribute(sourceFilePath, sourceLineNumber), ITraitAttribute
{
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() => NodeScenario.Traits;
}

/// <summary>
/// The <see cref="TheoryAttribute"/> counterpart of <see cref="NodeScenarioFactAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class NodeScenarioTheoryAttribute(
    [CallerFilePath] string? sourceFilePath = null,
    [CallerLineNumber] int sourceLineNumber = -1)
    : TheoryAttribute(sourceFilePath, sourceLineNumber), ITraitAttribute
{
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() => NodeScenario.Traits;
}
