// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Experimental.BuildCheck;

/// <summary>
/// Extension methods for <see cref="ProjectItemElement"/>.
/// </summary>
public static class ItemTypeExtensions
{
    public static IEnumerable<ProjectItemElement> GetItemsOfType(this IEnumerable<ProjectItemElement> items,
        string itemType)
    {
        return items.Where(i =>
            MSBuildNameIgnoreCaseComparer.Default.Equals(i.ItemType, itemType));
    }
}

/// <summary>
/// Holder for evaluated items and item groups.
/// </summary>
/// <param name="items"></param>
/// <param name="itemGroups"></param>
public class ItemsHolder(IEnumerable<ProjectItemElement> items, IEnumerable<ProjectItemGroupElement> itemGroups)
{
    public IEnumerable<ProjectItemElement> Items { get; } = items;
    public IEnumerable<ProjectItemGroupElement> ItemGroups { get; } = itemGroups;

    public IEnumerable<ProjectItemElement> GetItemsOfType(string itemType)
    {
        return Items.GetItemsOfType(itemType);
    }
}

/// <summary>
/// BuildCheck OM data representing the evaluated items of a project.
/// </summary>
public class ParsedItemsAnalysisData : AnalysisData
{
    internal ParsedItemsAnalysisData(
        string projectFilePath,
        ItemsHolder itemsHolder) :
        base(projectFilePath) => ItemsHolder = itemsHolder;

    public ItemsHolder ItemsHolder { get; }
}

/// <summary>
/// BuildCheck OM data representing a task executed by a project.
/// </summary>
public sealed class TaskInvocationAnalysisData : AnalysisDataWithLocation
{
    public record class TaskParameter(object? Value, bool IsOutput);

    internal TaskInvocationAnalysisData(
        string projectFilePath,
        int lineNumber,
        int columnNumber,
        string taskName,
        string taskFile,
        string taskAssemblyLocation,
        IReadOnlyDictionary<string, TaskParameter> parameters) :
        base(projectFilePath, lineNumber, columnNumber)
    {
        TaskName = taskName;
        TaskFile = taskFile;
        TaskAssemblyLocation = taskAssemblyLocation;
        Parameters = parameters;
    }

    /// <summary>
    /// MSBuild file where this task was defined.
    /// </summary>
    public string TaskFile { get; }

    /// <summary>
    /// Name of the task.
    /// </summary>
    public string TaskName { get; }

    /// <summary>
    /// The location of the assembly containing the implementation of the task.
    /// </summary>
    public string TaskAssemblyLocation { get; }

    /// <summary>
    /// The parameters of the task, keyed by parameter name.
    /// </summary>
    public IReadOnlyDictionary<string, TaskParameter> Parameters { get; }
}
