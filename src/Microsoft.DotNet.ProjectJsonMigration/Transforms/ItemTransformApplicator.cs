// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Construction;
using System.Linq;

namespace Microsoft.DotNet.ProjectJsonMigration.Transforms
{
    public class ItemTransformApplicator : ITransformApplicator
    {
        private readonly ProjectRootElement _projectElementGenerator = ProjectRootElement.Create();

        public void Execute<T, U>(
            T element,
            U destinationElement,
            bool mergeExisting) where T : ProjectElement where U : ProjectElementContainer
        {
            if (typeof(T) != typeof(ProjectItemElement))
            {
                throw new ArgumentException($"Expected element to be of type {nameof(ProjectItemElement)}, but got {typeof(T)}");
            }

            if (typeof(U) != typeof(ProjectItemGroupElement))
            {
                throw new ArgumentException($"Expected destinationElement to be of type {nameof(ProjectItemGroupElement)}, but got {typeof(U)}");
            }

            if (element == null)
            {
                return;
            }

            if (destinationElement == null)
            {
                throw new ArgumentException("expected destinationElement to not be null");
            }

            var item = element as ProjectItemElement;
            var destinationItemGroup = destinationElement as ProjectItemGroupElement;

            MigrationTrace.Instance.WriteLine($"{nameof(ItemTransformApplicator)}: Item {{ ItemType: {item.ItemType}, Condition: {item.Condition}, Include: {item.Include}, Exclude: {item.Exclude} }}");
            MigrationTrace.Instance.WriteLine($"{nameof(ItemTransformApplicator)}: ItemGroup {{ Condition: {destinationItemGroup.Condition} }}");

            if (mergeExisting)
            {
                // Don't duplicate items or includes
                item = MergeWithExistingItemsWithSameCondition(item, destinationItemGroup);
                if (item == null)
                {
                    MigrationTrace.Instance.WriteLine($"{nameof(ItemTransformApplicator)}: Item completely merged");
                    return;
                }

                // Handle duplicate includes between different conditioned items
                item = MergeWithExistingItemsWithNoCondition(item, destinationItemGroup);
                if (item == null)
                {
                    MigrationTrace.Instance.WriteLine($"{nameof(ItemTransformApplicator)}: Item completely merged");
                    return;
                }

                item = MergeWithExistingItemsWithACondition(item, destinationItemGroup);
                if (item == null)
                {
                    MigrationTrace.Instance.WriteLine($"{nameof(ItemTransformApplicator)}: Item completely merged");
                    return;
                }
            }

            AddItemToItemGroup(item, destinationItemGroup);
        }

        public void Execute<T, U>(
            IEnumerable<T> elements,
            U destinationElement,
            bool mergeExisting) where T : ProjectElement where U : ProjectElementContainer
        {
            foreach (var element in elements)
            {
                Execute(element, destinationElement, mergeExisting);
            }
        }

        private void AddItemToItemGroup(ProjectItemElement item, ProjectItemGroupElement itemGroup)
        {
            var outputItem = itemGroup.ContainingProject.CreateItemElement("___TEMP___");
            outputItem.CopyFrom(item);

            itemGroup.AppendChild(outputItem);
            outputItem.AddMetadata(item.Metadata);
        }

        private ProjectItemElement MergeWithExistingItemsWithACondition(ProjectItemElement item, ProjectItemGroupElement destinationItemGroup)
        {
            // This logic only applies to conditionless items
            if (item.ConditionChain().Any() || destinationItemGroup.ConditionChain().Any())
            {
                return item;
            }

            var existingItemsWithACondition =
                    FindExistingItemsWithACondition(item, destinationItemGroup.ContainingProject, destinationItemGroup);

            MigrationTrace.Instance.WriteLine($"{nameof(ItemTransformApplicator)}: Merging Item with {existingItemsWithACondition.Count()} existing items with a different condition chain.");

            foreach (var existingItem in existingItemsWithACondition)
            {
                // If this item is encompassing items in a condition, remove the encompassed includes from the existing item
                var encompassedIncludes = item.GetEncompassedIncludes(existingItem);
                if (encompassedIncludes.Any())
                {
                    MigrationTrace.Instance.WriteLine($"{nameof(ItemTransformApplicator)}: encompassed includes {string.Join(", ", encompassedIncludes)}");
                    existingItem.RemoveIncludes(encompassedIncludes);
                }

                // continue if the existing item is now empty
                if (!existingItem.Includes().Any())
                {
                    MigrationTrace.Instance.WriteLine($"{nameof(ItemTransformApplicator)}: Removing Item {{ ItemType: {existingItem.ItemType}, Condition: {existingItem.Condition}, Include: {existingItem.Include}, Exclude: {existingItem.Exclude} }}");
                    existingItem.Parent.RemoveChild(existingItem);
                    continue;
                }

                // If we haven't continued, the existing item may have includes 
                // that need to be removed before being redefined, to avoid duplicate includes
                // Create or merge with existing remove
                var remainingIntersectedIncludes = existingItem.IntersectIncludes(item);

                if (remainingIntersectedIncludes.Any())
                {
                    var existingRemoveItem = destinationItemGroup.Items
                        .Where(i =>
                            string.IsNullOrEmpty(i.Include)
                            && string.IsNullOrEmpty(i.Exclude)
                            && !string.IsNullOrEmpty(i.Remove))
                        .FirstOrDefault();

                    if (existingRemoveItem != null)
                    {
                        var removes = new HashSet<string>(existingRemoveItem.Remove.Split(';'));
                        foreach (var include in remainingIntersectedIncludes)
                        {
                            removes.Add(include);
                        }
                        existingRemoveItem.Remove = string.Join(";", removes);
                    }
                    else
                    {
                        var clearPreviousItem = _projectElementGenerator.CreateItemElement(item.ItemType);
                        clearPreviousItem.Remove = string.Join(";", remainingIntersectedIncludes);

                        AddItemToItemGroup(clearPreviousItem, existingItem.Parent as ProjectItemGroupElement);
                    }
                }
            }

            return item;
        }

        private ProjectItemElement MergeWithExistingItemsWithNoCondition(ProjectItemElement item, ProjectItemGroupElement destinationItemGroup)
        {
            // This logic only applies to items being placed into a condition
            if (!item.ConditionChain().Any() && !destinationItemGroup.ConditionChain().Any())
            {
                return item;
            }

            var existingItemsWithNoCondition =
                    FindExistingItemsWithNoCondition(item, destinationItemGroup.ContainingProject, destinationItemGroup);

            MigrationTrace.Instance.WriteLine($"{nameof(ItemTransformApplicator)}: Merging Item with {existingItemsWithNoCondition.Count()} existing items with a different condition chain.");

            // Handle the item being placed inside of a condition, when it is overlapping with a conditionless item
            // If it is not definining new metadata or excludes, the conditioned item can be merged with the 
            // conditionless item
            foreach (var existingItem in existingItemsWithNoCondition)
            {
                var encompassedIncludes = existingItem.GetEncompassedIncludes(item);
                if (encompassedIncludes.Any())
                {
                    MigrationTrace.Instance.WriteLine($"{nameof(ItemTransformApplicator)}: encompassed includes {string.Join(", ", encompassedIncludes)}");
                    item.RemoveIncludes(encompassedIncludes);
                    if (!item.Includes().Any())
                    {
                        MigrationTrace.Instance.WriteLine($"{nameof(ItemTransformApplicator)}: Ignoring Item {{ ItemType: {existingItem.ItemType}, Condition: {existingItem.Condition}, Include: {existingItem.Include}, Exclude: {existingItem.Exclude} }}");
                        return null;
                    }
                }
            }

            // If we haven't returned, and there are existing items with a separate condition, we need to 
            // overwrite with those items inside the destinationItemGroup by using a Remove
            if (existingItemsWithNoCondition.Any())
            {
                // Merge with the first remove if possible
                var existingRemoveItem = destinationItemGroup.Items
                    .Where(i =>
                        string.IsNullOrEmpty(i.Include)
                        && string.IsNullOrEmpty(i.Exclude)
                        && !string.IsNullOrEmpty(i.Remove))
                    .FirstOrDefault();

                if (existingRemoveItem != null)
                {
                    existingRemoveItem.Remove += ";" + item.Include;
                }
                else
                {
                    var clearPreviousItem = _projectElementGenerator.CreateItemElement(item.ItemType);
                    clearPreviousItem.Remove = item.Include;

                    AddItemToItemGroup(clearPreviousItem, destinationItemGroup);
                }
            }

            return item;
        }

        private ProjectItemElement MergeWithExistingItemsWithSameCondition(ProjectItemElement item, ProjectItemGroupElement destinationItemGroup)
        {
            var existingItemsWithSameCondition =
                   FindExistingItemsWithSameCondition(item, destinationItemGroup.ContainingProject, destinationItemGroup);

            MigrationTrace.Instance.WriteLine($"{nameof(TransformApplicator)}: Merging Item with {existingItemsWithSameCondition.Count()} existing items with the same condition chain.");

            foreach (var existingItem in existingItemsWithSameCondition)
            {
                var mergeResult = MergeItems(item, existingItem);
                item = mergeResult.InputItem;

                // Existing Item is null when it's entire set of includes has been merged with the MergeItem
                if (mergeResult.ExistingItem == null)
                {
                    existingItem.Parent.RemoveChild(existingItem);
                }
                
                MigrationTrace.Instance.WriteLine($"{nameof(TransformApplicator)}: Adding Merged Item {{ ItemType: {mergeResult.MergedItem.ItemType}, Condition: {mergeResult.MergedItem.Condition}, Include: {mergeResult.MergedItem.Include}, Exclude: {mergeResult.MergedItem.Exclude} }}");
                AddItemToItemGroup(mergeResult.MergedItem, destinationItemGroup);
            }

            return item;
        }

        /// <summary>
        /// Merges two items on their common sets of includes.
        /// The output is 3 items, the 2 input items and the merged items. If the common
        /// set of includes spans the entirety of the includes of either of the 2 input
        /// items, that item will be returned as null.
        ///
        /// The 3rd output item, the merged item, will have the Union of the excludes and
        /// metadata from the 2 input items. If any metadata between the 2 input items is different,
        /// this will throw.
        ///
        /// This function will mutate the Include property of the 2 input items, removing the common subset.
        /// </summary>
        private MergeResult MergeItems(ProjectItemElement item, ProjectItemElement existingItem)
        {
            if (!string.Equals(item.ItemType, existingItem.ItemType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Cannot merge items of different types.");
            }

            if (!item.IntersectIncludes(existingItem).Any())
            {
                throw new InvalidOperationException("Cannot merge items without a common include.");
            }

            var commonIncludes = item.IntersectIncludes(existingItem).ToList();
            var mergedItem = _projectElementGenerator.AddItem(item.ItemType, string.Join(";", commonIncludes));

            mergedItem.UnionExcludes(existingItem.Excludes());
            mergedItem.UnionExcludes(item.Excludes());

            mergedItem.AddMetadata(MergeMetadata(existingItem.Metadata, item.Metadata));

            item.RemoveIncludes(commonIncludes);
            existingItem.RemoveIncludes(commonIncludes);

            var mergeResult = new MergeResult
            {
                InputItem = string.IsNullOrEmpty(item.Include) ? null : item,
                ExistingItem = string.IsNullOrEmpty(existingItem.Include) ? null : existingItem,
                MergedItem = mergedItem
            };

            return mergeResult;
        }

        private ICollection<ProjectMetadataElement> MergeMetadata(
            ICollection<ProjectMetadataElement> existingMetadataElements,
            ICollection<ProjectMetadataElement> newMetadataElements)
        {
            var mergedMetadata = new List<ProjectMetadataElement>(existingMetadataElements);

            foreach (var newMetadata in newMetadataElements)
            {
                var existingMetadata = mergedMetadata.FirstOrDefault(m =>
                    m.Name.Equals(newMetadata.Name, StringComparison.OrdinalIgnoreCase));
                if (existingMetadata == null)
                {
                    mergedMetadata.Add(newMetadata);
                }
                else
                {
                    MergeMetadata(existingMetadata, newMetadata);
                }
            }

            return mergedMetadata;
        }

        public void MergeMetadata(ProjectMetadataElement existingMetadata, ProjectMetadataElement newMetadata)
        {
            if (existingMetadata.Value != newMetadata.Value)
            {
                existingMetadata.Value = string.Join(";", new [] { existingMetadata.Value, newMetadata.Value });
            }
        }

        private IEnumerable<ProjectItemElement> FindExistingItemsWithSameCondition(
            ProjectItemElement item, 
            ProjectRootElement project,
            ProjectElementContainer destinationContainer)
        {
                return project.Items
                    .Where(i => i.Condition == item.Condition)
                    .Where(i => i.Parent.ConditionChainsAreEquivalent(destinationContainer))
                    .Where(i => i.ItemType == item.ItemType)
                    .Where(i => i.IntersectIncludes(item).Any());
        }

        private IEnumerable<ProjectItemElement> FindExistingItemsWithNoCondition(
            ProjectItemElement item,
            ProjectRootElement project,
            ProjectElementContainer destinationContainer)
        {
            return project.Items
                .Where(i => !i.ConditionChain().Any())
                .Where(i => i.ItemType == item.ItemType)
                .Where(i => i.IntersectIncludes(item).Any());
        }

        private IEnumerable<ProjectItemElement> FindExistingItemsWithACondition(
            ProjectItemElement item,
            ProjectRootElement project,
            ProjectElementContainer destinationContainer)
        {
            return project.Items
                .Where(i => i.ConditionChain().Any())
                .Where(i => i.ItemType == item.ItemType)
                .Where(i => i.IntersectIncludes(item).Any());
        }

        private class MergeResult
        {
            public ProjectItemElement InputItem { get; set; }
            public ProjectItemElement ExistingItem { get; set; }
            public ProjectItemElement MergedItem { get; set; }
        }
    }
}
