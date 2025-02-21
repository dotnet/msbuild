// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

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
public sealed class TaskInvocationAnalysisData : AnalysisData
{
    /// <summary>
    /// Represents an input or output parameter of a task.
    /// </summary>
    /// <param name="Value">The value passed to (when <paramref name="IsOutput"/> is false) or from
    /// (when <paramref name="IsOutput"/> is true) a task. This object can be of any type supported
    /// in task parameters: <see cref="Framework.ITaskItem"/>, <see cref="Framework.ITaskItem"/>[],
    /// bool, string, or anything else convertible to/from string.</param>
    /// <param name="IsOutput">True for output parameters, false for input parameters.</param>
    public record class TaskParameter(object? Value, bool IsOutput)
    {
        /// <summary>
        /// Enumerates all values passed in this parameter. E.g. for Param="@(Compile)", this will return
        /// all Compile items.
        /// </summary>
        public IEnumerable<object> EnumerateValues()
        {
            if (Value is System.Collections.IList list)
            {
                foreach (object obj in list)
                {
                    yield return obj;
                }
            }
            else if (Value is object obj)
            {
                yield return obj;
            }
        }

        /// <summary>
        /// Enumerates all values passed in this parameter, converted to strings. E.g. for Param="@(Compile)",
        /// this will return all Compile item specs.
        /// </summary>
        public IEnumerable<string> EnumerateStringValues()
        {
            foreach (object obj in EnumerateValues())
            {
                if (obj is ITaskItem taskItem)
                {
                    yield return taskItem.ItemSpec;
                }
                else
                {
                    yield return obj.ToString() ?? "";
                }
            }
        }
    }

    internal TaskInvocationAnalysisData(
        string projectFilePath,
        ElementLocation taskInvocationLocation,
        string taskName,
        string taskAssemblyLocation,
        IReadOnlyDictionary<string, TaskParameter> parameters)
        : base(projectFilePath)
    {
        TaskInvocationLocation = taskInvocationLocation;
        TaskName = taskName;
        TaskAssemblyLocation = taskAssemblyLocation;
        Parameters = parameters;
    }

    /// <summary>
    /// The project file and line/column number where the task is invoked.
    /// </summary>
    public ElementLocation TaskInvocationLocation { get; }

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
