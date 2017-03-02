// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;
using System.Collections;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text;
using System.Globalization;

using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is used by the regular expression search/replace function to replace item references of the form
    /// @(itemtype->transform, separator) with the correct string.
    /// </summary>
    /// <owner>RGoel, SumedhK</owner>
    internal class ItemExpander
    {
        #region Regular expressions for item vectors

        /**************************************************************************************************************************
         * WARNING: The regular expressions below MUST be kept in sync with the expressions in the ProjectWriter class -- if the
         * description of an item vector changes, the expressions must be updated in both places.
         *************************************************************************************************************************/

        // the leading characters that indicate the start of an item vector
        internal const string itemVectorPrefix = "@(";

        // complete description of an item vector, including the optional transform expression and separator specification
        private const string itemVectorSpecification =
            @"@\(\s*
                (?<TYPE>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @")
                (?<TRANSFORM_SPECIFICATION>\s*->\s*'(?<TRANSFORM>[^']*)')?
                (?<SEPARATOR_SPECIFICATION>\s*,\s*'(?<SEPARATOR>[^']*)')?
            \s*\)";

        // description of an item vector, including the optional transform expression, but not the separator specification
        private const string itemVectorWithoutSeparatorSpecification =
            @"@\(\s*
                (?<TYPE>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @")
                (?<TRANSFORM_SPECIFICATION>\s*->\s*'(?<TRANSFORM>[^']*)')?
            \s*\)";

        // regular expression used to match item vectors, including those embedded in strings
        internal static readonly Regex itemVectorPattern = new Regex(itemVectorSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

        // regular expression used to match a list of item vectors that have no separator specification -- the item vectors
        // themselves may be optionally separated by semi-colons, or they might be all jammed together
        internal static readonly Regex listOfItemVectorsWithoutSeparatorsPattern =
            new Regex(@"^\s*(;\s*)*(" +
                      itemVectorWithoutSeparatorSpecification +
                      @"\s*(;\s*)*)+$",
                      RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

        // the leading characters that indicate the start of an item metadata reference
        internal const string itemMetadataPrefix = "%(";

        // complete description of an item metadata reference, including the optional qualifying item type
        private const string itemMetadataSpecification =
            @"%\(\s*
                (?<ITEM_SPECIFICATION>(?<TYPE>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @")\s*\.\s*)?
                (?<NAME>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @")
            \s*\)";

        // regular expression used to match item metadata references embedded in strings
        internal static readonly Regex itemMetadataPattern = new Regex(itemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

        // description of an item vector with a transform, split into two halves along the transform expression
        private const string itemVectorWithTransformLHS = @"@\(\s*" + ProjectWriter.itemTypeOrMetadataNameSpecification + @"\s*->\s*'[^']*";
        private const string itemVectorWithTransformRHS = @"[^']*'(\s*,\s*'[^']*')?\s*\)";

        // PERF WARNING: this Regex is complex and tends to run slowly
        // regular expression used to match item metadata references outside of item vector transforms
        internal static readonly Regex nonTransformItemMetadataPattern =
            new Regex(@"((?<=" + itemVectorWithTransformLHS + @")" + itemMetadataSpecification + @"(?!" + itemVectorWithTransformRHS + @")) |
                        ((?<!" + itemVectorWithTransformLHS + @")" + itemMetadataSpecification + @"(?=" + itemVectorWithTransformRHS + @")) |
                        ((?<!" + itemVectorWithTransformLHS + @")" + itemMetadataSpecification + @"(?!" + itemVectorWithTransformRHS + @"))",
                        RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

        /**************************************************************************************************************************
         * WARNING: The regular expressions above MUST be kept in sync with the expressions in the ProjectWriter class.
         *************************************************************************************************************************/

        #endregion

        #region Member data

        // When using this class to replace the item vector specification with the actual
        // list of items, we use this table to get the items
        private ReadOnlyLookup readOnlyLookup;

        // used when expanding item metadata during transforms
        private BuildItem itemUnderTransformation;

        // the XML node whose contents are being operated on by this class
        private XmlNode parentNode;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor, which captures the hashtable of items to use when expanding the item reference.
        /// </summary>
        private ItemExpander
        (
            XmlNode parentNode,
            ReadOnlyLookup readOnlyLookup
        )
        {
            this.parentNode = parentNode;
            this.readOnlyLookup = readOnlyLookup;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Expands all item vectors embedded in the given string.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="s"></param>
        /// <param name="parentNode"></param>
        /// <param name="itemsByType"></param>
        /// <returns>Given string, with embedded item vectors expanded.</returns>
        internal static string ExpandEmbeddedItemVectors(string s, XmlNode parentNode, ReadOnlyLookup readOnlyLookup)
        {
            // Before we do the expensive RegEx stuff, at least make sure there's 
            // an @ sign in the expression somewhere.  If not, skip all the hard work.
            if (s.IndexOf('@') != -1)
            {
                ItemExpander itemExpander = new ItemExpander(parentNode, readOnlyLookup);

                return itemVectorPattern.Replace(s, new MatchEvaluator(itemExpander.ExpandItemVector));
            }
            else
            {
                return s;
            }
        }

        /// <summary>
        /// Attempts to extract the items in the given item vector. Item vectors embedded in strings, and item vectors with
        /// separator specifications are considered invalid, because it is not clear if those item vectors are meant to be lists
        /// or strings -- if the latter, the ExpandEmbeddedItemVectors() method should be used instead.
        /// </summary>
        /// <owner>SumedhK;RGoel</owner>
        /// <param name="itemVectorExpression"></param>
        /// <param name="parentNode"></param>
        /// <param name="itemsByType"></param>
        /// <returns>a virtual BuildItemGroup containing the items resulting from the expression, or null if the expression was invalid.</returns>
        internal static BuildItemGroup ItemizeItemVector
        (
            string itemVectorExpression,
            XmlNode parentNode,
            ReadOnlyLookup readOnlyLookup
        )
        {
            Match throwAwayMatch;
            return ItemExpander.ItemizeItemVector(itemVectorExpression, parentNode, readOnlyLookup, out throwAwayMatch);
        }

        /// <summary>
        /// Attempts to extract the items in the given item vector expression. Item vectors embedded in strings, 
        /// and item vectors with separator specifications are considered invalid, because it is not clear 
        /// if those item vectors are meant to be lists or strings -- if the latter, the ExpandEmbeddedItemVectors() 
        /// method should be used instead.
        /// </summary>
        /// <param name="itemVectorExpression"></param>
        /// <param name="parentNode"></param>
        /// <param name="readOnlyLookup"></param>
        /// <param name="itemVectorMatch"></param>
        /// <returns>a virtual BuildItemGroup containing the items resulting from the expression, or null if the expression was invalid.</returns>
        /// <owner>SumedhK;RGoel</owner>
        internal static BuildItemGroup ItemizeItemVector
        (
            string itemVectorExpression,
            XmlNode parentNode,
            ReadOnlyLookup readOnlyLookup,
            out Match itemVectorMatch
        )
        {
            itemVectorMatch = null;
            BuildItemGroup items = null;

            itemVectorMatch = GetItemVectorMatches(itemVectorExpression);

            if (itemVectorMatch != null && itemVectorMatch.Success)
            {
                // The method above reports a match if there are any
                // valid @(itemlist) references in the given expression.
                // If the passed-in expression contains exactly one item list reference,
                // with nothing else concatenated to the beginning or end, then proceed
                // with itemizing it, otherwise error.
                ProjectErrorUtilities.VerifyThrowInvalidProject(itemVectorMatch.Value == itemVectorExpression,
                    parentNode, "EmbeddedItemVectorCannotBeItemized", itemVectorExpression);

                ItemExpander itemExpander = new ItemExpander(parentNode, readOnlyLookup);

                // If the reference contains a separator, we need to flatten the list into a scalar and then create
                // an item group with a single item. This is necessary for VSWhidbey 525917 - basically we need this
                // to be able to convert item lists with user specified separators into properties.
                if (itemVectorMatch.Groups["SEPARATOR_SPECIFICATION"].Length > 0)
                {
                    string expandedItemVector = itemExpander.ExpandItemVector(itemVectorMatch);

                    string itemType = itemVectorMatch.Groups["TYPE"].Value;
                    items = new BuildItemGroup();

                    if (expandedItemVector.Length > 0)
                    {
                        items.AddNewItem(itemType, expandedItemVector);
                    }
                }
                else
                {
                    items = itemExpander.ItemizeItemVector(itemVectorMatch);
                }

                ErrorUtilities.VerifyThrow(items != null, "ItemizeItemVector shouldn't give us null.");
            }
            
            return items;
        }

        /// <summary>
        /// Returns true if the expression contains an item vector pattern, else returns false.
        /// </summary>
        internal static bool ExpressionContainsItemVector(string expression)
        {
            Match itemVectorMatch = GetItemVectorMatches(expression);

            if (itemVectorMatch != null && itemVectorMatch.Success)
            {
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Returns matches to an item expression pattern in the expression.
        /// </summary>
        private static Match GetItemVectorMatches(string expression)
        {
            Match itemVectorMatch = null;

            // Before we do the expensive RegEx stuff, at least make sure there's 
            // an @ sign in the expression somewhere.  If not, skip all the hard work.
            if (expression.IndexOf('@') != -1)
            {
                itemVectorMatch = itemVectorPattern.Match(expression);
            }

            return itemVectorMatch; 
        }

        /// <summary>
        /// Extracts the items in the given item vector.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="itemVector"></param>
        /// <returns>The contents of the item vector (with transforms applied).</returns>
        private BuildItemGroup ItemizeItemVector(Match itemVector)
        {
            ErrorUtilities.VerifyThrow(itemVector.Success, "Need a valid item vector.");

            string itemType = itemVector.Groups["TYPE"].Value;
            string transform = (itemVector.Groups["TRANSFORM_SPECIFICATION"].Length > 0)
                ? itemVector.Groups["TRANSFORM"].Value
                : null;

            BuildItemGroup items = null;
            if (readOnlyLookup != null)
            {
                items = readOnlyLookup.GetItems(itemType);
            }

            if (items == null)
            {
                items = new BuildItemGroup();
            }
            else
            {
                items = items.Clone((transform != null) /* deep clone on transforms because we're actually creating new items */);
            }

            if (transform != null)
            {
                foreach (BuildItem item in items)
                {
                    itemUnderTransformation = item;
                    item.SetFinalItemSpecEscaped(itemMetadataPattern.Replace(transform, new MatchEvaluator(ExpandItemMetadata)));
                }
            }

            return items;
        }

        /// <summary>
        /// Expands a single item vector.
        /// 
        /// Item vectors are composed of a name, a transform, and a separator i.e.
        /// 
        ///     @(&lt;name&gt;->'&lt;transform&gt;','&lt;separator&gt;')
        /// 
        /// If a separator is not specified it defaults to a semi-colon. The transform expression is also optional, but if
        /// specified, it allows each item in the vector to have its item-spec converted to a different form. The transform
        /// expression can reference any custom metadata defined on the item, as well as the pre-defined item-spec modifiers.
        /// 
        /// NOTE:
        /// 1) white space between &lt;name&gt;, &lt;transform&gt; and &lt;separator&gt; is ignored
        ///    i.e. @(&lt;name&gt;, '&lt;separator&gt;') is valid
        /// 2) the separator is not restricted to be a single character, it can be a string
        /// 3) the separator can be an empty string i.e. @(&lt;name&gt;,'')
        /// 4) specifying an empty transform is NOT the same as specifying no transform -- the former will reduce all item-specs
        ///    to empty strings
        /// </summary>
        /// <remarks>This is the MatchEvaluator delegate passed to Regex.Replace().</remarks>
        /// <example>
        /// if @(files) is a vector for the files a.txt and b.txt, then:
        /// 
        ///     "my list: @(files)"                                 expands to      "my list: a.txt;b.txt"
        /// 
        ///     "my list: @(files,' ')"                             expands to      "my list: a.txt b.txt"
        /// 
        ///     "my list: @(files, '')"                             expands to      "my list: a.txtb.txt"
        /// 
        ///     "my list: @(files, '; ')"                           expands to      "my list: a.txt; b.txt"
        /// 
        ///     "my list: @(files->'%(Filename)')"                  expands to      "my list: a;b"
        /// 
        ///     "my list: @(files -> 'temp\%(Filename).xml', ' ')   expands to      "my list: temp\a.xml temp\b.xml"
        /// 
        ///     "my list: @(files->'')                              expands to      "my list: ;"
        /// </example>
        /// <owner>SumedhK</owner>
        /// <param name="itemVector"></param>
        /// <param name="isUnknownItemType">(out) true if the referenced item does not exist</param>
        /// <returns>expanded item vector</returns>
        private string ExpandItemVector(Match itemVector)
        {
            ErrorUtilities.VerifyThrow(itemVector.Success, "Need a valid item vector.");

            string separator = (itemVector.Groups["SEPARATOR_SPECIFICATION"].Length != 0) 
                ? itemVector.Groups["SEPARATOR"].Value
                : ";";

            BuildItemGroup items = ItemizeItemVector(itemVector);

            if (items.Count > 0)
            {
                StringBuilder expandedItemVector = new StringBuilder();

                for (int i = 0; i < items.Count; i++)
                {
                    expandedItemVector.Append(items[i].FinalItemSpecEscaped);

                    if (i < (items.Count - 1))
                    {
                        expandedItemVector.Append(separator);
                    }
                }

                return expandedItemVector.ToString();
            }
            else
            {
                return String.Empty;
            }
        }

        /// <summary>
        /// Retrieves the value of the given metadata for the item currently being transformed.
        /// </summary>
        /// <remarks>This method is a MatchEvaluator delegate passed to Regex.Replace().</remarks>
        /// <owner>SumedhK</owner>
        /// <param name="itemMetadataMatch"></param>
        /// <returns>item metadata value</returns>
        private string ExpandItemMetadata(Match itemMetadataMatch)
        {
            ErrorUtilities.VerifyThrow(itemUnderTransformation != null, "Need item to get metadata value from.");

            string itemMetadataName = itemMetadataMatch.Groups["NAME"].Value;

            ProjectErrorUtilities.VerifyThrowInvalidProject(itemMetadataMatch.Groups["ITEM_SPECIFICATION"].Length == 0,
                parentNode, "QualifiedMetadataInTransformNotAllowed", itemMetadataMatch.Value, itemMetadataName);

            string itemMetadataValue = null;

            try
            {
                itemMetadataValue = itemUnderTransformation.GetEvaluatedMetadataEscaped(itemMetadataName);
            }
            catch (InvalidOperationException e)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(false, parentNode,
                    "CannotEvaluateItemMetadata", itemMetadataName, e.Message);
            }

            return itemMetadataValue;
        }

        #endregion
    }
}
