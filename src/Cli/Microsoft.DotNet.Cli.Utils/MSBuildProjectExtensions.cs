// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Construction;
using Microsoft.DotNet.Tools.Common;

namespace Microsoft.DotNet.Cli.Utils
{
    internal static class MSBuildProjectExtensions
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

        public static IEnumerable<string> Includes(
            this ProjectItemElement item)
        {
            return SplitSemicolonDelimitedValues(item.Include);
        }

        private static IEnumerable<string> SplitSemicolonDelimitedValues(string combinedValue)
        {
            return string.IsNullOrEmpty(combinedValue) ? Enumerable.Empty<string>() : combinedValue.Split(';');
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
