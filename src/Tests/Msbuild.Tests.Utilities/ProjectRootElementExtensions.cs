// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.DotNet.Cli.Utils;

namespace Msbuild.Tests.Utilities
{
    public static class ProjectRootElementExtensions
    {
        public static int NumberOfItemGroupsWithConditionContaining(
            this ProjectRootElement root,
            string patternInCondition)
        {
            return root.ItemGroups.Count((itemGroup) => itemGroup.Condition.Contains(patternInCondition));
        }

        public static int NumberOfItemGroupsWithoutCondition(this ProjectRootElement root)
        {
            return root.ItemGroups.Count((ig) => string.IsNullOrEmpty(ig.Condition));
        }

        public static IEnumerable<ProjectElement> ItemsWithIncludeAndConditionContaining(
            this ProjectRootElement root,
            string itemType,
            string includePattern,
            string patternInCondition)
        {
            return root.Items.Where((it) =>
            {
                if (it.ItemType != itemType || !it.Include.Contains(includePattern))
                {
                    return false;
                }

                var condChain = it.ConditionChain();
                return condChain.Count == 1 && condChain.First().Contains(patternInCondition);
            });
        }

        public static int NumberOfProjectReferencesWithIncludeAndConditionContaining(
            this ProjectRootElement root,
            string includePattern,
            string patternInCondition)
        {
            return root.ItemsWithIncludeAndConditionContaining(
                "ProjectReference",
                includePattern,
                patternInCondition)
                .Count();
        }

        public static IEnumerable<ProjectElement> ItemsWithIncludeContaining(
            this ProjectRootElement root,
            string itemType,
            string includePattern)
        {
            return root.Items.Where((it) => it.ItemType == itemType && it.Include.Contains(includePattern)
                    && it.ConditionChain().Count() == 0);
        }

        public static int NumberOfProjectReferencesWithIncludeContaining(
            this ProjectRootElement root,
            string includePattern)
        {
            return root.ItemsWithIncludeContaining("ProjectReference", includePattern).Count();
        }
    }
}
