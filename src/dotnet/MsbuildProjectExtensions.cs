// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration;
using Microsoft.DotNet.Tools.Common;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Tools
{
    internal static class MsbuildProjectExtensions
    {
        public static bool IsConditionalOnFramework(this ProjectElement el, string framework)
        {
            string conditionStr;
            if (!TryGetFrameworkConditionString(framework, out conditionStr))
            {
                return el.ConditionChain().Count == 0;
            }

            var condChain = el.ConditionChain();
            return condChain.Count == 1 && condChain.First().Trim() == conditionStr;
        }

        public static ProjectItemGroupElement LastItemGroup(this ProjectRootElement root)
        {
            return root.ItemGroupsReversed.FirstOrDefault();
        }

        public static ProjectItemGroupElement FindUniformOrCreateItemGroupWithCondition(this ProjectRootElement root, string projectItemElementType, string framework)
        {
            var lastMatchingItemGroup = FindExistingUniformItemGroupWithCondition(root, projectItemElementType, framework);

            if (lastMatchingItemGroup != null)
            {
                return lastMatchingItemGroup;
            }

            ProjectItemGroupElement ret = root.CreateItemGroupElement();
            string condStr;
            if (TryGetFrameworkConditionString(framework, out condStr))
            {
                ret.Condition = condStr;
            }

            root.InsertAfterChild(ret, root.LastItemGroup());
            return ret;
        }

        public static ProjectItemGroupElement FindExistingUniformItemGroupWithCondition(this ProjectRootElement root, string projectItemElementType, string framework)
        {
            return root.ItemGroupsReversed.FirstOrDefault((itemGroup) => itemGroup.IsConditionalOnFramework(framework) && itemGroup.IsUniformItemElementType(projectItemElementType));
        }

        public static bool IsUniformItemElementType(this ProjectItemGroupElement group, string projectItemElementType)
        {
            return group.Items.All((it) => it.ItemType == projectItemElementType);
        }

        public static IEnumerable<ProjectItemElement> FindExistingItemsWithCondition(this ProjectRootElement root, string framework, string include)
        {
            return root.Items.Where((el) => el.IsConditionalOnFramework(framework) && el.HasInclude(include));
        }

        public static bool HasExistingItemWithCondition(this ProjectRootElement root, string framework, string include)
        {
            return root.FindExistingItemsWithCondition(framework, include).Count() != 0;
        }

        public static IEnumerable<ProjectItemElement> GetAllItemsWithElementType(this ProjectRootElement root, string projectItemElementType)
        {
            return root.Items.Where((it) => it.ItemType == projectItemElementType);
        }

        public static bool HasInclude(this ProjectItemElement el, string include)
        {
            include = NormalizeIncludeForComparison(include);
            foreach (var i in el.Includes())
            {
                if (include == NormalizeIncludeForComparison(i))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetFrameworkConditionString(string framework, out string condition)
        {
            if (string.IsNullOrEmpty(framework))
            {
                condition = null;
                return false;
            }

            condition = $"'$(TargetFramework)' == '{framework}'";
            return true;
        }

        private static string NormalizeIncludeForComparison(string include)
        {
            return PathUtility.GetPathWithBackSlashes(include.ToLower());
        }
    }
}
