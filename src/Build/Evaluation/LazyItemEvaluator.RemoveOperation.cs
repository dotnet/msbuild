// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.Build.Shared;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
    {
        class RemoveOperation : LazyItemOperation
        {
            readonly ImmutableList<string> _matchOnMetadata;
            readonly MatchOnMetadataOptions _matchOnMetadataOptions;
            private MetadataSet metadataSet;

            public RemoveOperation(RemoveOperationBuilder builder, LazyItemEvaluator<P, I, M, D> lazyEvaluator)
                : base(builder, lazyEvaluator)
            {
                _matchOnMetadata = builder.MatchOnMetadata.ToImmutable();
                _matchOnMetadataOptions = builder.MatchOnMetadataOptions;
            }

            /// <summary>
            /// Apply the Remove operation.
            /// </summary>
            /// <remarks>
            /// This operation is mostly implemented in terms of the default <see cref="LazyItemOperation.ApplyImpl(ImmutableList{ItemData}.Builder, ImmutableHashSet{string})"/>.
            /// This override exists to apply the removing-everything short-circuit.
            /// </remarks>
            protected override void ApplyImpl(ImmutableList<ItemData>.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
                var matchOnMetadataValid = !_matchOnMetadata.IsEmpty && _itemSpec.Fragments.Count == 1
                    && _itemSpec.Fragments.First() is ItemSpec<ProjectProperty, ProjectItem>.ItemExpressionFragment;
                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(
                    _matchOnMetadata.IsEmpty || (matchOnMetadataValid && _matchOnMetadata.Count == 1),
                    new BuildEventFileInfo(string.Empty),
                    "OM_MatchOnMetadataIsRestrictedToOnlyOneReferencedItem");

                if (_matchOnMetadata.IsEmpty && ItemspecContainsASingleBareItemReference(_itemSpec, _itemElement.ItemType) && _conditionResult)
                {
                    // Perf optimization: If the Remove operation references itself (e.g. <I Remove="@(I)"/>)
                    // then all items are removed and matching is not necessary
                    listBuilder.Clear();
                    return;
                }

                base.ApplyImpl(listBuilder, globsToIgnore);
            }

            // todo Perf: do not match against the globs: https://github.com/Microsoft/msbuild/issues/2329
            protected override ImmutableList<I> SelectItems(ImmutableList<ItemData>.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
                var items = ImmutableHashSet.CreateBuilder<I>();
                foreach (ItemData item in listBuilder)
                {
                    if (_matchOnMetadata.IsEmpty ? _itemSpec.MatchesItem(item.Item) : MatchesItemOnMetadata(item.Item))
                        items.Add(item.Item);
                }

                return items.ToImmutableList();
            }

            private bool MatchesItemOnMetadata(I item)
            {
                if (metadataSet == null)
                {
                    metadataSet = new MetadataSet();
                    foreach (ItemSpec<P, I>.ItemExpressionFragment frag in _itemSpec.Fragments)
                    {
                        foreach (ItemSpec<P, I>.ReferencedItem referencedItem in frag.ReferencedItems)
                        {
                            metadataSet.Add(_matchOnMetadata.Select(m => (referencedItem.Item.GetMetadata(m) as ProjectMetadata).EvaluatedValue));
                        }
                    }
                }

                return metadataSet.Contains(_matchOnMetadata.Select(m => (item.GetMetadata(m) as ProjectMetadata).EvaluatedValue));
            }

            protected override void SaveItems(ImmutableList<I> items, ImmutableList<ItemData>.Builder listBuilder)
            {
                if (!_conditionResult)
                {
                    return;
                }

                listBuilder.RemoveAll(itemData => items.Contains(itemData.Item));
            }

            public ImmutableHashSet<string>.Builder GetRemovedGlobs()
            {
                var builder = ImmutableHashSet.CreateBuilder<string>();

                if (!_conditionResult)
                {
                    return builder;
                }

                var globs = _itemSpec.Fragments.OfType<GlobFragment>().Select(g => g.TextFragment);

                builder.UnionWith(globs);

                return builder;
            }
        }

        class RemoveOperationBuilder : OperationBuilder
        {
            public ImmutableList<string>.Builder MatchOnMetadata { get; } = ImmutableList.CreateBuilder<string>();

            public MatchOnMetadataOptions MatchOnMetadataOptions { get; set; }

            public RemoveOperationBuilder(ProjectItemElement itemElement, bool conditionResult) : base(itemElement, conditionResult)
            {
            }
        }
    }

    internal class MetadataSet
    {
        private Dictionary<string, MetadataSet> children;

        internal MetadataSet()
        {
            children = new Dictionary<string, MetadataSet>();
        }

        // Relies on IEnumerable returning the metadata in a reasonable order. Reasonable?
        internal void Add(IEnumerable<string> metadata)
        {
            MetadataSet current = this;
            foreach (string s in metadata)
            {
                if (current.children.TryGetValue(s, out MetadataSet child))
                {
                    current = child;
                }
                else
                {
                    current.children.Add(s, new MetadataSet());
                    current = current.children[s];
                }
            }
        }

        internal bool Contains(IEnumerable<string> metadata)
        {
            List<string> metadataList = metadata.ToList();
            return this.Contains(metadataList, 0);
        }

        private bool Contains(List<string> metadata, int index)
        {
            if (index == metadata.Count)
            {
                return true;
            }
            else if (String.IsNullOrEmpty(metadata[index]))
            {
                return children.Any(kvp => !String.IsNullOrEmpty(kvp.Key) && kvp.Value.Contains(metadata, index + 1));
            }
            else
            {
                return (children.TryGetValue(metadata[index], out MetadataSet child) && child.Contains(metadata, index + 1)) ||
                    (children.TryGetValue(string.Empty, out MetadataSet emptyChild) && emptyChild.Contains(metadata, index + 1));
            }
        }
    }

    public enum MatchOnMetadataOptions
    {
        CaseSensitive,
        CaseInsensitive,
        PathLike
    }

    public static class MatchOnMetadataConstants {
        public const MatchOnMetadataOptions MatchOnMetadataOptionsDefaultValue = MatchOnMetadataOptions.CaseSensitive;
    }
}
