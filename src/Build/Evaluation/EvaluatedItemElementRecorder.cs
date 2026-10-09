// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Records evaluated item elements in the requested collections.
/// </summary>
/// <remarks>
///  Copies share the destination collections. The default value performs no recording.
/// </remarks>
internal readonly struct EvaluatedItemElementRecorder
{
    [Flags]
    private enum RecordingOptions
    {
        None = 0,
        ItemElements = 1 << 0,
        ItemGlobs = 1 << 1,
    }

    private readonly RecordingOptions _options;
    private readonly List<ProjectItemElement>? _evaluatedItemElements;
    private readonly HashSet<string>? _itemGlobRequestedTypes;
    private readonly List<ProjectItemElement>? _itemGlobElements;

    private EvaluatedItemElementRecorder(
        RecordingOptions options,
        List<ProjectItemElement>? evaluatedItemElements,
        HashSet<string>? itemGlobRequestedTypes,
        List<ProjectItemElement>? itemGlobElements)
    {
        _options = options;
        _evaluatedItemElements = evaluatedItemElements;
        _itemGlobRequestedTypes = itemGlobRequestedTypes;
        _itemGlobElements = itemGlobElements;
    }

    [MemberNotNullWhen(true, nameof(_evaluatedItemElements))]
    private bool IsRecordingItemElements
    {
        get
        {
            if ((_options & RecordingOptions.ItemElements) == 0)
            {
                return false;
            }

            Assumed.NotNull(_evaluatedItemElements);
            return true;
        }
    }

    [MemberNotNullWhen(true, nameof(_itemGlobRequestedTypes))]
    [MemberNotNullWhen(true, nameof(_itemGlobElements))]
    private bool IsRecordingItemGlobs
    {
        get
        {
            if ((_options & RecordingOptions.ItemGlobs) == 0)
            {
                return false;
            }

            Assumed.NotNull(_itemGlobRequestedTypes);
            Assumed.NotNull(_itemGlobElements);
            return true;
        }
    }

    /// <summary>
    ///  Evaluated include/exclude/remove item elements (condition-true, in document order) of the item types in
    ///  <see cref="_itemGlobRequestedTypes"/>, collected during the items pass so that <c>MSBuildItemGlob</c>
    ///  items can be synthesized from them. <see langword="null"/> when the feature is off.
    /// </summary>
    public IReadOnlyList<ProjectItemElement>? ItemGlobElements => _itemGlobElements;

    /// <summary>
    ///  Creates a recorder for evaluated item elements.
    /// </summary>
    /// <param name="evaluatedItemElements">The evaluated-element destination.</param>
    /// <returns>
    ///  A recorder for evaluated item elements.
    /// </returns>
    public static EvaluatedItemElementRecorder Create(List<ProjectItemElement> evaluatedItemElements)
        => new(RecordingOptions.ItemElements, evaluatedItemElements, itemGlobRequestedTypes: null, itemGlobElements: null);

    /// <summary>
    ///  Detects whether <c>MSBuildProvideItemGlobs</c> requests glob information
    ///  for one or more item types and, if so, prepares to collect their evaluated item elements. Does nothing
    ///  (and allocates nothing) when the property is unset or empty, keeping the feature zero-cost when unused.
    /// </summary>
    /// <param name="itemGlobRequests">The value of the <c>MSBuildProvideItemGlobs</c> property.</param>
    /// <returns>
    ///  A recorder for the requested item globs, or the default value when no item types are requested.
    /// </returns>
    public static EvaluatedItemElementRecorder Create(string itemGlobRequests)
    {
        HashSet<string>? itemGlobRequestedTypes = ParseItemGlobRequests(itemGlobRequests);

        return itemGlobRequestedTypes is not null
            ? new(RecordingOptions.ItemGlobs, evaluatedItemElements: null, itemGlobRequestedTypes, itemGlobElements: [])
            : default;
    }

    /// <summary>
    ///  Creates a recorder for evaluated item elements and requested item globs.
    /// </summary>
    /// <param name="evaluatedItemElements">The evaluated-element destination.</param>
    /// <param name="itemGlobRequests">The value of the <c>MSBuildProvideItemGlobs</c> property.</param>
    /// <returns>
    ///  A recorder for evaluated item elements and any requested item globs.
    /// </returns>
    public static EvaluatedItemElementRecorder Create(List<ProjectItemElement> evaluatedItemElements, string itemGlobRequests)
    {
        HashSet<string>? itemGlobRequestedTypes = ParseItemGlobRequests(itemGlobRequests);

        return itemGlobRequestedTypes is not null
            ? new(RecordingOptions.ItemElements | RecordingOptions.ItemGlobs, evaluatedItemElements, itemGlobRequestedTypes, itemGlobElements: [])
            : new(RecordingOptions.ItemElements, evaluatedItemElements, itemGlobRequestedTypes: null, itemGlobElements: null);
    }

    /// <summary>
    ///  Records an element whose item group and item conditions both evaluated to <see langword="true"/>.
    /// </summary>
    /// <param name="itemElement">The evaluated item element.</param>
    public void Record(ProjectItemElement itemElement)
    {
        if (IsRecordingItemElements)
        {
            _evaluatedItemElements.Add(itemElement);
        }

        if (IsRecordingItemGlobs && _itemGlobRequestedTypes.Contains(itemElement.ItemType))
        {
            _itemGlobElements.Add(itemElement);
        }
    }

    private static HashSet<string>? ParseItemGlobRequests(string itemGlobRequests)
    {
        HashSet<string>? itemGlobRequestedTypes = null;

        foreach (string itemGlobRequestedType in ExpressionShredder.SplitSemiColonSeparatedList(itemGlobRequests))
        {
            itemGlobRequestedTypes ??= [with(StringComparer.OrdinalIgnoreCase)];
            itemGlobRequestedTypes.Add(itemGlobRequestedType);
        }

        return itemGlobRequestedTypes;
    }
}
