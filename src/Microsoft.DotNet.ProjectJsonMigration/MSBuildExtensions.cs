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

namespace Microsoft.DotNet.ProjectJsonMigration
{
    public static class MSBuildExtensions
    {
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

        public static IEnumerable<string> AllConditions(this ProjectElement projectElement)
        {
            return new string[] { projectElement.Condition }.Concat(projectElement.AllParents.Select(p=> p.Condition));
        }

        public static IEnumerable<string> IntersectIncludes(this ProjectItemElement item, ProjectItemElement otherItem)
        {
            return item.Includes().Intersect(otherItem.Includes());
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

        public static ProjectMetadataElement GetMetadataWithName(this ProjectItemElement item, string name)
        {
            return item.Metadata.FirstOrDefault(m => m.Name.Equals(name, StringComparison.Ordinal));
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

        public static void AddMetadata(this ProjectItemElement item, ProjectMetadataElement metadata)
        {
            var existingMetadata = item.GetMetadataWithName(metadata.Name);

            if (existingMetadata != default(ProjectMetadataElement) && !existingMetadata.ValueEquals(metadata))
            {
                throw new Exception("Cannot merge metadata with the same name and different values");
            }

            if (existingMetadata == default(ProjectMetadataElement))
            {
                MigrationTrace.Instance.WriteLine($"{nameof(AddMetadata)}: Adding metadata to {item.ItemType} item: {{ {metadata.Name}, {metadata.Value} }}");
                item.AddMetadata(metadata.Name, metadata.Value);
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
