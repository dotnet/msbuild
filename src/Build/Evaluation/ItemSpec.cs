// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Globbing;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    ///     Represents the elements of an item specification string (e.g. Include="*.cs;foo;@(i)") and
    ///     provides some operations over them (like matching items against a given ItemSpec)
    /// </summary>
    internal class ItemSpec<P, I>
        where P : class, IProperty
        where I : class, IItem, IMetadataTable
    {
        internal readonly struct ReferencedItem
        {
            public I Item { get; }
            public ValueFragment ItemAsValueFragment { get; }

            public ReferencedItem(I item, ValueFragment itemAsValueFragment)
            {
                Item = item;
                ItemAsValueFragment = itemAsValueFragment;
            }
        }

        internal class ItemExpressionFragment : ItemSpecFragment
        {
            private readonly ItemSpec<P, I> _containingItemSpec;
            private Expander<P, I> _expander;

            private IMSBuildGlob _msbuildGlob;

            private List<ReferencedItem> _referencedItems;
            public ExpressionShredder.ItemExpressionCapture Capture { get; }

            public List<ReferencedItem> ReferencedItems
            {
                get
                {
                    InitReferencedItemsIfNecessary();
                    return _referencedItems;
                }
            }

            protected override IMSBuildGlob MsBuildGlob
            {
                get
                {
                    if (InitReferencedItemsIfNecessary() || _msbuildGlob == null)
                    {
                        _msbuildGlob = CreateMsBuildGlob();
                    }

                    return _msbuildGlob;
                }
            }

            public ItemExpressionFragment(
                ExpressionShredder.ItemExpressionCapture capture,
                string textFragment,
                ItemSpec<P, I> containingItemSpec,
                string projectDirectory)
                : base(textFragment, projectDirectory)
            {
                Capture = capture;

                _containingItemSpec = containingItemSpec;
                _expander = _containingItemSpec.Expander;
            }

            public override int MatchCount(string itemToMatch)
            {
                return ReferencedItems.Count(v => v.ItemAsValueFragment.IsMatch(itemToMatch));
            }

            public override bool IsMatch(string itemToMatch)
            {
                return ReferencedItems.Any(v => v.ItemAsValueFragment.IsMatch(itemToMatch));
            }

            public override IEnumerable<string> GetReferencedItems()
            {
                return ReferencedItems.Select(v => EscapingUtilities.UnescapeAll(v.ItemAsValueFragment.TextFragment));
            }

            public override IMSBuildGlob ToMSBuildGlob()
            {
                return MsBuildGlob;
            }

            protected override IMSBuildGlob CreateMsBuildGlob()
            {
                if (ReferencedItems.Count == 1)
                {
                    // Optimize the common case, avoiding allocation of enumerable/enumerator.
                    return ReferencedItems[0].ItemAsValueFragment.ToMSBuildGlob();
                }

                return CompositeGlob.Create(ReferencedItems.Select(i => i.ItemAsValueFragment.ToMSBuildGlob()));
            }

            private bool InitReferencedItemsIfNecessary()
            {
                // cache referenced items as long as the expander does not change
                // reference equality works for now since the expander cannot mutate its item state (hopefully it stays that way)
                if (_referencedItems == null || _expander != _containingItemSpec.Expander)
                {
                    _expander = _containingItemSpec.Expander;

                    _expander.ExpandExpressionCapture(
                        Capture,
                        _containingItemSpec.ItemSpecLocation,
                        ExpanderOptions.ExpandItems,
                        includeNullEntries: false,
                        isTransformExpression: out _,
                        entries: out var entries);
                    _referencedItems =
                        entries?.Select(i => new ReferencedItem(i.Item, new ValueFragment(i.Value, ProjectDirectory))).ToList() ?? [];

                    return true;
                }

                return false;
            }
        }

        public string ItemSpecString { get; }

        /// <summary>
        ///     The fragments that compose an item spec string (values, globs, item references)
        /// </summary>
        public List<ItemSpecFragment> Fragments { get; }

        /// <summary>
        ///     The expander needs to have a default item factory set.
        /// </summary>
        // todo Make this type immutable. Dealing with an Expander change is painful. See the ItemExpressionFragment
        public Expander<P, I> Expander { get; set; }

        /// <summary>
        ///     The xml attribute where this itemspec comes from
        /// </summary>
        public IElementLocation ItemSpecLocation { get; }

        /// <param name="itemSpec">The string containing item syntax</param>
        /// <param name="expander">Expects the expander to have a default item factory set</param>
        /// <param name="itemSpecLocation">The xml location the itemspec comes from</param>
        /// <param name="projectDirectory">The directory that the project is in.</param>
        /// <param name="expandProperties">Expand properties before breaking down fragments. Defaults to true</param>
        public ItemSpec(
            string itemSpec,
            Expander<P, I> expander,
            IElementLocation itemSpecLocation,
            string projectDirectory,
            bool expandProperties = true)
        {
            ItemSpecString = itemSpec;
            Expander = expander;
            ItemSpecLocation = itemSpecLocation;

            Fragments = BuildItemFragments(itemSpecLocation, projectDirectory, expandProperties);
        }

        private List<ItemSpecFragment> BuildItemFragments(IElementLocation itemSpecLocation, string projectDirectory, bool expandProperties)
        {
            // Code corresponds to Evaluator.CreateItemsFromInclude
            var evaluatedItemspecEscaped = ItemSpecString;

            if (string.IsNullOrEmpty(evaluatedItemspecEscaped))
            {
                return new List<ItemSpecFragment>();
            }

            // STEP 1: Expand properties in Include
            if (expandProperties)
            {
                evaluatedItemspecEscaped = Expander.ExpandIntoStringLeaveEscaped(
                    ItemSpecString,
                    ExpanderOptions.ExpandProperties,
                    itemSpecLocation);
            }

            var semicolonCount = 0;
            foreach (var c in evaluatedItemspecEscaped)
            {
                if (c == ';')
                {
                    semicolonCount++;
                }
            }

            // estimate the number of fragments with the number of semicolons. This is will overestimate in case of transforms with semicolons, but won't underestimate.
            var fragments = new List<ItemSpecFragment>(semicolonCount + 1);

            // STEP 2: Split Include on any semicolons, and take each split in turn
            if (evaluatedItemspecEscaped.Length > 0)
            {
                var splitsEscaped = ExpressionShredder.SplitSemiColonSeparatedList(evaluatedItemspecEscaped);

                foreach (var splitEscaped in splitsEscaped)
                {
                    // STEP 3: If expression is "@(x)" copy specified list with its metadata, otherwise just treat as string
                    var itemReferenceFragment = ProcessItemExpression(
                        splitEscaped,
                        itemSpecLocation,
                        projectDirectory,
                        out var isItemListExpression);

                    if (isItemListExpression)
                    {
                        fragments.Add(itemReferenceFragment);
                    }
                    else
                    {
                        // The expression is not of the form "@(X)". Treat as string

                        // Code corresponds to EngineFileUtilities.GetFileList
                        if (!FileMatcher.HasWildcards(splitEscaped))
                        {
                            // No real wildcards means we just return the original string.  Don't even bother
                            // escaping ... it should already be escaped appropriately since it came directly
                            // from the project file

                            fragments.Add(new ValueFragment(splitEscaped, projectDirectory));
                        }
                        else if (EscapingUtilities.ContainsEscapedWildcards(splitEscaped))
                        {
                            // '*' is an illegal character to have in a filename.
                            // todo: file-system assumption on legal path characters: https://github.com/dotnet/msbuild/issues/781
                            // Just return the original string.
                            fragments.Add(new ValueFragment(splitEscaped, projectDirectory));
                        }
                        else
                        {
                            // Unescape before handing it to the filesystem.
                            var filespecUnescaped = EscapingUtilities.UnescapeAll(splitEscaped);

                            fragments.Add(new GlobFragment(filespecUnescaped, projectDirectory));
                        }
                    }
                }
            }

            return fragments;
        }

        private ItemExpressionFragment ProcessItemExpression(
            string expression,
            IElementLocation elementLocation,
            string projectDirectory,
            out bool isItemListExpression)
        {
            isItemListExpression = false;

            if (Expander<P, I>.TryExpandSingleItemVectorExpression(
                    expression,
                    ExpanderOptions.ExpandItems,
                    elementLocation,
                    out ExpressionShredder.ItemExpressionCapture itemVector))
            {
                isItemListExpression = true;

                return new ItemExpressionFragment(itemVector, expression, this, projectDirectory);
            }

            return null;
        }

        /// <summary>
        ///     Return true if the given <paramref name="item" /> matches this itemspec
        /// </summary>
        /// <param name="item">The item to attempt to find a match for.</param>
        public bool MatchesItem(I item)
        {
            // Avoid unnecessary LINQ/Func/Enumerator allocations on this path, this is called a lot

            var evaluatedInclude = item.EvaluatedInclude;
            foreach (var fragment in Fragments)
            {
                if (fragment.IsMatch(evaluatedInclude))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Return the fragments that match against the given <paramref name="itemToMatch" />
        /// </summary>
        /// <param name="itemToMatch">The item to match.</param>
        /// <param name="matches">
        ///     Total number of matches. Some fragments match more than once (item expression may contain multiple instances of
        ///     <paramref name="itemToMatch" />)
        /// </param>
        public IEnumerable<ItemSpecFragment> FragmentsMatchingItem(string itemToMatch, out int matches)
        {
            var result = new List<ItemSpecFragment>(Fragments.Count);
            matches = 0;

            foreach (var fragment in Fragments)
            {
                var itemMatches = fragment.MatchCount(itemToMatch);

                if (itemMatches > 0)
                {
                    result.Add(fragment);
                    matches += itemMatches;
                }
            }

            return result;
        }

        /// <summary>
        ///  Returns the normalized item paths matched by this item specification.
        /// </summary>
        /// <param name="normalizedItemPaths">The normalized item paths to match against.</param>
        /// <returns>
        ///  The set of matching paths, using the comparison rules of <paramref name="normalizedItemPaths"/>.
        /// </returns>
        public ISet<NormalizedItemPath> IntersectsWith(HashSet<NormalizedItemPath> normalizedItemPaths)
        {
            HashSet<NormalizedItemPath> matches = null;

            foreach (var fragment in Fragments)
            {
                IEnumerable<string> referencedItems = fragment.GetReferencedItems();
                if (referencedItems is not null)
                {
                    // The fragment can enumerate its referenced items, so use path membership lookups.
                    foreach (var spec in referencedItems)
                    {
                        NormalizedItemPath key = new(FileUtilities.NormalizePathForComparisonNoThrow(spec, fragment.ProjectDirectory));
                        if (normalizedItemPaths.Contains(key))
                        {
                            matches ??= [with(normalizedItemPaths.Comparer)];
                            matches.Add(key);
                        }
                    }
                }
                else
                {
                    // The fragment cannot enumerate its referenced items. Test each indexed path.
                    foreach (NormalizedItemPath path in normalizedItemPaths)
                    {
                        if (fragment.IsMatchNormalized(path.Value))
                        {
                            matches ??= [with(normalizedItemPaths.Comparer)];
                            matches.Add(path);
                        }
                    }
                }
            }

            return matches is not null ? matches : FrozenSet<NormalizedItemPath>.Empty;
        }

        /// <summary>
        ///     Return an MSBuildGlob that represents this ItemSpec.
        /// </summary>
        public IMSBuildGlob ToMSBuildGlob()
        {
            if (Fragments.Count == 1)
            {
                // Optimize the common case, avoiding allocation of enumerable/enumerator.
                return Fragments[0].ToMSBuildGlob();
            }

            return CompositeGlob.Create(Fragments.Select(f => f.ToMSBuildGlob()));
        }

        /// <summary>
        ///     Returns all the fragment strings that represent it.
        ///     "1;*;2;@(foo)" gets returned as ["1", "2", "*", "a", "b"], given that @(foo)=["a", "b"]
        ///     Order is not preserved. Globs are not expanded. Item expressions get replaced with their referring item instances.
        /// </summary>
        public IEnumerable<string> FlattenFragmentsAsStrings()
        {
            foreach (var fragment in Fragments)
            {
                if (fragment is ValueFragment or GlobFragment)
                {
                    yield return fragment.TextFragment;
                }
                else if (fragment is ItemExpressionFragment itemExpression)
                {
                    foreach (var referencedItem in itemExpression.ReferencedItems)
                    {
                        yield return referencedItem.ItemAsValueFragment.TextFragment;
                    }
                }
                else
                {
                    Assumed.Unreachable();
                }
            }
        }

        public override string ToString()
        {
            return ItemSpecString;
        }
    }

    internal abstract class ItemSpecFragment
    {
        private FileSpecMatcherTester _fileMatcher;

        private bool _fileMatcherInitialized;

        private IMSBuildGlob _msbuildGlob;

        /// <summary>
        ///     The substring from the original itemspec representing this fragment
        /// </summary>
        public string TextFragment { get; }

        /// <summary>
        ///     Path of the project the itemspec is coming from
        /// </summary>
        internal string ProjectDirectory { get; }

        // not a Lazy to reduce memory
        private ref FileSpecMatcherTester FileMatcher
        {
            get
            {
                if (!_fileMatcherInitialized)
                {
                    _fileMatcher = CreateFileSpecMatcher();
                    _fileMatcherInitialized = true;
                }

                return ref _fileMatcher;
            }
        }

        // not a Lazy to reduce memory
        protected virtual IMSBuildGlob MsBuildGlob => _msbuildGlob ??= CreateMsBuildGlob();

        protected ItemSpecFragment(string textFragment, string projectDirectory)
        {
            TextFragment = textFragment;
            ProjectDirectory = projectDirectory;
        }

        /// <returns>The number of times the
        ///     <param name="itemToMatch"></param>
        ///     appears in this fragment
        /// </returns>
        public virtual int MatchCount(string itemToMatch)
        {
            return IsMatch(itemToMatch)
                ? 1
                : 0;
        }

        public virtual bool IsMatch(string itemToMatch)
        {
            return FileMatcher.IsMatch(itemToMatch);
        }

        public virtual bool IsMatchNormalized(string normalizedItemToMatch)
        {
            return FileMatcher.IsMatchNormalized(normalizedItemToMatch);
        }

        public virtual IEnumerable<string> GetReferencedItems()
        {
            return Enumerable.Repeat(EscapingUtilities.UnescapeAll(TextFragment), 1);
        }

        public virtual IMSBuildGlob ToMSBuildGlob()
        {
            return MsBuildGlob;
        }

        protected virtual IMSBuildGlob CreateMsBuildGlob()
        {
            return MSBuildGlob.Parse(ProjectDirectory, EscapingUtilities.UnescapeAll(TextFragment));
        }

        private FileSpecMatcherTester CreateFileSpecMatcher()
        {
            return FileSpecMatcherTester.Parse(ProjectDirectory, TextFragment);
        }
    }

    internal class ValueFragment : ItemSpecFragment
    {
        public ValueFragment(string textFragment, string projectDirectory)
            : base(textFragment, projectDirectory)
        {
        }
    }

    internal class GlobFragment : ItemSpecFragment
    {
        public GlobFragment(string textFragment, string projectDirectory)
            : base(textFragment, projectDirectory)
        {
        }

        public override IEnumerable<string> GetReferencedItems()
        {
            // This fragment cannot efficiently enumerate its referenced items.
            return null;
        }

        /// <summary>
        /// True if TextFragment starts with /**/ or a variation thereof with backslashes.
        /// </summary>
        public bool IsFullFileSystemScan => TextFragment.Length >= 4
            && FileUtilities.IsAnySlash(TextFragment[0])
            && TextFragment[1] == '*'
            && TextFragment[2] == '*'
            && FileUtilities.IsAnySlash(TextFragment[3]);
    }

    /// <summary>
    ///  Stores ordered tuples of metadata values from the referenced source items.
    /// </summary>
    /// <typeparam name="P">Property type</typeparam>
    /// <typeparam name="I">Item type</typeparam>
    /// <remarks>
    ///  Each level represents one metadata name in the caller's sequence, and each edge is a complete
    ///  value for that name. Tuples such as <c>(shared, left)</c> and <c>(shared, right)</c> share their
    ///  first edge. Following a complete path requires all values to match the same source tuple;
    ///  independent sets of values could incorrectly combine metadata from different sources.
    ///  <para>
    ///   For example, sources with tuples <c>(a, x)</c> and <c>(b, y)</c> must not match a candidate
    ///   with <c>(a, y)</c>. Separate sets for each metadata name would accept both candidate values,
    ///   but no single source has that combination. A flat set of values would also lose the
    ///   association between a value and its metadata name. The trie preserves both the tuple
    ///   boundaries and the position of each value within the tuple.
    ///  </para>
    ///  <para>
    ///   Construction and lookup must use the same ordered metadata names. There are no terminal
    ///   markers because all tuples have the same width. Empty candidate values never match.
    ///   The tree captures values during creation and is only read afterward; subsequent source
    ///   item changes do not alter it. Leaf nodes have no child dictionary.
    ///  </para>
    ///  <para>
    ///   Metadata ordering affects prefix sharing and memory use. For <c>N</c> distinct two-value
    ///   tuples sharing their first value, the tree needs one shared node and <c>N</c> second-level
    ///   nodes, excluding the root. If those metadata names are reversed, the unique values come
    ///   first and each needs its own child for the shared value, producing <c>2N</c> nodes instead.
    ///   Thus, users can affect the tree's shape by ordering names in <c>MatchOnMetadata</c>.
    ///  </para>
    ///  <para>
    ///   A future optimization could choose an order that improves prefix sharing, but it would
    ///   need to apply the same permutation to source construction and candidate lookup. Reordering
    ///   only one side would compare different metadata names at corresponding levels. No such
    ///   reordering is currently performed.
    ///  </para>
    /// </remarks>
    internal sealed class MetadataTrie<P, I>
        where P : class, IProperty
        where I : class, IItem, IMetadataTable
    {
        private readonly Dictionary<string, MetadataTrie<P, I>> _children;

        /// <summary>
        ///  The captured directory for path-like comparisons, or <see langword="null"/> when values are compared unchanged.
        /// </summary>
        private readonly string _baseDirectory;

        private MetadataTrie(Dictionary<string, MetadataTrie<P, I>> children, string baseDirectory)
        {
            _children = children;
            _baseDirectory = baseDirectory;
        }

        /// <summary>
        ///  Creates a trie from the captured source items using the specified metadata comparison policy.
        /// </summary>
        /// <param name="options">The comparison and normalization policy.</param>
        /// <param name="metadataNames">The metadata names, whose enumeration order must remain consistent during construction and lookup.</param>
        /// <param name="itemSpec">The captured source item references.</param>
        /// <returns>
        ///  The completed trie, including an empty trie when no source tuples contribute child edges.
        /// </returns>
        public static MetadataTrie<P, I> Create(
            MatchOnMetadataOptions options,
            ImmutableArray<string> metadataNames,
            ItemSpec<P, I> itemSpec)
        {
            var (comparer, baseDirectory) = GetComparisonPolicy(options);
            int metadataCount = metadataNames.Length;
            Dictionary<string, MetadataTrie<P, I>> rootChildren = null;

            // Keep this enumeration concrete to avoid boxing the immutable array or its enumerator.
            foreach (ItemSpec<P, I>.ItemExpressionFragment fragment in itemSpec.Fragments)
            {
                foreach (ItemSpec<P, I>.ReferencedItem source in fragment.ReferencedItems)
                {
                    if (metadataCount == 0)
                    {
                        continue;
                    }

                    rootChildren ??= [with(comparer)];
                    Dictionary<string, MetadataTrie<P, I>> currentChildren = rootChildren;
                    int level = 0;
                    foreach (string name in metadataNames)
                    {
                        string value = source.Item.GetMetadataValue(name);
                        bool hasMoreMetadata = level < metadataCount - 1;
                        MetadataTrie<P, I> child = GetOrAddChild(currentChildren, value, hasMoreMetadata, comparer, baseDirectory);
                        currentChildren = child._children;
                        level++;
                    }
                }
            }

            return new(rootChildren, baseDirectory);
        }

        /// <summary>
        ///  Creates a trie from the captured source items using the specified metadata comparison policy.
        /// </summary>
        /// <param name="options">The comparison and normalization policy.</param>
        /// <param name="metadataNames">The metadata names, whose enumeration order must remain consistent during construction and lookup.</param>
        /// <param name="itemSpec">The captured source item references.</param>
        /// <returns>
        ///  The completed trie, including an empty trie when no source tuples contribute child edges.
        /// </returns>
        public static MetadataTrie<P, I> Create(
            MatchOnMetadataOptions options,
            HashSet<string> metadataNames,
            ItemSpec<P, I> itemSpec)
        {
            var (comparer, baseDirectory) = GetComparisonPolicy(options);
            int metadataCount = metadataNames.Count;
            Dictionary<string, MetadataTrie<P, I>> rootChildren = null;

            // The execution-time caller's HashSet enumerator also stays unboxed.
            foreach (ItemSpec<P, I>.ItemExpressionFragment fragment in itemSpec.Fragments)
            {
                foreach (ItemSpec<P, I>.ReferencedItem source in fragment.ReferencedItems)
                {
                    if (metadataCount == 0)
                    {
                        continue;
                    }

                    rootChildren ??= [with(comparer)];
                    Dictionary<string, MetadataTrie<P, I>> currentChildren = rootChildren;
                    int level = 0;
                    foreach (string name in metadataNames)
                    {
                        string value = source.Item.GetMetadataValue(name);
                        bool hasMoreMetadata = level < metadataCount - 1;
                        MetadataTrie<P, I> child = GetOrAddChild(currentChildren, value, hasMoreMetadata, comparer, baseDirectory);
                        currentChildren = child._children;
                        level++;
                    }
                }
            }

            return new(rootChildren, baseDirectory);
        }

        private static (StringComparer Comparer, string BaseDirectory) GetComparisonPolicy(MatchOnMetadataOptions options)
        {
            StringComparer comparer = options switch
            {
                MatchOnMetadataOptions.CaseSensitive => StringComparer.Ordinal,
                MatchOnMetadataOptions.CaseInsensitive => StringComparer.OrdinalIgnoreCase,
                _ => FileUtilities.PathComparison == StringComparison.OrdinalIgnoreCase
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal,
            };

            string baseDirectory = null;
            if (options == MatchOnMetadataOptions.PathLike)
            {
                // Capture the project's thread-local directory rather than an unrelated process directory.
                // Construction and every later lookup must normalize against this same context.
                baseDirectory = FileUtilities.CurrentThreadWorkingDirectory is { Length: > 0 } threadWorkingDirectory
                    ? threadWorkingDirectory
                    : Environment.CurrentDirectory;
            }

            return (comparer, baseDirectory);
        }

        /// <summary>
        ///  Finds or creates one edge, allocating child storage only when the source tuple has another level.
        /// </summary>
        /// <param name="children">The current level's child dictionary.</param>
        /// <param name="value">The source item's metadata value.</param>
        /// <param name="hasMoreMetadata">Whether another metadata value follows this one.</param>
        /// <param name="comparer">The comparison policy for newly created child dictionaries.</param>
        /// <param name="baseDirectory">The captured directory for path-like values, or <see langword="null"/> for unchanged values.</param>
        /// <returns>
        ///  The child node for the normalized metadata value.
        /// </returns>
        private static MetadataTrie<P, I> GetOrAddChild(
            Dictionary<string, MetadataTrie<P, I>> children,
            string value,
            bool hasMoreMetadata,
            StringComparer comparer,
            string baseDirectory)
        {
            string normalizedValue = NormalizeValue(value, baseDirectory);
            if (!children.TryGetValue(normalizedValue, out var child))
            {
                Dictionary<string, MetadataTrie<P, I>> childDictionary = null;
                if (hasMoreMetadata)
                {
                    childDictionary = [with(comparer)];
                }

                child = new MetadataTrie<P, I>(childDictionary, baseDirectory: null);
                children.Add(normalizedValue, child);
            }

            return child;
        }

        private static string NormalizeValue(string value, string baseDirectory)
            => baseDirectory is null ? value : FileUtilities.NormalizePathForComparisonNoThrow(value, baseDirectory);

        internal bool Contains(IEnumerable<string> metadata)
        {
            MetadataTrie<P, I> current = this;

            foreach (string m in metadata)
            {
                if (m.IsNullOrEmpty())
                {
                    return false;
                }

                string normalizedValue = NormalizeValue(m, _baseDirectory);
                if (current._children is null || !current._children.TryGetValue(normalizedValue, out var child))
                {
                    return false;
                }

                current = child;
            }

            return true;
        }

        /// <summary>
        ///  Looks up an item's metadata tuple without allocating a projected sequence of values.
        /// </summary>
        /// <param name="item">The candidate item.</param>
        /// <param name="metadataNames">The same ordered metadata names used when constructing this trie.</param>
        /// <returns>
        ///  <see langword="true"/> when all nonempty values follow a stored tuple; otherwise, <see langword="false"/>.
        /// </returns>
        public bool Contains(I item, ImmutableArray<string> metadataNames)
        {
            MetadataTrie<P, I> current = this;

            foreach (string name in metadataNames)
            {
                string value = item.GetMetadataValue(name);
                if (value.IsNullOrEmpty())
                {
                    return false;
                }

                string normalizedValue = NormalizeValue(value, _baseDirectory);
                if (current._children is null || !current._children.TryGetValue(normalizedValue, out var child))
                {
                    return false;
                }

                current = child;
            }

            return true;
        }
    }

    public enum MatchOnMetadataOptions
    {
        CaseSensitive,
        CaseInsensitive,
        PathLike
    }

    public static class MatchOnMetadataConstants
    {
        public const MatchOnMetadataOptions MatchOnMetadataOptionsDefaultValue = MatchOnMetadataOptions.CaseSensitive;
    }
}
