// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using System.Linq;
using System.IO;
using Newtonsoft.Json;

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

            if (mergeExisting)
            {
                var existingItems = FindExistingItems(item, destinationItemGroup.ContainingProject);

                foreach (var existingItem in existingItems)
                {
                    var mergeResult = MergeItems(item, existingItem);
                    item = mergeResult.InputItem;

                    // Existing Item is null when it's entire set of includes has been merged with the MergeItem
                    if (mergeResult.ExistingItem == null)
                    {
                        existingItem.Parent.RemoveChild(existingItem);
                    }

                    Execute(mergeResult.MergedItem, destinationItemGroup);
                }

                // Item will be null only when it's entire set of includes is merged with existing items
                if (item != null)
                {
                    Execute(item, destinationItemGroup);
                }
            }
            else
            {
                Execute(item, destinationItemGroup);
            }
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
            item.RemoveIncludes(commonIncludes);
            existingItem.RemoveIncludes(commonIncludes);

            var mergedItem = _projectElementGenerator.AddItem(item.ItemType, string.Join(";", commonIncludes));

            mergedItem.UnionExcludes(existingItem.Excludes());
            mergedItem.UnionExcludes(item.Excludes());

            mergedItem.AddMetadata(existingItem.Metadata);
            mergedItem.AddMetadata(item.Metadata);

            var mergeResult = new MergeResult
            {
                InputItem = string.IsNullOrEmpty(item.Include) ? null : item,
                ExistingItem = string.IsNullOrEmpty(existingItem.Include) ? null : existingItem,
                MergedItem = mergedItem
            };

            return mergeResult;
        }

        private IEnumerable<ProjectItemElement> FindExistingItems(ProjectItemElement item, ProjectRootElement project)
        {
                return project.ItemsWithoutConditions()
                    .Where(i => string.Equals(i.ItemType, item.ItemType, StringComparison.Ordinal))
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
