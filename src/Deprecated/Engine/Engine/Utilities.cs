// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;

using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class contains utility methods for the MSBuild engine.
    /// </summary>
    /// <owner>RGoel</owner>
    static public class Utilities
    {
        private readonly static Regex singlePropertyRegex = new Regex(@"^\$\(([^\$\(\)]*)\)$");

        /// <summary>
        /// Update our table which keeps track of all the properties that are referenced
        /// inside of a condition and the string values that they are being tested against.
        /// So, for example, if the condition was " '$(Configuration)' == 'Debug' ", we
        /// would get passed in leftValue="$(Configuration)" and rightValueExpanded="Debug".
        /// This call would add the string "Debug" to the list of possible values for the 
        /// "Configuration" property.
        ///
        /// This method also handles the case when two or more properties are being
        /// concatenated together with a vertical bar, as in '
        ///     $(Configuration)|$(Platform)' == 'Debug|x86'
        /// </summary>
        /// <param name="conditionedPropertiesTable"></param>
        /// <param name="leftValue"></param>
        /// <param name="rightValueExpanded"></param>
        /// <owner>rgoel</owner>
        internal static void UpdateConditionedPropertiesTable
        (
            Hashtable conditionedPropertiesTable,   // Hash table containing a StringCollection
                                                    // of possible values, keyed by property name.

            string leftValue,                       // The raw value on the left side of the operator

            string rightValueExpanded               // The fully expanded value on the right side
                                                    // of the operator.
        )
        {
            if ((conditionedPropertiesTable != null) && (rightValueExpanded.Length > 0))
            {
                // The left side should be exactly "$(propertyname)" or "$(propertyname1)|$(propertyname2)"
                // or "$(propertyname1)|$(propertyname2)|$(propertyname3)", etc.  Anything else,
                // and we don't touch the table.

                // Split up the leftValue into pieces based on the vertical bar character.
                string[] leftValuePieces = leftValue.Split(new char[]{'|'});

                // Loop through each of the pieces.
                for (int i = 0 ; i < leftValuePieces.Length ; i++)
                {
                    Match singlePropertyMatch = singlePropertyRegex.Match(leftValuePieces[i]);

                    if (singlePropertyMatch.Success)
                    {
                        // Find the first vertical bar on the right-hand-side expression.
                        int indexOfVerticalBar = rightValueExpanded.IndexOf('|');
                        string rightValueExpandedPiece;

                        // If there was no vertical bar, then just use the remainder of the right-hand-side
                        // expression as the value of the property, and terminate the loop after this iteration.  
                        // Also, if we're on the last segment of the left-hand-side, then use the remainder
                        // of the right-hand-side expression as the value of the property.
                        if ((indexOfVerticalBar == -1) || (i == (leftValuePieces.Length - 1)))
                        {
                            rightValueExpandedPiece = rightValueExpanded;
                            i = leftValuePieces.Length;
                        }
                        else
                        {
                            // If we found a vertical bar, then the portion before the vertical bar is the
                            // property value which we will store in our table.  Then remove that portion 
                            // from the original string so that the next iteration of the loop can easily search
                            // for the first vertical bar again.
                            rightValueExpandedPiece = rightValueExpanded.Substring(0, indexOfVerticalBar);
                            rightValueExpanded = rightValueExpanded.Substring(indexOfVerticalBar + 1);
                        }

                        // Capture the property name out of the regular expression.
                        string propertyName = singlePropertyMatch.Groups[1].ToString();

                        // Get the string collection for this property name, if one already exists.
                        StringCollection conditionedPropertyValues = 
                            (StringCollection) conditionedPropertiesTable[propertyName];

                        // If this property is not already represented in the table, add a new entry
                        // for it.
                        if (conditionedPropertyValues == null)
                        {
                            conditionedPropertyValues = new StringCollection();
                            conditionedPropertiesTable[propertyName] = conditionedPropertyValues;
                        }

                        // If the "rightValueExpanded" is not already in the string collection
                        // for this property name, add it now.
                        if (!conditionedPropertyValues.Contains(rightValueExpandedPiece))
                        {
                            conditionedPropertyValues.Add(rightValueExpandedPiece);
                        }
                    }
                }
            }
        }

        /*
         * Method:  GatherReferencedPropertyNames
         * Owner:   DavidLe
         * 
         * Find and record all of the properties that are referenced in the given
         * condition.
         *
         * FUTURE: it is unfortunate that we have to completely parse+evaluate the expression
         */
        internal static void GatherReferencedPropertyNames
        (
            string          condition,                  // Can be null
            XmlAttribute    conditionAttribute,         // XML attribute on which the condition is evaluated
            Expander        expander,                   // The set of properties to use for expansion
            Hashtable       conditionedPropertiesTable  // Can be null
        )
        {
            EvaluateCondition(condition, conditionAttribute, expander, conditionedPropertiesTable, ParserOptions.AllowProperties | ParserOptions.AllowItemLists, null, null);
        }

        // An array of hashtables with cached expression trees for all the combinations of condition strings 
        // and parser options
        private static volatile Hashtable[] cachedExpressionTrees = new Hashtable[8 /* == ParserOptions.AllowAll*/]
            {
                new Hashtable(StringComparer.OrdinalIgnoreCase), new Hashtable(StringComparer.OrdinalIgnoreCase), 
                new Hashtable(StringComparer.OrdinalIgnoreCase), new Hashtable(StringComparer.OrdinalIgnoreCase), 
                new Hashtable(StringComparer.OrdinalIgnoreCase), new Hashtable(StringComparer.OrdinalIgnoreCase), 
                new Hashtable(StringComparer.OrdinalIgnoreCase), new Hashtable(StringComparer.OrdinalIgnoreCase)
            };

        /// <summary>
        /// Evaluates a string representing a condition from a "condition" attribute.
        /// If the condition is a malformed string, it throws an InvalidProjectFileException.
        /// This method uses cached expression trees to avoid generating them from scratch every time it's called.
        /// This method is thread safe and is called from engine and task execution module threads
        /// </summary>
        /// <param name="condition">Can be null</param>
        /// <param name="conditionAttribute">XML attribute on which the condition is evaluated</param>
        /// <param name="expander">All the data available for expanding embedded properties, metadata, and items</param>
        /// <param name="itemListOptions"></param>
        /// <returns>true, if the expression evaluates to true, otherwise false</returns>
        internal static bool EvaluateCondition
        (
            string condition,
            XmlAttribute conditionAttribute,
            Expander expander,
            ParserOptions itemListOptions,
            Project parentProject
        )
        {
            return EvaluateCondition(condition,
                                     conditionAttribute,
                                     expander,
                                     parentProject.ConditionedProperties,
                                     itemListOptions,
                                     parentProject.ParentEngine.LoggingServices,
                                     parentProject.ProjectBuildEventContext);
        }

        /// <summary>
        /// Evaluates a string representing a condition from a "condition" attribute.
        /// If the condition is a malformed string, it throws an InvalidProjectFileException.
        /// This method uses cached expression trees to avoid generating them from scratch every time it's called.
        /// This method is thread safe and is called from engine and task execution module threads
        /// </summary>
        /// <param name="condition">Can be null</param>
        /// <param name="conditionAttribute">XML attribute on which the condition is evaluated</param>
        /// <param name="expander">All the data available for expanding embedded properties, metadata, and items</param>
        /// <param name="itemListOptions"></param>
        /// <param name="loggingServices">Can be null</param>
        /// <param name="eventContext"> contains contextual information for logging events</param>
        /// <returns>true, if the expression evaluates to true, otherwise false</returns>
        internal static bool EvaluateCondition
        (
            string condition,
            XmlAttribute conditionAttribute,
            Expander expander,
            ParserOptions itemListOptions,
            EngineLoggingServices loggingServices,
            BuildEventContext buildEventContext
        )
        {
            return EvaluateCondition(condition,
                                     conditionAttribute,
                                     expander,
                                     null,
                                     itemListOptions,
                                     loggingServices,
                                     buildEventContext);
        }

        /// <summary>
        /// Evaluates a string representing a condition from a "condition" attribute.
        /// If the condition is a malformed string, it throws an InvalidProjectFileException.
        /// This method uses cached expression trees to avoid generating them from scratch every time it's called.
        /// This method is thread safe and is called from engine and task execution module threads
        /// </summary>
        /// <param name="condition">Can be null</param>
        /// <param name="conditionAttribute">XML attribute on which the condition is evaluated</param>
        /// <param name="expander">All the data available for expanding embedded properties, metadata, and items</param>
        /// <param name="conditionedPropertiesTable">Can be null</param>
        /// <param name="itemListOptions"></param>
        /// <param name="loggingServices">Can be null</param>
        /// <param name="buildEventContext"> contains contextual information for logging events</param>
        /// <returns>true, if the expression evaluates to true, otherwise false</returns>
        internal static bool EvaluateCondition
        (
            string condition,
            XmlAttribute conditionAttribute,
            Expander expander,
            Hashtable conditionedPropertiesTable,
            ParserOptions itemListOptions,
            EngineLoggingServices loggingServices,
            BuildEventContext buildEventContext
        )
        {
            ErrorUtilities.VerifyThrow((conditionAttribute != null) || (string.IsNullOrEmpty(condition)),
                "If condition is non-empty, you must provide the XML node representing the condition.");

            // An empty condition is equivalent to a "true" condition.
            if (string.IsNullOrEmpty(condition))
            {
                return true;
            }

            Hashtable cachedExpressionTreesForCurrentOptions = cachedExpressionTrees[(int)itemListOptions];

            // Try and see if we have an expression tree for this condition already
            GenericExpressionNode parsedExpression = (GenericExpressionNode) cachedExpressionTreesForCurrentOptions[condition];

            if (parsedExpression == null)
            {
                Parser conditionParser = new Parser();

                #region REMOVE_COMPAT_WARNING
                conditionParser.LoggingServices = loggingServices;
                conditionParser.LogBuildEventContext = buildEventContext;
                #endregion

                parsedExpression = conditionParser.Parse(condition, conditionAttribute, itemListOptions);

                // It's possible two threads will add a different tree to the same entry in the hashtable, 
                // but it should be rare and it's not a problem - the previous entry will be thrown away.
                // We could ensure no dupes with double check locking but it's not really necessary here.
                // Also, we don't want to lock on every read.
                lock (cachedExpressionTreesForCurrentOptions)
                {
                    cachedExpressionTreesForCurrentOptions[condition] = parsedExpression;
                }
            }

            ConditionEvaluationState state = new ConditionEvaluationState(conditionAttribute, expander, conditionedPropertiesTable, condition);
            bool result;

            // We are evaluating this expression now and it can cache some state for the duration,
            // so we don't want multiple threads working on the same expression
            lock (parsedExpression)
            {
                result = parsedExpression.Evaluate(state);
                parsedExpression.ResetState();
            }

            return result;
        }

        /// <summary>
        /// Sets the inner XML/text of the given XML node, escaping as necessary.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="node"></param>
        /// <param name="s">Can be empty string, but not null.</param>
        internal static void SetXmlNodeInnerContents(XmlNode node, string s)
        {
            ErrorUtilities.VerifyThrow(s != null, "Need value to set.");

            if (s.IndexOf('<') != -1)
            {
                // If the value looks like it probably contains XML markup ...
                try
                {
                    // Attempt to store it verbatim as XML.
                    node.InnerXml = s;
                    return;
                }
                catch (XmlException)
                {
                    // But that may fail, in the event that "s" is not really well-formed
                    // XML.  Eat the exception and fall through below ...
                }
            }
                
            // The value does not contain valid XML markup.  Store it as text, so it gets 
            // escaped properly.
            node.InnerText = s;
        }

        /// <summary>
        /// Extracts the inner XML/text of the given XML node, unescaping as necessary.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="node"></param>
        /// <returns>Inner XML/text of specified node.</returns>
        internal static string GetXmlNodeInnerContents(XmlNode node)
        {
            // XmlNode.InnerXml gives back a string that consists of the set of characters
            // in between the opening and closing elements of the XML node, without doing any
            // unescaping.  Any "strange" character sequences (like "<![CDATA[...]]>" will remain 
            // exactly so and will not be translated or interpreted.  The only modification that
            // .InnerXml will do is that it will normalize any Xml contained within.  This means
            // normalizing whitespace between XML attributes and quote characters that surround XML 
            // attributes.  If PreserveWhitespace is false, then it will also normalize whitespace
            // between elements.
            //
            // XmlNode.InnerText strips out any Xml contained within, and then unescapes the rest
            // of the text.  So if the remaining text contains certain character sequences such as
            // "&amp;" or "<![CDATA[...]]>", these will be translated into their equivalent representations.
            //
            // It's hard to explain, but much easier to demonstrate with examples:
            //
            // Original XML                     XmlNode.InnerText               XmlNode.InnerXml
            // ===========================      ==============================  ======================================
            //
            // <a><![CDATA[whatever]]></a>      whatever                        <![CDATA[whatever]]>
            //
            // <a>123<MyNode/>456</a>           123456                          123<MyNode />456
            //
            // <a>123456</a>                    123456                          123456
            //
            // <a>123<MyNode b='&lt;'/>456</a>  123456                          123<MyNode b="&lt;" />456
            //
            // <a>123&amp;456</a>               123&456                         123&amp;456

            // So the trick for MSBuild when interpreting a property value is to know which one to
            // use ... InnerXml or InnerText.  There are two basic scenarios we care about.
            //
            // 1.)  The first scenario is that the user is trying to create a property whose
            //      contents are actually XML.  That is to say that the contents may be written 
            //      to a XML file, or may be passed in as a string to XmlDocument.LoadXml.
            //      In this case, we would want to use XmlNode.InnerXml, because we DO NOT want 
            //      character sequences to be unescaped.  If we did unescape them, then whatever 
            //      XML parser tried to read in the stream as XML later on would totally barf.
            //
            // 2.)  The second scenario is the the user is trying to create a property that
            //      is just intended to be treated as a string.  That string may be very large
            //      and could contain all sorts of whitespace, carriage returns, special characters,
            //      etc.  But in the end, it's just a big string.  In this case, whatever 
            //      task is actually processing this string ... it's not going to know anything
            //      about character sequences such as &amp; and &lt;.  These character sequences
            //      are specific to XML markup.  So, here we want to use XmlNode.InnerText so that 
            //      the character sequences get unescaped into their actual character before
            //      the string is passed to the task (or wherever else the property is used).
            //      Of course, if the string value of the property needs to contain characters
            //      like <, >, &, etc., then the user must XML escape these characters otherwise
            //      the XML parser reading the project file will croak.  Or if the user doesn't
            //      want to escape every instance of these characters, he can surround the whole
            //      thing with a CDATA tag.  Again, if he does this, we don't want the task to
            //      receive the C, D, A, T, A as part of the string ... this should be stripped off.
            //      Again, using XmlNode.InnerText takes care of this.
            //
            // 2b.) A variation of the second scenario is that the user is trying to create a property
            //      that is just intended to be a string, but wants to comment out part of the string.
            //      For example, it's a semicolon separated list that's going ultimately to end up in a list.
            //      eg. (DDB #56841)
            //
            //     <BuildDirectories>
            //        <!--
            //              env\TestTools\tshell\pkg;
            //        -->
            //                ndp\fx\src\VSIP\FrameWork;
            //                ndp\fx\src\xmlTools;
            //                ddsuites\src\vs\xmlTools;
            //     </BuildDirectories>
            //
            //      In this case, we want to treat the string as text, so that we don't retrieve the comment.
            //      We only want to retrieve the comment if there's some other XML in there. The
            //      mere presence of an XML comment shouldn't make us think the value is XML.
            //
            // Given these two scenarios, how do we know whether the user intended to treat
            // a property value as XML or text?  We use a simple heuristic which is that if
            // XmlNode.InnerXml contains any "<" characters, then there pretty much has to be
            // XML in there, so we'll just use XmlNode.InnerXml.  If there are no "<" characters that aren't merely comments,
            // then we assume it's to be treated as text and we use XmlNode.InnerText.  Also, if
            // it looks like the whole thing is one big CDATA block, then we also use XmlNode.InnerText.

            // XmlNode.InnerXml is much more expensive than InnerText. Don't use it for trivial cases.
            // (single child node with a trivial value or no child nodes)
            if (!node.HasChildNodes)
            {
                return string.Empty;
            }

            if (node.ChildNodes.Count == 1 && (node.FirstChild.NodeType == XmlNodeType.Text || node.FirstChild.NodeType == XmlNodeType.CDATA))
            {
                return node.InnerText;
            }

            string innerXml = node.InnerXml;

            // If there is no markup under the XML node (detected by the presence
            // of a '<' sign
            int firstLessThan = innerXml.IndexOf('<');
            if (firstLessThan == -1)
            {
                // return the inner text so it gets properly unescaped
                return node.InnerText;
            }

            bool containsNoTagsOtherThanComments = ContainsNoTagsOtherThanComments(innerXml, firstLessThan);

            // ... or if the only XML is comments,
            if (containsNoTagsOtherThanComments)
            {
                // return the inner text so the comments are stripped
                // (this is how one might comment out part of a list in a property value)
                return node.InnerText;
            }

            // ...or it looks like the whole thing is a big CDATA tag ...
            bool startsWithCData = (innerXml.IndexOf("<![CDATA[", StringComparison.Ordinal) == 0);

            if (startsWithCData)
            {
                // return the inner text so it gets properly extracted from the CDATA
                return node.InnerText;
            }

            // otherwise, it looks like genuine XML; return the inner XML so that
            // tags and comments are preserved and any XML escaping is preserved
            return innerXml;
        }

        /// <summary>
        /// Figure out whether there are any XML tags, other than comment tags,
        /// in the string.
        /// </summary>
        /// <remarks>
        /// We know the string coming in is a valid XML fragment. (The project loaded after all.)
        /// So for example we can ignore an open comment tag without a matching closing comment tag.
        /// </remarks>
        private static bool ContainsNoTagsOtherThanComments(string innerXml, int firstLessThan)
        {
            bool insideComment = false;
            for (int i = firstLessThan; i < innerXml.Length; i++)
            {
                if (!insideComment)
                {
                    // XML comments start with exactly "<!--"
                    if (i < innerXml.Length - 3
                        && innerXml[i] == '<'
                        && innerXml[i + 1] == '!'
                        && innerXml[i + 2] == '-'
                        && innerXml[i + 3] == '-')
                    {
                        // Found the start of a comment
                        insideComment = true;
                        i += 3;
                        continue;
                    }
                }

                if (!insideComment)
                {
                    if (innerXml[i] == '<')
                    {
                        // Found a tag!
                        return false;
                    }
                }

                if (insideComment)
                {
                    // XML comments end with exactly "-->"
                    if (i < innerXml.Length - 2
                        && innerXml[i] == '-'
                        && innerXml[i + 1] == '-'
                        && innerXml[i + 2] == '>')
                    {
                        // Found the end of a comment
                        insideComment = false;
                        i += 2;
                        continue;
                    }
                }
            }

            // Didn't find any tags, except possibly comments
            return true;
        }

        // used to find the xmlns attribute
        private static readonly Regex xmlnsPattern = new Regex("xmlns=\"[^\"]*\"\\s*");

        /// <summary>
        /// Removes the xmlns attribute from an XML string.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="xml">XML string to process.</param>
        /// <returns>The modified XML string.</returns>
        internal static string RemoveXmlNamespace(string xml)
        {
            return xmlnsPattern.Replace(xml, String.Empty);
        }

        /// <summary>
        /// Escapes given string, that is replaces special characters with escape sequences that allow MSBuild hosts
        /// to treat MSBuild-interpreted characters literally (';' becomes "%3b" and so on).
        /// </summary>
        /// <param name="unescapedExpression">string to escape</param>
        /// <returns>escaped string</returns>
        public static string Escape(string unescapedExpression)
        {
            return EscapingUtilities.Escape(unescapedExpression);
        }

        /// <summary>
        /// Instantiates a new BuildEventFileInfo object using an XML node (presumably from the project
        /// file).  The reason this isn't just another constructor on BuildEventFileInfo is because
        /// BuildEventFileInfo.cs gets compiled into multiple assemblies (Engine and Conversion, at least),
        /// and not all of those assemblies have the code for XmlUtilities.
        /// </summary>
        /// <param name="xmlNode"></param>
        /// <param name="defaultFile"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        internal static BuildEventFileInfo CreateBuildEventFileInfo(XmlNode xmlNode, string defaultFile)
        {
            ErrorUtilities.VerifyThrow(xmlNode != null, "Need Xml node.");

            // Get the file path out of the Xml node.
            int line = 0;
            int column = 0;
            string file = XmlUtilities.GetXmlNodeFile(xmlNode, String.Empty);

            if (file.Length == 0)
            {
                file = defaultFile;
            }
            else
            {
                // Compute the line number and column number of the XML node.
                XmlSearcher.GetLineColumnByNode(xmlNode, out line, out column);
            }

            return new BuildEventFileInfo(file, line, column);
        }


        /// <summary>
        /// Helper useful for lazy table creation
        /// </summary>
        internal static Hashtable CreateTableIfNecessary(Hashtable table)
        {
            if (table == null)
            {
                return new Hashtable(StringComparer.OrdinalIgnoreCase);
            }

            return table;
        }

        /// <summary>
        /// Helper useful for lazy table creation
        /// </summary>
        internal static Dictionary<string, V> CreateTableIfNecessary<V>(Dictionary<string, V> table)
        {
            if (table == null)
            {
                return new Dictionary<string, V>(StringComparer.OrdinalIgnoreCase);
            }

            return table;
        }
    }
}
