// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Build.Shared;
using Microsoft.NET.StringTools;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    /// Regular expressions used by the expander.
    /// The expander currently uses regular expressions rather than a parser to do its work.
    /// </summary>
    private static partial class RegularExpressions
    {
        /**************************************************************************************************************************
        * WARNING: The regular expressions below MUST be kept in sync with the expressions in the ProjectWriter class -- if the
        * description of an item vector changes, the expressions must be updated in both places.
        *************************************************************************************************************************/

#if NET
        [GeneratedRegex(ItemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture)]
        internal static partial Regex ItemMetadataRegex { get; }
#else
        /// <summary>
        /// Regular expression used to match item metadata references embedded in strings.
        /// For example, %(Compile.DependsOn) or %(DependsOn).
        /// </summary>
        internal static Regex ItemMetadataRegex => s_itemMetadataRegex ??=
            new Regex(ItemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        internal static Regex s_itemMetadataRegex;
#endif

        /// <summary>
        /// Name of the group matching the "name" of a metadatum.
        /// </summary>
        internal const string NameGroup = "NAME";

        /// <summary>
        /// Name of the group matching the prefix on a metadata expression, for example "Compile." in "%(Compile.Object)".
        /// </summary>
        internal const string ItemSpecificationGroup = "ITEM_SPECIFICATION";

        /// <summary>
        /// Name of the group matching the item type in an item expression or metadata expression.
        /// </summary>
        internal const string ItemTypeGroup = "ITEM_TYPE";

        internal const string NonTransformItemMetadataSpecification = @"((?<=" + ItemVectorWithTransformLHS + @")" + ItemMetadataSpecification + @"(?!" +
                                                            ItemVectorWithTransformRHS + @")) | ((?<!" + ItemVectorWithTransformLHS + @")" +
                                                            ItemMetadataSpecification + @"(?=" + ItemVectorWithTransformRHS + @")) | ((?<!" +
                                                            ItemVectorWithTransformLHS + @")" + ItemMetadataSpecification + @"(?!" +
                                                            ItemVectorWithTransformRHS + @"))";

#if NET
        [GeneratedRegex(NonTransformItemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture)]
        internal static partial Regex NonTransformItemMetadataRegex { get; }
#else
        /// <summary>
        /// regular expression used to match item metadata references outside of item vector transforms.
        /// </summary>
        /// <remarks>PERF WARNING: this Regex is complex and tends to run slowly.</remarks>
        private static Regex s_nonTransformItemMetadataPattern;

        internal static Regex NonTransformItemMetadataRegex => s_nonTransformItemMetadataPattern ??=
            new Regex(NonTransformItemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture | RegexOptions.Compiled);
#endif

        /// <summary>
        /// Complete description of an item metadata reference, including the optional qualifying item type.
        /// For example, %(Compile.DependsOn) or %(DependsOn).
        /// </summary>
        private const string ItemMetadataSpecification = @"%\(\s* (?<ITEM_SPECIFICATION>(?<ITEM_TYPE>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @")\s*\.\s*)? (?<NAME>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @") \s*\)";

        /// <summary>
        /// description of an item vector with a transform, left hand side.
        /// </summary>
        private const string ItemVectorWithTransformLHS = @"@\(\s*" + ProjectWriter.itemTypeOrMetadataNameSpecification + @"\s*->\s*'[^']*";

        /// <summary>
        /// description of an item vector with a transform, right hand side.
        /// </summary>
        private const string ItemVectorWithTransformRHS = @"[^']*'(\s*,\s*'[^']*')?\s*\)";

        /**************************************************************************************************************************
         * WARNING: The regular expressions above MUST be kept in sync with the expressions in the ProjectWriter class.
         *************************************************************************************************************************/

        /// <summary>
        /// Copied from <see cref="Regex.Replace(string, MatchEvaluator, int, int)"/> and modified to use a <see cref="SpanBasedStringBuilder"/> rather than repeatedly allocating a <see cref="System.Text.StringBuilder"/>. This
        /// allows us to avoid intermediate string allocations when repeatedly doing replacements. 
        /// </summary>
        /// <param name="input">The string to operate on.</param>
        /// <param name="evaluator">A function to transform any matches found.</param>
        /// <param name="metadataMatchEvaluator">State used in the transform function.</param>
        /// <param name="stringBuilder">The <see cref="SpanBasedStringBuilder"/> that will accumulate the results.</param>
        /// <param name="regex">The <see cref="Regex"/> that will perform the matching.</param>
        public static void ReplaceAndAppend(string input, Func<Match, MetadataMatchEvaluator, string> evaluator, MetadataMatchEvaluator metadataMatchEvaluator, SpanBasedStringBuilder stringBuilder, Regex regex)
        {
            ReplaceAndAppend(input, evaluator, metadataMatchEvaluator, -1, regex.RightToLeft ? input.Length : 0, stringBuilder, regex);
        }

        /// <summary>
        /// Copied from <see cref="Regex.Replace(string, MatchEvaluator, int, int)"/> and modified to use a <see cref="SpanBasedStringBuilder"/> rather than repeatedly allocating a <see cref="System.Text.StringBuilder"/>. This
        /// allows us to avoid intermediate string allocations when repeatedly doing replacements.
        /// </summary>
        /// <param name="input">The string to operate on.</param>
        /// <param name="evaluator">A function to transform any matches found.</param>
        /// <param name="matchEvaluatorState">State used in the transform function.</param>
        /// <param name="count">The number of replacements.</param>
        /// <param name="startat">Index to start when doing replacements.</param>
        /// <param name="stringBuilder">The <see cref="SpanBasedStringBuilder"/> that will accumulate the results.</param>
        /// <param name="regex">The <see cref="Regex"/> that will perform the matching.</param>
        public static void ReplaceAndAppend(string input, Func<Match, MetadataMatchEvaluator, string> evaluator, MetadataMatchEvaluator matchEvaluatorState, int count, int startat, SpanBasedStringBuilder stringBuilder, Regex regex)
        {
            if (evaluator is null)
            {
                throw new ArgumentNullException(nameof(evaluator));
            }

            if (stringBuilder is null)
            {
                throw new ArgumentNullException(nameof(stringBuilder));
            }

            if (count < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (startat < 0 || startat > input.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(startat));
            }

            if (regex is null)
            {
                throw new ArgumentNullException(nameof(regex));
            }

            if (count == 0)
            {
                stringBuilder.Append(input);

                return;
            }

            Match match = regex.Match(input, startat);
            if (!match.Success)
            {
                stringBuilder.Append(input);

                return;
            }

            if (!regex.RightToLeft)
            {
                int prevat = 0;
                do
                {
                    if (match.Index != prevat)
                    {
                        stringBuilder.Append(input, prevat, match.Index - prevat);
                    }

                    prevat = match.Index + match.Length;
                    stringBuilder.Append(evaluator(match, matchEvaluatorState));
                    if (--count == 0)
                    {
                        break;
                    }

                    match = match.NextMatch();
                }
                while (match.Success);
                if (prevat < input.Length)
                {
                    stringBuilder.Append(input, prevat, input.Length - prevat);
                }
            }
            else
            {
                List<ReadOnlyMemory<char>> list = new List<ReadOnlyMemory<char>>();
                int prevat = input.Length;
                do
                {
                    if (match.Index + match.Length != prevat)
                    {
                        list.Add(input.AsMemory().Slice(match.Index + match.Length, prevat - match.Index - match.Length));
                    }

                    prevat = match.Index;
                    list.Add(evaluator(match, matchEvaluatorState).AsMemory());
                    if (--count == 0)
                    {
                        break;
                    }

                    match = match.NextMatch();
                }
                while (match.Success);

                if (prevat > 0)
                {
                    stringBuilder.Append(input, 0, prevat);
                }

                for (int i = list.Count - 1; i >= 0; i--)
                {
                    stringBuilder.Append(list[i]);
                }
            }
        }
    }
}
