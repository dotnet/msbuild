// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using System;
using System.Linq;
using Microsoft.DotNet.ProjectJsonMigration;
using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.Add.P2P.Tests
{
    internal static class Extensions
    {
        //public static int CountOccurrances(this string s, string pattern)
        //{
        //    int ret = 0;
        //    for (int i = s.IndexOf(pattern); i != -1; i = s.IndexOf(pattern, i + 1))
        //    {
        //        ret++;
        //    }

        //    return ret;
        //}

        //public static int NumberOfLinesWith(this string s, params string[] patterns)
        //{
        //    int ret = 0;
        //    string[] lines = s.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        //    foreach (var line in lines)
        //    {
        //        bool shouldCount = true;

        //        foreach (var p in patterns)
        //        {
        //            if (!line.Contains(p))
        //            {
        //                shouldCount = false;
        //                break;
        //            }
        //        }

        //        if (shouldCount)
        //        {
        //            ret++;
        //        }
        //    }

        //    return ret;
        //}

        public static int NumberOfItemGroupsWithConditionContaining(this ProjectRootElement root, string patternInCondition)
        {
            return root.ItemGroups.Count((ig) => ig.Condition.Contains(patternInCondition));
        }

        public static int NumberOfItemGroupsWithoutCondition(this ProjectRootElement root)
        {
            return root.ItemGroups.Count((ig) => string.IsNullOrEmpty(ig.Condition));
        }

        public static IEnumerable<ProjectElement> ItemsWithIncludeAndConditionContaining(this ProjectRootElement root, string itemType, string includePattern, string patternInCondition)
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

        public static int NumberOfProjectReferencesWithIncludeAndConditionContaining(this ProjectRootElement root, string includePattern, string patternInCondition)
        {
            return root.ItemsWithIncludeAndConditionContaining("ProjectReference", includePattern, patternInCondition).Count();
        }

        public static IEnumerable<ProjectElement> ItemsWithIncludeContaining(this ProjectRootElement root, string itemType, string includePattern)
        {
            return root.Items.Where((it) => it.ItemType == itemType && it.Include.Contains(includePattern));
        }

        public static int NumberOfProjectReferencesWithIncludeContaining(this ProjectRootElement root, string includePattern)
        {
            return root.ItemsWithIncludeContaining("ProjectReference", includePattern).Count();
        }
    }
}