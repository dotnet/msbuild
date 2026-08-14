// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
#if NET
using System.Buffers;
#endif
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Globbing;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Derives <see cref="GlobResult"/> information (the include globs of an item element together with the
/// excludes present on it and all the removes that apply to it) from a set of
/// <see cref="ProjectItemElement"/>s.
/// </summary>
/// <remarks>
/// This logic is shared between <see cref="Project.GetAllGlobs()"/> (which runs after evaluation over the
/// public object model) and the evaluator's synthesis of <c>MSBuildItemGlob</c> items (which runs during
/// evaluation). Sharing a single implementation guarantees the two produce identical results, so the globs
/// observed by a target or task during a command-line build match those returned by <c>GetAllGlobs</c>.
/// </remarks>
internal static class GlobResultBuilder
{
    /// <summary>
    /// * and ? are invalid file name characters, but they occur in globs as wild cards.
    /// </summary>
#if NET
    private static readonly SearchValues<char> s_invalidGlobChars = SearchValues.Create(
#else
    private static readonly char[] s_invalidGlobChars = (
#endif
        FileUtilities.InvalidFileNameCharsArray.Where(c => c is not ('*' or '?' or '/' or '\\' or ':')).ToArray());

    /// <summary>
    /// Builds a <see cref="GlobResult"/> for each include element in <paramref name="projectItemElements"/>
    /// that contributes globs.
    /// </summary>
    /// <remarks>
    /// The elements are scanned in reverse document order so that each include element is attributed exactly
    /// the removes that appear after it (the removes that can affect the items it produced). Consequently the
    /// returned list is in reverse document order (the last include element first). Because every
    /// <see cref="GlobResult"/> already folds in its own excludes and applicable removes, membership can be
    /// decided by testing a file against the union of the results regardless of their order.
    /// </remarks>
    public static List<GlobResult> BuildGlobResults<P, I>(IReadOnlyList<ProjectItemElement> projectItemElements, Expander<P, I> expander)
        where P : class, IProperty
        where I : class, IItem, IMetadataTable
    {
        if (projectItemElements.Count == 0)
        {
            return new List<GlobResult>();
        }

        // Scan the project elements in reverse order and build globbing information for each include element.
        // Based on the fact that relevant removes for a particular include element (xml element A) consist of:
        // - all the removes seen by the next include statement of A's type (xml element B which appears after A in file order)
        // - new removes between A and B (removes that apply to A but not to B. Spatially, these are placed between A's element and B's element)

        // Example:
        // 1. <I Include="A"/>
        // 2. <I Remove="..."/> // this remove applies to the include at 1
        // 3. <I Include="B"/>
        // 4. <I Remove="..."/> // this remove applies to the includes at 1, 3
        // 5. <I Include="C"/>
        // 6. <I Remove="..."/> // this remove applies to the includes at 1, 3, 5
        // So A's applicable removes are composed of:
        //
        // The applicable removes for the element at position 1 (xml element A) are composed of:
        // - all the removes seen by the next include statement of I's type (xml element B, position 3, which appears after A in file order). In this example that's Removes at positions 4 and 6.
        // - new removes between A and B. In this example that's Remove 2.

        // use immutable builders because there will be a lot of structural sharing between includes which share increasing subsets of corresponding remove elements
        // item type -> aggregated information about all removes seen so far for that item type
        var removeElementCache = new Dictionary<string, CumulativeRemoveElementData>(projectItemElements.Count);
        var globResults = new List<GlobResult>(projectItemElements.Count);

        for (var i = projectItemElements.Count - 1; i >= 0; i--)
        {
            var itemElement = projectItemElements[i];

            if (!string.IsNullOrEmpty(itemElement.Include))
            {
                var globResult = BuildGlobResultFromIncludeItem(itemElement, removeElementCache, expander);

                if (globResult != null)
                {
                    globResults.Add(globResult);
                }
            }
            else if (!string.IsNullOrEmpty(itemElement.Remove))
            {
                CacheInformationFromRemoveItem(itemElement, removeElementCache, expander);
            }
        }

        globResults.TrimExcess();

        return globResults;
    }

    private static GlobResult? BuildGlobResultFromIncludeItem<P, I>(ProjectItemElement itemElement, IReadOnlyDictionary<string, CumulativeRemoveElementData> removeElementCache, Expander<P, I> expander)
        where P : class, IProperty
        where I : class, IItem, IMetadataTable
    {
        var includeItemspec = new ItemSpec<P, I>(itemElement.Include, expander, itemElement.IncludeLocation, itemElement.ContainingProject.DirectoryPath);

        List<ItemSpecFragment>? includeGlobFragmentsList = null;
        foreach (ItemSpecFragment fragment in includeItemspec.Fragments)
        {
            if (fragment is GlobFragment && fragment.TextFragment.AsSpan().IndexOfAny(s_invalidGlobChars) < 0)
            {
                includeGlobFragmentsList ??= new List<ItemSpecFragment>(includeItemspec.Fragments.Count);
                includeGlobFragmentsList.Add(fragment);
            }
        }

        if (includeGlobFragmentsList == null || includeGlobFragmentsList.Count == 0)
        {
            return null;
        }

        string[] includeGlobStrings = new string[includeGlobFragmentsList.Count];
        for (int i = 0; i < includeGlobStrings.Length; ++i)
        {
            includeGlobStrings[i] = includeGlobFragmentsList[i].TextFragment;
        }

        var includeGlob = CompositeGlob.Create(includeGlobFragmentsList.Select(f => f.ToMSBuildGlob()));

        IEnumerable<string> excludeFragmentStrings = [];
        IMSBuildGlob? excludeGlob = null;

        if (!string.IsNullOrEmpty(itemElement.Exclude))
        {
            var excludeItemspec = new ItemSpec<P, I>(itemElement.Exclude, expander, itemElement.ExcludeLocation, itemElement.ContainingProject.DirectoryPath);

            excludeFragmentStrings = excludeItemspec.FlattenFragmentsAsStrings().ToImmutableHashSet();
            excludeGlob = excludeItemspec.ToMSBuildGlob();
        }

        IEnumerable<string> removeFragmentStrings = [];
        IMSBuildGlob? removeGlob = null;

        if (removeElementCache.TryGetValue(itemElement.ItemType, out CumulativeRemoveElementData removeItemElement))
        {
            removeFragmentStrings = removeItemElement.FragmentStrings;
            removeGlob = CompositeGlob.Create(removeItemElement.Globs);
        }

        var includeGlobWithGaps = CreateIncludeGlobWithGaps(includeGlob, excludeGlob, removeGlob);

        return new GlobResult(itemElement, includeGlobStrings.ToImmutableArray(), includeGlobWithGaps, excludeFragmentStrings, removeFragmentStrings);
    }

    private static IMSBuildGlob CreateIncludeGlobWithGaps(IMSBuildGlob includeGlob, IMSBuildGlob? excludeGlob, IMSBuildGlob? removeGlob)
    {
        return (excludeGlob, removeGlob) switch
        {
            (null, null) => includeGlob,
            (not null, null) => new MSBuildGlobWithGaps(includeGlob, excludeGlob),
            (null, not null) => new MSBuildGlobWithGaps(includeGlob, removeGlob),
            (not null, not null) => new MSBuildGlobWithGaps(includeGlob, new CompositeGlob(excludeGlob, removeGlob))
        };
    }

    private static void CacheInformationFromRemoveItem<P, I>(ProjectItemElement itemElement, Dictionary<string, CumulativeRemoveElementData> removeElementCache, Expander<P, I> expander)
        where P : class, IProperty
        where I : class, IItem, IMetadataTable
    {
        if (!removeElementCache.TryGetValue(itemElement.ItemType, out CumulativeRemoveElementData cumulativeRemoveElementData))
        {
            cumulativeRemoveElementData = CumulativeRemoveElementData.Create();

            removeElementCache[itemElement.ItemType] = cumulativeRemoveElementData;
        }

        var removeSpec = new ItemSpec<P, I>(itemElement.Remove, expander, itemElement.RemoveLocation, itemElement.ContainingProject.DirectoryPath);

        cumulativeRemoveElementData.AccumulateInformationFromRemoveItemSpec(removeSpec.FlattenFragmentsAsStrings(), removeSpec.ToMSBuildGlob());
    }

    // represents cumulated remove information for a particular item type
    private struct CumulativeRemoveElementData
    {
        private ImmutableList<IMSBuildGlob>.Builder _globs;
        private ImmutableHashSet<string>.Builder _fragmentStrings;

        public IEnumerable<IMSBuildGlob> Globs => _globs.ToImmutable();
        public IEnumerable<string> FragmentStrings => _fragmentStrings.ToImmutable();

        public static CumulativeRemoveElementData Create()
        {
            return new CumulativeRemoveElementData
            {
                _globs = ImmutableList.CreateBuilder<IMSBuildGlob>(),
                _fragmentStrings = ImmutableHashSet.CreateBuilder<string>()
            };
        }

        public readonly void AccumulateInformationFromRemoveItemSpec(IEnumerable<string> removeSpecFragmentStrings, IMSBuildGlob removeGlob)
        {
            _globs.Add(removeGlob);

            foreach (var removeFragment in removeSpecFragmentStrings)
            {
                _fragmentStrings.Add(removeFragment);
            }
        }
    }
}
