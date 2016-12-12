// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Construction;
using System.Linq;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal static class MSBuildExtensions
    {
        public static IEnumerable<string> GetEncompassedIncludes(this ProjectItemElement item, 
            ProjectItemElement otherItem)
        {
            if (otherItem.IsEquivalentToExceptIncludeAndExclude(item) && 
                new HashSet<string>(otherItem.Excludes()).IsSubsetOf(new HashSet<string>(item.Excludes())))
            {
                return otherItem.IntersectIncludes(item);
            }

            return Enumerable.Empty<string>();
        }

        public static bool IsEquivalentTo(this ProjectItemElement item, ProjectItemElement otherItem)
        {
            // Different includes
            if (item.IntersectIncludes(otherItem).Count() != item.Includes().Count())
            {
#if !DISABLE_TRACE
                MigrationTrace.Instance.WriteLine($"{nameof(MSBuildExtensions)}.{nameof(IsEquivalentTo)} includes not equivalent.");
#endif
                return false;
            }

            // Different Excludes
            if (item.IntersectExcludes(otherItem).Count() != item.Excludes().Count())
            {
#if !DISABLE_TRACE
                MigrationTrace.Instance.WriteLine($"{nameof(MSBuildExtensions)}.{nameof(IsEquivalentTo)} excludes not equivalent.");
#endif
                return false;
            }

            return item.IsEquivalentToExceptIncludeAndExclude(otherItem);
        }

        public static bool IsEquivalentToExceptIncludeAndExclude(this ProjectItemElement item, ProjectItemElement otherItem)
        {
            // Different remove
            if (item.Remove != otherItem.Remove)
            {
#if !DISABLE_TRACE
                MigrationTrace.Instance.WriteLine($"{nameof(MSBuildExtensions)}.{nameof(IsEquivalentTo)} removes not equivalent.");
#endif
                return false;
            }

            // Different Metadata
            var metadataTuples = otherItem.Metadata.Select(m => Tuple.Create(m, item)).Concat( 
                item.Metadata.Select(m => Tuple.Create(m, otherItem)));
            foreach (var metadataTuple in metadataTuples)
            {
                var metadata = metadataTuple.Item1;
                var itemToCompare = metadataTuple.Item2;

                var otherMetadata = itemToCompare.GetMetadataWithName(metadata.Name);
                if (otherMetadata == null)
                {
#if !DISABLE_TRACE
                    MigrationTrace.Instance.WriteLine($"{nameof(MSBuildExtensions)}.{nameof(IsEquivalentTo)} metadata doesn't exist {{ {metadata.Name} {metadata.Value} }}");
#endif
                    return false;
                }

                if (!metadata.ValueEquals(otherMetadata))
                {
#if !DISABLE_TRACE
                    MigrationTrace.Instance.WriteLine($"{nameof(MSBuildExtensions)}.{nameof(IsEquivalentTo)} metadata has another value {{ {metadata.Name} {metadata.Value} {otherMetadata.Value} }}");
#endif
                    return false;
                }
            }

            return true;
        }

        public static ISet<string> ConditionChain(this ProjectElement projectElement)
        {
            var conditionChainSet = new HashSet<string>();

            if (!string.IsNullOrEmpty(projectElement.Condition))
            {
                conditionChainSet.Add(projectElement.Condition);
            }

            foreach (var parent in projectElement.AllParents)
            {
                if (!string.IsNullOrEmpty(parent.Condition))
                {
                    conditionChainSet.Add(parent.Condition);
                }
            }

            return conditionChainSet;
        }

        public static bool ConditionChainsAreEquivalent(this ProjectElement projectElement, ProjectElement otherProjectElement)
        {
            return projectElement.ConditionChain().SetEquals(otherProjectElement.ConditionChain());
        }

        public static IEnumerable<ProjectPropertyElement> PropertiesWithoutConditions(
            this ProjectRootElement projectRoot)
        {
            return ElementsWithoutConditions(projectRoot.Properties);
        }

        public static IEnumerable<ProjectItemElement> ItemsWithoutConditions(
            this ProjectRootElement projectRoot)
        {
            return ElementsWithoutConditions(projectRoot.Items);
        }

        public static IEnumerable<string> Includes(
            this ProjectItemElement item)
        {
            return SplitSemicolonDelimitedValues(item.Include);
        }

        public static IEnumerable<string> Excludes(
            this ProjectItemElement item)
        {
            return SplitSemicolonDelimitedValues(item.Exclude);
        }

        public static IEnumerable<string> Removes(
            this ProjectItemElement item)
        {
            return SplitSemicolonDelimitedValues(item.Remove);
        }

        public static IEnumerable<string> AllConditions(this ProjectElement projectElement)
        {
            return new string[] { projectElement.Condition }.Concat(projectElement.AllParents.Select(p=> p.Condition));
        }

        public static IEnumerable<string> IntersectIncludes(this ProjectItemElement item, ProjectItemElement otherItem)
        {
            return item.Includes().Intersect(otherItem.Includes());
        }

        public static IEnumerable<string> IntersectExcludes(this ProjectItemElement item, ProjectItemElement otherItem)
        {
            return item.Excludes().Intersect(otherItem.Excludes());
        }

        public static void RemoveIncludes(this ProjectItemElement item, IEnumerable<string> includesToRemove)
        {
            item.Include = string.Join(";", item.Includes().Except(includesToRemove));
        }

        public static void UnionIncludes(this ProjectItemElement item, IEnumerable<string> includesToAdd)
        {
            item.Include = string.Join(";", item.Includes().Union(includesToAdd));
        }

        public static void UnionExcludes(this ProjectItemElement item, IEnumerable<string> excludesToAdd)
        {
            item.Exclude = string.Join(";", item.Excludes().Union(excludesToAdd));
        }

        public static bool ValueEquals(this ProjectMetadataElement metadata, ProjectMetadataElement otherMetadata)
        {
            return metadata.Value.Equals(otherMetadata.Value, StringComparison.Ordinal);
        }

        public static void AddMetadata(this ProjectItemElement item, ICollection<ProjectMetadataElement> metadataElements)
        {
            foreach (var metadata in metadataElements)
            {
                item.AddMetadata(metadata);
            }
        }

        public static void RemoveIfEmpty(this ProjectElementContainer container)
        {
            if (!container.Children.Any())
            {
                container.Parent.RemoveChild(container);
            }
        }

        public static ProjectMetadataElement GetMetadataWithName(this ProjectItemElement item, string name)
        {
            return item.Metadata.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public static void AddMetadata(this ProjectItemElement item, ProjectMetadataElement metadata)
        {
            var existingMetadata = item.GetMetadataWithName(metadata.Name);

            if (existingMetadata != default(ProjectMetadataElement) && !existingMetadata.ValueEquals(metadata))
            {
                throw new Exception("Cannot merge metadata with the same name and different values");
            }

            if (existingMetadata == default(ProjectMetadataElement))
            {
#if !DISABLE_TRACE
                MigrationTrace.Instance.WriteLine($"{nameof(AddMetadata)}: Adding metadata to {item.ItemType} item: {{ {metadata.Name}, {metadata.Value}, {metadata.Condition} }}");
#endif
                var metametadata = item.AddMetadata(metadata.Name, metadata.Value);
                metametadata.Condition = metadata.Condition;
            }
        }

        private static IEnumerable<string> SplitSemicolonDelimitedValues(string combinedValue)
        {
            return string.IsNullOrEmpty(combinedValue) ? Enumerable.Empty<string>() : combinedValue.Split(';');
        }

        private static IEnumerable<T> ElementsWithoutConditions<T>(IEnumerable<T> elements) where T : ProjectElement
        {
            return elements
                .Where(e => string.IsNullOrEmpty(e.Condition)
                            && e.AllParents.All(parent => string.IsNullOrEmpty(parent.Condition)));
        }
    }
}
