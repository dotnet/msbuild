// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Construction;
using System.Linq;

namespace Microsoft.DotNet.ProjectJsonMigration.Transforms
{
    public class TransformApplicator : ITransformApplicator
    {
        private readonly ProjectRootElement _projectElementGenerator = ProjectRootElement.Create();

        public void Execute<T, U>(
            T element,
            U destinationElement) where T : ProjectElement where U : ProjectElementContainer
        {
            if (element != null)
            {
                if (typeof(T) == typeof(ProjectItemElement))
                {
                    var item = destinationElement.ContainingProject.CreateItemElement("___TEMP___");
                    item.CopyFrom(element);

                    destinationElement.AppendChild(item);
                    item.AddMetadata((element as ProjectItemElement).Metadata);
                }
                else if (typeof(T) == typeof(ProjectPropertyElement))
                {
                    MigrationTrace.Instance.WriteLine(
                        $"{nameof(TransformApplicator)}: Adding Property to project {(element as ProjectPropertyElement).Name}");
                    var property = destinationElement.ContainingProject.CreatePropertyElement("___TEMP___");
                    property.CopyFrom(element);

                    destinationElement.AppendChild(property);
                }
                else
                {
                    throw new Exception("Unable to add unknown project element to project");
                }
            }
        }

        public void Execute<T, U>(
            IEnumerable<T> elements,
            U destinationElement) where T : ProjectElement where U : ProjectElementContainer
        {
            foreach (var element in elements)
            {
                Execute(element, destinationElement);
            }
        }

        public void Execute(
            ProjectItemElement item,
            ProjectItemGroupElement destinationItemGroup,
            bool mergeExisting)
        {
            if (item == null)
            {
                return;
            }

            MigrationTrace.Instance.WriteLine($"{nameof(TransformApplicator)}: Item {{ ItemType: {item.ItemType}, Condition: {item.Condition}, Include: {item.Include}, Exclude: {item.Exclude} }}");
            MigrationTrace.Instance.WriteLine($"{nameof(TransformApplicator)}: ItemGroup {{ Condition: {destinationItemGroup.Condition} }}");

            if (mergeExisting)
            {
                item = MergeWithExistingItemsWithSameCondition(item, destinationItemGroup);

                // Item will be null when it's entire set of includes has been merged.
                if (item == null)
                {
                    MigrationTrace.Instance.WriteLine($"{nameof(TransformApplicator)}: Item completely merged");
                    return;
                }

                item = MergeWithExistingItemsWithDifferentCondition(item, destinationItemGroup);

                // Item will be null when it is equivalent to a conditionless item
                if (item == null)
                {
                    MigrationTrace.Instance.WriteLine($"{nameof(TransformApplicator)}: Item c");
                    return;
                }
            }

            Execute(item, destinationItemGroup);
        }

        private ProjectItemElement MergeWithExistingItemsWithDifferentCondition(ProjectItemElement item, ProjectItemGroupElement destinationItemGroup)
        {
            var existingItemsWithDifferentCondition =
                    FindExistingItemsWithDifferentCondition(item, destinationItemGroup.ContainingProject, destinationItemGroup);

            MigrationTrace.Instance.WriteLine($"{nameof(TransformApplicator)}: Merging Item with {existingItemsWithDifferentCondition.Count()} existing items with a different condition chain.");

            foreach (var existingItem in existingItemsWithDifferentCondition)
            {
                var encompassedIncludes = existingItem.GetEncompassedIncludes(item);
                if (encompassedIncludes.Any())
                {
                    MigrationTrace.Instance.WriteLine($"{nameof(TransformApplicator)}: encompassed includes {string.Join(", ", encompassedIncludes)}");
                    item.RemoveIncludes(encompassedIncludes);
                    if (!item.Includes().Any())
                    {
                        MigrationTrace.Instance.WriteLine($"{nameof(TransformApplicator)}: Ignoring Item {{ ItemType: {existingItem.ItemType}, Condition: {existingItem.Condition}, Include: {existingItem.Include}, Exclude: {existingItem.Exclude} }}");
                        return null;
                    }
                }
            }

            // If we haven't returned, and there are existing items with a separate condition, we need to 
            // overwrite with those items inside the destinationItemGroup by using a Remove
            // Unless this is a conditionless item, in which case this the conditioned items should be doing the
            // overwriting.
            if (existingItemsWithDifferentCondition.Any() && 
                (item.ConditionChain().Count() > 0 || destinationItemGroup.ConditionChain().Count() > 0))
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

                    Execute(clearPreviousItem, destinationItemGroup);
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
                Execute(mergeResult.MergedItem, destinationItemGroup);
            }

            return item;
        }

        public void Execute(
            IEnumerable<ProjectItemElement> items,
            ProjectItemGroupElement destinationItemGroup,
            bool mergeExisting)
        {
            foreach (var item in items)
            {
                Execute(item, destinationItemGroup, mergeExisting);
            }
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

            mergedItem.AddMetadata(existingItem.Metadata);
            mergedItem.AddMetadata(item.Metadata);

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

        private IEnumerable<ProjectItemElement> FindExistingItemsWithDifferentCondition(
            ProjectItemElement item,
            ProjectRootElement project,
            ProjectElementContainer destinationContainer)
        {
            return project.Items
                .Where(i => !i.ConditionChainsAreEquivalent(item) || !i.Parent.ConditionChainsAreEquivalent(destinationContainer))
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
