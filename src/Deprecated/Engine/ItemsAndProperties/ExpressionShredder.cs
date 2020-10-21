// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using Microsoft.Build.BuildEngine.Shared;
using System.Collections.Generic;

namespace Microsoft.Build.BuildEngine
{
    internal static class ExpressionShredder
    {
        /// <summary>
        /// Splits an expression into fragments at semi-colons, except where the
        /// semi-colons are in a macro or separator expression.
        /// Fragments are trimmed and empty fragments discarded.
        /// </summary>
        /// <remarks>
        /// These complex cases prevent us from doing a simple split on ';':
        ///  (1) Macro expression: @(foo->'xxx;xxx')
        ///  (2) Separator expression: @(foo, 'xxx;xxx')
        ///  (3) Combination: @(foo->'xxx;xxx', 'xxx;xxx')
        ///  We must not split on semicolons in macro or separator expressions like these.
        /// </remarks>
        /// <param name="expression">List expression to split</param>
        /// <owner>danmose</owner>
        /// <returns>Array of non-empty strings from split list.</returns>
        internal static List<string> SplitSemiColonSeparatedList(string expression)
        {
            List<string> splitList = new List<string>();
            int segmentStart = 0;
            bool insideItemList = false;
            bool insideQuotedPart = false;
            string segment;

            // Walk along the string, keeping track of whether we are in an item list expression.
            // If we hit a semi-colon or the end of the string and we aren't in an item list, 
            // add the segment to the list.
            for (int current = 0; current < expression.Length; current++)
            {
                switch (expression[current])
                {
                    case ';':
                        if (!insideItemList)
                        {
                            // End of segment, so add it to the list
                            segment = expression.Substring(segmentStart, current - segmentStart).Trim();
                            if (segment.Length > 0)
                            {
                                splitList.Add(segment);
                            }
                            // Move past this semicolon
                            segmentStart = current + 1;
                        }
                        break;
                    case '@':
                        // An '@' immediately followed by a '(' is the start of an item list
                        if (expression.Length > current + 1 && expression[current + 1] == '(')
                        {
                            // Start of item expression
                            insideItemList = true;
                        }
                        break;
                    case ')':
                        if (insideItemList && !insideQuotedPart)
                        {
                            // End of item expression
                            insideItemList = false;
                        }
                        break;
                    case '\'':
                        if (insideItemList)
                        {
                            // Start or end of quoted expression in item expression
                            insideQuotedPart = !insideQuotedPart;
                        }
                        break;
                }
            }

            // Reached the end of the string: what's left is another segment
            segment = expression.Substring(segmentStart, expression.Length - segmentStart).Trim();
            if (segment.Length > 0)
            {
                splitList.Add(segment);
            }

            return splitList;
        }
        
        /// <summary>
        /// Given a list of expressions that may contain item list expressions,
        /// returns a pair of tables of all item names found, as K=Name, V=String.Empty;
        /// and all metadata not in transforms, as K=Metadata key, V=MetadataReference,
        /// where metadata key is like "itemname.metadataname" or "metadataname".
        /// PERF: Tables are null if there are no entries, because this is quite a common case.
        /// </summary>
        internal static ItemsAndMetadataPair GetReferencedItemNamesAndMetadata(List<string> expressions)
        {
            ItemsAndMetadataPair pair = new ItemsAndMetadataPair(null, null);

            foreach (string expression in expressions)
            {
                GetReferencedItemNamesAndMetadata(expression, 0, expression.Length, ref pair, ShredderOptions.All);
            }

            return pair;
        }

        /// <summary>
        /// Returns true if there is a metadata expression (outside of a transform) in the expression.
        /// </summary>
        internal static bool ContainsMetadataExpressionOutsideTransform(string expression)
        {
            ItemsAndMetadataPair pair = new ItemsAndMetadataPair(null, null);

            GetReferencedItemNamesAndMetadata(expression, 0, expression.Length, ref pair, ShredderOptions.MetadataOutsideTransforms);

            bool result = (pair.Metadata?.Count > 0);

            return result;
        }

        /// <summary>
        /// Given a subexpression, finds referenced item names and inserts them into the table
        /// as K=Name, V=String.Empty.
        /// </summary>
        /// <remarks>
        /// We can ignore any semicolons in the expression, since we're not itemizing it.
        /// </remarks>
        private static void GetReferencedItemNamesAndMetadata(string expression, int start, int end, ref ItemsAndMetadataPair pair, ShredderOptions whatToShredFor)
        {
            for (int i = start; i < end; i++)
            {
                int restartPoint;

                if (Sink(expression, ref i, end, '@', '('))
                {
                    // Start of a possible item list expression

                    // Store the index to backtrack to if this doesn't turn out to be a well
                    // formed metadata expression. (Subtract one for the increment when we loop around.)
                    restartPoint = i - 1;

                    SinkWhitespace(expression, ref i);

                    int startOfName = i;

                    if (!SinkValidName(expression, ref i, end))
                    {
                        i = restartPoint;
                        continue;
                    }

                    // '-' is a legitimate char in an item name, but we should match '->' as an arrow
                    // in '@(foo->'x')' rather than as the last char of the item name.
                    // The old regex accomplished this by being "greedy"
                    if (end > i && expression[i - 1] == '-' && expression[i] == '>')
                    {
                        i--;
                    }

                    // Grab the name, but continue to verify it's a well-formed expression
                    // before we store it.
                    string name = expression.Substring(startOfName, i - startOfName);

                    SinkWhitespace(expression, ref i);

                    // If there's an '->' eat it and the subsequent quoted expression
                    if (Sink(expression, ref i, end, '-', '>'))
                    {
                        SinkWhitespace(expression, ref i);

                        if (!SinkSingleQuotedExpression(expression, ref i, end))
                        {
                            i = restartPoint;
                            continue;
                        }
                    }

                    SinkWhitespace(expression, ref i);

                    // If there's a ',', eat it and the subsequent quoted expression
                    if (Sink(expression, ref i, ','))
                    {
                        SinkWhitespace(expression, ref i);

                        if (!Sink(expression, ref i, '\''))
                        {
                            i = restartPoint;
                            continue;
                        }

                        int closingQuote = expression.IndexOf('\'', i);
                        if (closingQuote == -1)
                        {
                            i = restartPoint;
                            continue;
                        }

                        // Look for metadata in the separator expression
                        // e.g., @(foo, '%(bar)') contains batchable metadata 'bar'
                        GetReferencedItemNamesAndMetadata(expression, i, closingQuote, ref pair, ShredderOptions.MetadataOutsideTransforms);

                        i = closingQuote + 1;
                    }

                    SinkWhitespace(expression, ref i);

                    if (!Sink(expression, ref i, ')'))
                    {
                        i = restartPoint;
                        continue;
                    }

                    // If we've got this far, we know the item expression was
                    // well formed, so make sure the name's in the table
                    if ((whatToShredFor & ShredderOptions.ItemTypes) != 0)
                    {
                        pair.Items = Utilities.CreateTableIfNecessary(pair.Items);
                        pair.Items[name] = String.Empty;
                    }

                    i--;

                    continue;
                }

                if (Sink(expression, ref i, end, '%', '('))
                {
                    // Start of a possible metadata expression

                    // Store the index to backtrack to if this doesn't turn out to be a well
                    // formed metadata expression. (Subtract one for the increment when we loop around.)
                    restartPoint = i - 1;

                    SinkWhitespace(expression, ref i);

                    int startOfText = i;

                    if (!SinkValidName(expression, ref i, end))
                    {
                        i = restartPoint;
                        continue;
                    }

                    // Grab this, but we don't know if it's an item or metadata name yet
                    string firstPart = expression.Substring(startOfText, i - startOfText);
                    string itemName = null;
                    string metadataName;
                    string qualifiedMetadataName;

                    SinkWhitespace(expression, ref i);

                    bool qualified = Sink(expression, ref i, '.');

                    if (qualified)
                    {
                        SinkWhitespace(expression, ref i);

                        startOfText = i;

                        if (!SinkValidName(expression, ref i, end))
                        {
                            i = restartPoint;
                            continue;
                        }

                        itemName = firstPart;
                        metadataName = expression.Substring(startOfText, i - startOfText);
                        qualifiedMetadataName = itemName + "." + metadataName;
                    }
                    else
                    {
                        metadataName = firstPart;
                        qualifiedMetadataName = metadataName;
                    }

                    SinkWhitespace(expression, ref i);

                    if (!Sink(expression, ref i, ')'))
                    {
                        i = restartPoint;
                        continue;
                    }

                    if ((whatToShredFor & ShredderOptions.MetadataOutsideTransforms) != 0)
                    {
                        pair.Metadata = Utilities.CreateTableIfNecessary(pair.Metadata);
                        pair.Metadata[qualifiedMetadataName] = new MetadataReference(itemName, metadataName);
                    }

                    i--;
                }
            }
        }

        /// <summary>
        /// Returns true if a single quoted subexpression begins at the specified index
        /// and ends before the specified end index.
        /// Leaves index one past the end of the second quote.
        /// </summary>
        private static bool SinkSingleQuotedExpression(string expression, ref int i, int end)
        {
            if (!Sink(expression, ref i, '\''))
            {
                return false;
            }

            while (i < end && expression[i] != '\'')
            {
                i++;
            }

            i++;

            if (end <= i)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns true if a valid name begins at the specified index.
        /// Leaves index one past the end of the name.
        /// </summary>
        private static bool SinkValidName(string expression, ref int i, int end)
        {
            if (end <= i || !XmlUtilities.IsValidInitialElementNameCharacter(expression[i]))
            {
                return false;
            }

            i++;

            while (end > i && XmlUtilities.IsValidSubsequentElementNameCharacter(expression[i]))
            {
                i++;
            }

            return true;
        }

        /// <summary>
        /// Returns true if the character at the specified index 
        /// is the specified char. 
        /// Leaves index one past the character.
        /// </summary>
        private static bool Sink(string expression, ref int i, char c)
        {
            if (i < expression.Length && expression[i] == c)
            {
                i++;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the next two characters at the specified index
        /// are the specified sequence.
        /// Leaves index one past the second character.
        /// </summary>
        private static bool Sink(string expression, ref int i, int end, char c1, char c2)
        {
            if (i < end - 1 && expression[i] == c1 && expression[i + 1] == c2)
            {
                i += 2;
                return true;
            }

            return false;
        }
        /// <summary>
        /// Moves past all whitespace starting at the specified index.
        /// Returns the next index, possibly the string length.
        /// </summary>
        /// <remarks>
        /// Char.IsWhitespace() is not identical in behavior to regex's \s character class,
        /// but it's extremely close, and it's what we use in conditional expressions.
        /// </remarks>
        private static void SinkWhitespace(string expression, ref int i)
        {
            while (i < expression.Length && Char.IsWhiteSpace(expression[i]))
            {
                i++;
            }
        }
    }

    # region Related Types

    /// <summary>
    /// What the shredder should be looking for.
    /// </summary>
    [Flags]
    internal enum ShredderOptions
    {
        Invalid = 0x0,
        ItemTypes = 0x1,
        MetadataOutsideTransforms = 0x2,
        All = ItemTypes | MetadataOutsideTransforms
    }

    /// <summary>
    /// Wrapper of two tables for a convenient method return value.
    /// </summary>
    internal struct ItemsAndMetadataPair
    {
        private Hashtable items;
        private Dictionary<string, MetadataReference> metadata;

        internal ItemsAndMetadataPair(Hashtable items, Dictionary<string, MetadataReference> metadata)
        {
            this.items = items;
            this.metadata = metadata;
        }

        internal Hashtable Items
        {
            get { return items; }
            set { items = value; }
        }

        internal Dictionary<string, MetadataReference> Metadata
        {
            get { return metadata; }
            set { metadata = value; }
        }
    }

    // This struct represents a reference to a piece of item metadata.  For example,
    // %(EmbeddedResource.Culture) or %(Culture) in the project file.  In this case,
    // "EmbeddedResource" is the item name, and "Culture" is the metadata name.
    // The item name is optional.
    internal struct MetadataReference
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="itemName">can be null</param>
        /// <param name="metadataName"></param>
        internal MetadataReference
        (
            string itemName,
            string metadataName
        )
        {
            this.itemName = itemName;
            this.metadataName = metadataName;
        }

        internal string itemName;       // Could be null if the %(...) is not qualified with an item name.
        internal string metadataName;
    }

    #endregion
}
