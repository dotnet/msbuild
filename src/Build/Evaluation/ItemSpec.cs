// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Build.Globbing;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.EscapingStringExtensions;

namespace Microsoft.Build.Evaluation
{

    /// <summary>
    /// Represents the elements of an item specification string and 
    /// provides some operations over them (like matching items against a given ItemSpec)
    /// </summary>
    internal class ItemSpec<P, I>
        where P : class, IProperty
        where I : class, IItem
    {
        public string ItemSpecString { get; }

        /// <summary>
        /// The fragments that compose an item spec string (values, globs, item references)
        /// </summary>
        public List<ItemFragment> Fragments { get; }

        /// <summary>
        /// The expander needs to have a default item factory set.
        /// </summary>
        // todo Make this type immutable. Dealing with an Expander change is painful. See the ItemExpressionFragment
        public Expander<P, I> Expander { get; set; }

        /// <summary>
        /// The xml attribute where this itemspec comes from
        /// </summary>
        public IElementLocation ItemSpecLocation { get; }

        /// <param name="itemSpec">The string containing item syntax</param>
        /// <param name="expander">Expects the expander to have a default item factory set</param>
        /// <param name="itemSpecLocation">The xml location the itemspec comes from</param>
        /// <param name="projectDirectory">The directory that the project is in.</param>
        /// <param name="expandProperties">Expand properties before breaking down fragments. Defaults to true</param>
        public ItemSpec(string itemSpec, Expander<P, I> expander, IElementLocation itemSpecLocation, string projectDirectory, bool expandProperties = true)
        {
            ItemSpecString = itemSpec;
            Expander = expander;
            ItemSpecLocation = itemSpecLocation;

            Fragments = BuildItemFragments(itemSpecLocation, projectDirectory, expandProperties);
        }

        private List<ItemFragment> BuildItemFragments(IElementLocation itemSpecLocation, string projectDirectory, bool expandProperties)
        {
            //  Code corresponds to Evaluator.CreateItemsFromInclude
            var evaluatedItemspecEscaped = ItemSpecString;

            if (string.IsNullOrEmpty(evaluatedItemspecEscaped))
            {
                return new List<ItemFragment>();
            }

            // STEP 1: Expand properties in Include
            if (expandProperties)
            {
                evaluatedItemspecEscaped = Expander.ExpandIntoStringLeaveEscaped(ItemSpecString, ExpanderOptions.ExpandProperties, itemSpecLocation);
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
            var fragments = new List<ItemFragment>(semicolonCount + 1);

            // STEP 2: Split Include on any semicolons, and take each split in turn
            if (evaluatedItemspecEscaped.Length > 0)
            {
                var splitsEscaped = ExpressionShredder.SplitSemiColonSeparatedList(evaluatedItemspecEscaped);

                foreach (var splitEscaped in splitsEscaped)
                {
                    // STEP 3: If expression is "@(x)" copy specified list with its metadata, otherwise just treat as string
                    bool isItemListExpression;
                    var itemReferenceFragment = ProcessItemExpression(splitEscaped, itemSpecLocation, projectDirectory, out isItemListExpression);

                    if (isItemListExpression)
                    {
                        fragments.Add(itemReferenceFragment);
                    }
                    else
                    {
                        // The expression is not of the form "@(X)". Treat as string

                        //  Code corresponds to EngineFileUtilities.GetFileList
                        var containsEscapedWildcards = EscapingUtilities.ContainsEscapedWildcards(splitEscaped);
                        var containsRealWildcards = FileMatcher.HasWildcards(splitEscaped);

                        // '*' is an illegal character to have in a filename.
                        // todo: file-system assumption on legal path characters: https://github.com/Microsoft/msbuild/issues/781
                        if (containsEscapedWildcards && containsRealWildcards)
                        {

                            // Just return the original string.
                            fragments.Add(new ValueFragment(splitEscaped, projectDirectory));
                        }
                        else if (!containsEscapedWildcards && containsRealWildcards)
                        {
                            // Unescape before handing it to the filesystem.
                            var filespecUnescaped = EscapingUtilities.UnescapeAll(splitEscaped);

                            fragments.Add(new GlobFragment(filespecUnescaped, projectDirectory));
                        }
                        else
                        {
                            // No real wildcards means we just return the original string.  Don't even bother 
                            // escaping ... it should already be escaped appropriately since it came directly
                            // from the project file

                            fragments.Add(new ValueFragment(splitEscaped, projectDirectory));
                        }
                    }
                }
            }

            return fragments;
        }

        private ItemExpressionFragment<P, I> ProcessItemExpression(string expression, IElementLocation elementLocation, string projectDirectory, out bool isItemListExpression)
        {
            isItemListExpression = false;

            //  Code corresponds to Expander.ExpandSingleItemVectorExpressionIntoItems
            if (expression.Length == 0)
            {
                return null;
            }

            var capture = Expander<P, I>.ExpandSingleItemVectorExpressionIntoExpressionCapture(expression, ExpanderOptions.ExpandItems, elementLocation);

            if (capture == null)
            {
                return null;
            }

            isItemListExpression = true;

            return new ItemExpressionFragment<P, I>(capture, expression, this, projectDirectory);
        }

        /// <summary>
        /// Return true if the given <paramref name="item"/> matches this itemspec
        /// </summary>
        /// <param name="item">The item to attempt to find a match for.</param>
        public bool MatchesItem(I item)
        {
            // Avoid unnecessary LINQ/Func/Enumerator allocations on this path, this is called a lot

            string evaluatedInclude = item.EvaluatedInclude;
            foreach (ItemFragment fragment in Fragments)
            {
                if (fragment.MatchCount(evaluatedInclude) > 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Return the fragments that match against the given <paramref name="itemToMatch"/>
        /// </summary>
        /// <param name="itemToMatch">The item to match.</param>
        /// <param name="matches">
        /// Total number of matches. Some fragments match more than once (item expression may contain multiple instances of <paramref name="itemToMatch"/>)
        /// </param>
        public IEnumerable<ItemFragment> FragmentsMatchingItem(string itemToMatch, out int matches)
        {
            var result = new List<ItemFragment>(Fragments.Count());
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
        /// Return an MSBuildGlob that represents this ItemSpec.
        /// </summary>
        public IMSBuildGlob ToMSBuildGlob()
        {
            return new CompositeGlob(Fragments.Select(f => f.ToMSBuildGlob()));
        }

        /// <summary>
        ///     Returns all the fragment strings that represent it.
        ///     "1;*;2;@(foo)" gets returned as ["1", "2", "*", "a", "b"], given that @(foo)=["a", "b"]
        /// 
        ///     Order is not preserved. Globs are not expanded. Item expressions get replaced with their referring item instances.
        /// </summary>
        public IEnumerable<string> FlattenFragmentsAsStrings()
        {
            foreach (var fragment in Fragments)
            {
                if (fragment is ValueFragment || fragment is GlobFragment)
                {
                    yield return fragment.ItemSpecFragment;
                }
                else if (fragment is ItemExpressionFragment<P, I>)
                {
                    var itemExpression = (ItemExpressionFragment<P, I>) fragment;

                    foreach (var referencedItem in itemExpression.ReferencedItems)
                    {
                        yield return referencedItem.ItemSpecFragment;
                    }
                }
                else
                {
                    ErrorUtilities.ThrowInternalErrorUnreachable();
                }
            }
        }
		
        public override string ToString()
        {
            return ItemSpecString;
        }
    }

    internal abstract class ItemFragment
    {
        /// <summary>
        /// The substring from the original itemspec representing this fragment
        /// </summary>
        public string ItemSpecFragment { get; }

        /// <summary>
        /// Path of the project the itemspec is coming from
        /// </summary>
        protected string ProjectDirectory { get; }

        private bool _fileMatcherInitialized;
        private FileSpecMatcherTester _fileMatcher;

        // not a Lazy to reduce memory
        private FileSpecMatcherTester FileMatcher
        {
            get
            {
                if (_fileMatcherInitialized)
                {
                    return _fileMatcher;
                }

                _fileMatcher = CreateFileSpecMatcher();
                _fileMatcherInitialized = true;

                return _fileMatcher;
            }
        }

        private IMSBuildGlob _msbuildGlob;

        // not a Lazy to reduce memory
        protected virtual IMSBuildGlob MsBuildGlob
        {
            get
            {
                if (_msbuildGlob == null)
                {
                    _msbuildGlob = CreateMsBuildGlob();
                }

                return _msbuildGlob;
            }
        }

        protected ItemFragment(string itemSpecFragment, string projectDirectory)
        {
            ItemSpecFragment = itemSpecFragment;
            ProjectDirectory = projectDirectory;
        }

        /// <returns>The number of times the <param name="itemToMatch"></param> appears in this fragment</returns>
        public virtual int MatchCount(string itemToMatch)
        {
            return FileMatcher.IsMatch(itemToMatch) ? 1 : 0;
        }

        public virtual IMSBuildGlob ToMSBuildGlob()
        {
            return MsBuildGlob;
        }

        protected virtual IMSBuildGlob CreateMsBuildGlob()
        {
            return Globbing.MSBuildGlob.Parse(ProjectDirectory, ItemSpecFragment.Unescape());
        }

        private FileSpecMatcherTester CreateFileSpecMatcher()
        {
            return FileSpecMatcherTester.Parse(ProjectDirectory, ItemSpecFragment);
        }
    }

    internal class ValueFragment : ItemFragment
    {
        public ValueFragment(string itemSpecFragment, string projectDirectory)
            : base(itemSpecFragment, projectDirectory)
        {
        }
    }

    internal class GlobFragment : ItemFragment
    {
        public GlobFragment(string itemSpecFragment, string projectDirectory)
            : base(itemSpecFragment, projectDirectory)
        {
        }
    }

    internal class ItemExpressionFragment<P, I> : ItemFragment
        where P : class, IProperty
        where I : class, IItem
    {
        public ExpressionShredder.ItemExpressionCapture Capture { get; }

        private readonly ItemSpec<P, I> _containingItemSpec;
        private Expander<P, I> _expander;

        private IList<ValueFragment> _referencedItems;
        public IList<ValueFragment> ReferencedItems
        {
            get
            {
                InitReferencedItemsIfNecessary();
                return _referencedItems;
            }
        }

        private IMSBuildGlob _msbuildGlob;
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

        public ItemExpressionFragment(ExpressionShredder.ItemExpressionCapture capture, string itemSpecFragment, ItemSpec<P, I> containingItemSpec, string projectDirectory)
            : base(itemSpecFragment, projectDirectory)
        {
            Capture = capture;

            _containingItemSpec = containingItemSpec;
            _expander = _containingItemSpec.Expander;
        }

        public override int MatchCount(string itemToMatch)
        {

            return ReferencedItems.Count(v => v.MatchCount(itemToMatch) > 0);
        }

        public override IMSBuildGlob ToMSBuildGlob()
        {
            return MsBuildGlob;
        }

        protected override IMSBuildGlob CreateMsBuildGlob()
        {
            return new CompositeGlob(ReferencedItems.Select(i => i.ToMSBuildGlob()));
        }

        private bool InitReferencedItemsIfNecessary()
        {
            // cache referenced items as long as the expander does not change
            // reference equality works for now since the expander cannot mutate its item state (hopefully it stays that way)
            if (_referencedItems == null || _expander != _containingItemSpec.Expander)
            {
                _expander = _containingItemSpec.Expander;

                List<Pair<string, I>> itemsFromCapture;
                bool throwaway;
                _expander.ExpandExpressionCapture(
                    Capture,
                    _containingItemSpec.ItemSpecLocation,
                    ExpanderOptions.ExpandItems,
                    false /* do not include null expansion results */,
                    out throwaway,
                    out itemsFromCapture);
                _referencedItems = itemsFromCapture.Select(i => new ValueFragment(i.Key, ProjectDirectory)).ToList();

                return true;
            }

            return false;
        }
    }
}
