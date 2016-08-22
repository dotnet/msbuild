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
            return projectRoot.Properties
                .Where(p => p.Condition == string.Empty
                    && p.AllParents.Count(parent => parent.Condition != string.Empty) == 0);
        }

        public static IEnumerable<ProjectItemElement> ItemsWithoutConditions(
            this ProjectRootElement projectRoot)
        {
            return projectRoot.Items
                .Where(p => string.IsNullOrEmpty(p.Condition)
                            && p.AllParents.All(parent => string.IsNullOrEmpty(parent.Condition)));
        }

        public static IEnumerable<string> Includes(
            this ProjectItemElement item)
        {
            return item.Include.Equals(string.Empty) ? Enumerable.Empty<string>() : item.Include.Split(';');
        }

        public static IEnumerable<string> Excludes(
            this ProjectItemElement item)
        {
            return item.Exclude.Equals(string.Empty) ? Enumerable.Empty<string>() :  item.Exclude.Split(';');
        }

        public static IEnumerable<string> AllConditions(this ProjectElement projectElement)
        {
            return new string[] { projectElement.Condition }.Concat(projectElement.AllParents.Select(p=> p.Condition));
        }

        public static IEnumerable<string> CommonIncludes(this ProjectItemElement item, ProjectItemElement otherItem)
        {
            return item.Includes().Intersect(otherItem.Includes());
        }

        public static void RemoveIncludes(this ProjectItemElement item, IEnumerable<string> includesToRemove)
        {
            item.Include = string.Join(";", item.Includes().Except(includesToRemove));
        }

        public static void AddIncludes(this ProjectItemElement item, IEnumerable<string> includes)
        {
            item.Include = string.Join(";", item.Includes().Union(includes));
        }

        public static void AddExcludes(this ProjectItemElement item, IEnumerable<string> excludes)
        {
            item.Exclude = string.Join(";", item.Excludes().Union(excludes));
        }

        public static ProjectMetadataElement GetMetadataWithName(this ProjectItemElement item, string name)
        {
            return item.Metadata.FirstOrDefault(m => m.Name.Equals(name, StringComparison.Ordinal));
        }

        public static bool ValueEquals(this ProjectMetadataElement metadata, ProjectMetadataElement otherMetadata)
        {
            return metadata.Value.Equals(otherMetadata.Value, StringComparison.Ordinal);
        }

        public static void AddMetadata(this ProjectItemElement item, ICollection<ProjectMetadataElement> metadatas)
        {
            foreach (var metadata in metadatas)
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

            if (existingMetadata == null)
            {
                Console.WriteLine(metadata.Name);
                item.AddMetadata(metadata.Name, metadata.Value);
            }
        }

    }
}
