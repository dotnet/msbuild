// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Microsoft.Build.BuildEngine.Shared;
using Microsoft.Win32;
using System.IO;
using System.Security;
using System.Globalization;
using System.Diagnostics;
using System.Reflection;

namespace Microsoft.Build.BuildEngine
{
    internal class Expander
    {
        /// <summary>
        /// Debugging aid and emergency exit for customers.
        /// Allows any functions to be used not just the safe list.
        /// </summary>
        private static bool enableAllPropertyFunctions = (Environment.GetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS") == "1");

        // Items and properties to refer to
        private ReadOnlyLookup lookup;

        // If we're only initialized with properties, store them directly
        // instead of using the overhead of a lookup
        private BuildPropertyGroup properties;

        // Table of metadata values. 
        // May have some qualified keys (type.name) or all unqualified.
        // If all unqualified, the implicitMetadataItemType field indicates the type.
        private Dictionary<string, string> itemMetadata;
        private string implicitMetadataItemType;

        // An optional item definition library to refer to when expanding metadata in expressions
        private SpecificItemDefinitionLibrary specificItemDefinitionLibrary;

        private ExpanderOptions options;

        /// <summary>
        /// Accessor for the item metadata used for metadata expansion (not counting metadata
        /// referenced inside a transform).
        /// </summary>
        internal Dictionary<string, string> ItemMetadata
        {
            get { return itemMetadata; }
        }

        #region Constructors

        /// <summary>
        /// Special cased constructor. Where we are only going to expand properties,
        /// it's a waste of memory to use a lookup. Just use the property group.
        /// PERF: This improves the EvaluateAllPropertyGroups codepath.
        /// </summary>
        internal Expander(BuildPropertyGroup properties)
        {
            this.options = ExpanderOptions.ExpandProperties;
            this.properties = properties;
        }

        /// <summary>
        /// Special cased constructor. Where we are only going to expand properties and metadata,
        /// it's a waste of memory to use a lookup. Just use the property group.
        /// PERF: This improves the EvaluateAllItemDefinitions codepath.
        /// </summary>
        internal Expander(BuildPropertyGroup properties, string implicitMetadataItemType, Dictionary<string, string> unqualifiedItemMetadata)
        {
            this.options = ExpanderOptions.ExpandPropertiesAndMetadata;
            this.properties = properties;
            this.itemMetadata = unqualifiedItemMetadata;
            this.implicitMetadataItemType = implicitMetadataItemType;
        }

        // Used in many places
        internal Expander(BuildPropertyGroup properties, Hashtable items)
            : this(new ReadOnlyLookup(items, properties), null, ExpanderOptions.ExpandPropertiesAndItems)
        {
        }

        // Used by BuildItemGroup.Evaluate
        internal Expander(BuildPropertyGroup properties, Hashtable items, ExpanderOptions options)
            : this(new ReadOnlyLookup(items, properties), null, options)
        {
        }

        // Used by ItemBucket
        internal Expander(ReadOnlyLookup lookup, Dictionary<string, string> itemMetadata)
            : this(lookup, itemMetadata, ExpanderOptions.ExpandAll)
        {
        }

        // Used by IntrinsicTask
        internal Expander(ReadOnlyLookup lookup)
            : this(lookup, null, ExpanderOptions.ExpandPropertiesAndItems)
        {
        }

        // Used by unit tests
        internal Expander(ReadOnlyLookup lookup, Dictionary<string, string> itemMetadata, ExpanderOptions options)
        {
            ErrorUtilities.VerifyThrow(options != ExpanderOptions.Invalid, "Must specify options");

            this.lookup = lookup;
            this.itemMetadata = itemMetadata;
            this.options = options;
        }

        /// <summary>
        /// Create an expander from another expander, but with different
        /// options
        /// </summary>
        internal Expander(Expander expander, ExpanderOptions options)
            : this(expander.lookup, expander.itemMetadata, options)
        {
        }

        internal Expander(Expander expander, SpecificItemDefinitionLibrary itemDefinitionLibrary)
            : this(expander.lookup, null , expander.options)
        {
            if (implicitMetadataItemType == null)
            {
                this.itemMetadata = expander.itemMetadata;
            }
            this.specificItemDefinitionLibrary = itemDefinitionLibrary;
        }

#endregion

        /// <summary>
        /// Adds metadata to the table being used by this expander.
        /// This is useful when expanding metadata definitions that may refer to other values defined
        /// immediately above: as each value is expanded, it is added to the table in the expander.
        /// </summary>
        internal void SetMetadataInMetadataTable(string itemType, string name, string value)
        {
            ErrorUtilities.VerifyThrow((options & ExpanderOptions.ExpandMetadata) != 0, "Must be expanding metadata");
            ErrorUtilities.VerifyThrow(implicitMetadataItemType == null || String.Equals(implicitMetadataItemType, itemType, StringComparison.OrdinalIgnoreCase), "Unexpected metadata type");

            if (itemMetadata == null)
            {
                itemMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                implicitMetadataItemType = itemType;
            }

            if (String.Equals(implicitMetadataItemType, itemType, StringComparison.OrdinalIgnoreCase))
            {
                itemMetadata[name] = value;
            }
            else
            {
                itemMetadata[itemType + "." + name] = value;
            }
        }

        /// <summary>
        /// Expands item metadata, properties, and items (in that order), and produces a list of TaskItems.
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="expressionAttribute"></param>
        /// <returns></returns>
        internal List<BuildItem> ExpandAllIntoBuildItems
            (
            string expression,
            XmlAttribute expressionAttribute
            )
        {
            // We don't know how many items we're going to end up with, but we'll
            // keep adding them to this arraylist as we find them.
            List<BuildItem> buildItems = new List<BuildItem>();

            string evaluatedParameterValue = this.ExpandPropertiesLeaveEscaped(this.ExpandMetadataLeaveEscaped(expression), expressionAttribute);

            if (evaluatedParameterValue.Length > 0)
            {
                // Take the string that is being passed into the task parameter in the
                // project XML file, and split it up by semicolons.  Loop through each
                // piece individually.
                List<string> userSpecifiedItemExpressions = ExpressionShredder.SplitSemiColonSeparatedList(evaluatedParameterValue);
                foreach (string userSpecifiedItemExpression in userSpecifiedItemExpressions)
                {
                    BuildItemGroup itemsToAdd = this.ExpandSingleItemListExpressionIntoItemsLeaveEscaped(userSpecifiedItemExpression, expressionAttribute);
                    if (itemsToAdd != null)
                    {
                        foreach (BuildItem itemToAdd in itemsToAdd)
                        {
                            buildItems.Add(itemToAdd);
                        }
                    }
                    else
                    {
                        // The expression is not of the form @(itemName).  Therefore, just
                        // treat it as a string, and create a new TaskItem from that string.
                        buildItems.Add(new BuildItem(null, userSpecifiedItemExpression));
                    }
                }
            }

            return buildItems;
        }

        /// <summary>
        /// Expands item metadata, properties, and items (in that order), and produces a list of TaskItems.
        /// 
        /// All data accessed through the TaskItem (ItemSpec and metadata) is going to be unescaped, so it's nice 
        /// and ready for a task to consume.
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="expressionAttribute"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        internal List<TaskItem> ExpandAllIntoTaskItems
            (
            string expression,
            XmlAttribute expressionAttribute
            )
        {
            List<BuildItem> buildItems = ExpandAllIntoBuildItems(expression, expressionAttribute);

            List<TaskItem> taskItems = new List<TaskItem>(buildItems.Count);
            for (int i = 0; i < buildItems.Count; i++)
            {
                if (!buildItems[i].IsUninitializedItem)
                {
                    taskItems.Add(new TaskItem(buildItems[i]));
                }
                else
                {
                    taskItems.Add(new TaskItem(buildItems[i].FinalItemSpecEscaped));
                }
            }

            return taskItems;
        }

        /// <summary>
        /// An overload of ExpandAllIntoString that conveniently only takes in an XmlAttribute whose
        /// value we should expand.
        /// </summary>
        /// <param name="expressionAttribute"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        internal string ExpandAllIntoString
            (
            XmlAttribute expressionAttribute
            )
        {
            return this.ExpandAllIntoString(expressionAttribute.Value, expressionAttribute);
        }

        /// <summary>
        /// Expands embedded item metadata, properties, and embedded item lists (in that order)
        /// within an expression.
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="expressionNode">The XML attribute containing the string we're trying to expand here.  Solely
        /// for the purposes of providing line/column number information when there's an error.</param>
        /// <returns>fully expanded string</returns>
        /// <owner>RGoel</owner>
        internal string ExpandAllIntoString
            (
            string expression,
            XmlNode expressionNode
            )
        {
            return EscapingUtilities.UnescapeAll(this.ExpandAllIntoStringLeaveEscaped(expression, expressionNode));
        }

        /// <summary>
        /// An overload of ExpandAllIntoString that conveniently only takes in an XmlAttribute whose
        /// value we should expand.
        /// </summary>
        /// <param name="expressionAttribute"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        internal string ExpandAllIntoStringLeaveEscaped
            (
            XmlAttribute expressionAttribute
            )
        {
            return this.ExpandAllIntoStringLeaveEscaped(expressionAttribute.Value, expressionAttribute);
        }

        /// <summary>
        /// Expands embedded item metadata, properties, and embedded item lists (in that order)
        /// within an expression.
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="expressionNode">The XML attribute containing the string we're trying to expand here.  Solely
        /// for the purposes of providing line/column number information when there's an error.</param>
        /// <returns>fully expanded string</returns>
        /// <owner>RGoel</owner>
        internal string ExpandAllIntoStringLeaveEscaped
            (
            string expression,
            XmlNode expressionNode
            )
        {
            ErrorUtilities.VerifyThrow(expression != null, "Must pass in non-null expression.");
            if (expression.Length == 0)
            {
                return expression;
            }

            return this.ExpandItemsIntoStringLeaveEscaped(this.ExpandPropertiesLeaveEscaped(this.ExpandMetadataLeaveEscaped(expression), expressionNode), expressionNode);
        }

        /// <summary>
        /// Expands metadata, properties, and items (in that order) into a list of strings.
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="expressionNode">The XML attribute containing the string we're trying to expand here.  Solely
        /// for the purposes of providing line/column number information when there's an error.</param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        internal List<string> ExpandAllIntoStringList
            (
            string expression,
            XmlNode expressionNode
            )
        {
            List<string> stringList = ExpressionShredder.SplitSemiColonSeparatedList(ExpandAllIntoStringLeaveEscaped(expression, expressionNode));

            for (int i = 0; i < stringList.Count; i++)
            {
                stringList[i] = EscapingUtilities.UnescapeAll(stringList[i]);
            }

            return stringList;
        }

        /// <summary>
        /// Expands metadata, properties, and items (in that order) into a list of strings.
        /// </summary>
        /// <param name="expressionAttribute"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        internal List<string> ExpandAllIntoStringList
            (
            XmlAttribute expressionAttribute
            )
        {
            return this.ExpandAllIntoStringList(expressionAttribute.Value, expressionAttribute);
        }

        /// <summary>
        /// Expands metadata, properties, and items (in that order) into a list of strings.
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="expressionNode">The XML attribute containing the string we're trying to expand here.  Solely
        /// for the purposes of providing line/column number information when there's an error.</param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        internal List<string> ExpandAllIntoStringListLeaveEscaped
            (
            string expression,
            XmlNode expressionNode
            )
        {
            return ExpressionShredder.SplitSemiColonSeparatedList(ExpandAllIntoStringLeaveEscaped(expression, expressionNode));
        }

        /// <summary>
        /// Expands metadata, properties, and items (in that order) into a list of strings.
        /// </summary>
        /// <param name="expressionAttribute"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        internal List<string> ExpandAllIntoStringListLeaveEscaped
            (
            XmlAttribute expressionAttribute
            )
        {
            return ExpandAllIntoStringListLeaveEscaped(expressionAttribute.Value, expressionAttribute);
        }

        /// <summary>
        /// This method takes a string which may contain any number of
        /// "$(propertyname)" tags in it.  It replaces all those tags with
        /// the actual property values, and returns a new string.  For example,
        ///
        ///     string processedString =
        ///         propertyBag.ExpandProperties("Value of NoLogo is $(NoLogo).");
        ///
        /// This code might produce:
        ///
        ///     processedString = "Value of NoLogo is true."
        ///
        /// If the sourceString contains an embedded property which doesn't
        /// have a value, then we replace that tag with an empty string.
        ///
        /// This method leaves the expression escaped.  Callers may need to unescape on their own as appropriate.
        /// This method leaves the result escaped.  Callers may need to unescape on their own as appropriate.
        /// </summary>
        internal string ExpandPropertiesLeaveEscaped
        (
            string sourceString,
            XmlNode sourceNode
        )
        {
            return ConvertToString(ExpandPropertiesLeaveTypedAndEscaped(sourceString, sourceNode));
        }

        /// <summary>
        /// This method takes a string which may contain any number of
        /// "$(propertyname)" tags in it.  It replaces all those tags with
        /// the actual property values, and returns a new string.  For example,
        ///
        ///     string processedString =
        ///         propertyBag.ExpandProperties("Value of NoLogo is $(NoLogo).");
        ///
        /// This code might produce:
        ///
        ///     processedString = "Value of NoLogo is true."
        ///
        /// If the sourceString contains an embedded property which doesn't
        /// have a value, then we replace that tag with an empty string.
        ///
        /// This method leaves the expression escaped.  Callers may need to unescape on their own as appropriate.
        /// </summary>
        /// <param name="expression"></param>
        /// <returns></returns>
        /// <owner>RGoel, JomoF</owner>
        private object ExpandPropertiesLeaveTypedAndEscaped
        (
            string expression,
            XmlNode expressionNode
        )
        {
            if (((options & ExpanderOptions.ExpandProperties) != ExpanderOptions.ExpandProperties) || String.IsNullOrEmpty(expression))
            {
                return expression;
            }

            // These are also zero-based indices into the sourceString, but
            // these tell us where the current property tag begins and ends.
            int propertyStartIndex, propertyEndIndex;

            // If there are no substitutions, then just return the string.
            propertyStartIndex = expression.IndexOf("$(", StringComparison.Ordinal);
            if (propertyStartIndex == -1)
            {
                return expression;
            }

            // We will build our set of results as object components
            // so that we can either maintain the object's type in the event
            // that we have a single component, or convert to a string
            // if concatenation is required.
            List<object> results = new List<object>();

            // The sourceIndex is the zero-based index into the sourceString,
            // where we've essentially read up to and copied into the target string.
            int sourceIndex = 0;

            // Search for "$(" in the sourceString.  Loop until we don't find it 
            // any more.
            while (propertyStartIndex != -1)
            {
                bool tryExtractPropertyFunction = false;
                bool tryExtractRegistryFunction = false;

                // Append the targetString with the portion of the sourceString up to
                // (but not including) the "$(", and advance the sourceIndex pointer.
                if (propertyStartIndex - sourceIndex > 0)
                {
                    results.Add(expression.Substring(sourceIndex, propertyStartIndex - sourceIndex));
                }
                sourceIndex = propertyStartIndex;

                // Following the "$(" we need to locate the matching ')'
                // Scan for the matching closing bracket, skipping any nested ones
                // This is a very complete, fast validation of parenthesis matching including for nested
                // function calls.
                propertyEndIndex = ScanForClosingParenthesis(expression, propertyStartIndex + 2, out tryExtractPropertyFunction, out tryExtractRegistryFunction);

                if (propertyEndIndex == -1)
                {
                    // If we didn't find the closing parenthesis, that means this
                    // isn't really a well-formed property tag.  Just literally
                    // copy the remainder of the sourceString (starting with the "$("
                    // that we found) into the targetString, and quit.
                    results.Add(expression.Substring(propertyStartIndex, expression.Length - propertyStartIndex));
                    sourceIndex = expression.Length;
                }
                else
                {
                    // Aha, we found the closing parenthesis.  All the stuff in
                    // between the "$(" and the ")" constitutes the property body.
                    // Note: Current propertyStartIndex points to the "$", and 
                    // propertyEndIndex points to the ")".  That's why we have to
                    // add 2 for the start of the substring, and subtract 2 for 
                    // the length.
                    string propertyBody = expression.Substring(propertyStartIndex + 2, propertyEndIndex - propertyStartIndex - 2);

                    // A property value of null will indicate that we're calling a static function on a type
                    object propertyValue = null;

                    // Compat: $() should return String.Empty
                    if (propertyBody.Length == 0)
                    {
                        propertyValue = String.Empty;
                    }
                    else if (tryExtractRegistryFunction && propertyBody.StartsWith("Registry:", StringComparison.OrdinalIgnoreCase))
                    {
                        // This is a registry reference, like $(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation)
                        propertyValue = ExpandRegistryValue(propertyBody, null);
                    }
                    else if (tryExtractPropertyFunction)
                    {
                        // This is either a regular property or a function expression
                        propertyValue = ExpandPropertyBody(propertyBody, propertyValue, properties, options);
                    }
                    else // This is a regular property
                    {
                        propertyValue = LookupProperty(properties, propertyBody, expressionNode);
                    }

                    // If it's a property function result, it may return null, so check before we add it.  
                    if (propertyValue != null)
                    {
                        // Append the property value to our targetString, and advance
                        // our sourceIndex pointer to the character just after the closing
                        // parenthesis.
                        results.Add(propertyValue);
                    }
                    sourceIndex = propertyEndIndex + 1;
                }

                propertyStartIndex = expression.IndexOf("$(", sourceIndex, StringComparison.Ordinal);
            }
            // If we have only a single result, then just return it
            if (results.Count == 1 && expression.Length == sourceIndex)
            {
                return results[0];
            }
            else
            {
                // We have more than one result collected, therefore we need to concatenate
                // into the final result string. This does mean that we will lose type information.
                // However since the user wanted contatenation, then they clearly wanted that to happen.

                // Initialize our output string to empty string.
                // PERF: This method is called very often - of the order of 3,000 times per project.
                // StringBuilder by default is initialized with a 16 char string and doubles the length
                // whenever it's too short. We want to avoid reallocation but also avoid excessive allocation.
                // The length of the source string turns out to be a fair compromise. (The final result may 
                // be longer or it may be shorter.)
                StringBuilder result = new StringBuilder(expression.Length);

                // We couldn't find anymore property tags in the expression,
                // so just literally copy the remainder into the result
                // and return.
                if (expression.Length - sourceIndex > 0)
                {
                    results.Add(expression.Substring(sourceIndex, expression.Length - sourceIndex));
                }

                // Create a combined result string from the result components that we've gathered
                foreach (object component in results)
                {
                    result.Append(component.ToString());
                }

                return result.ToString();
            }
        }

        /// <summary>
        /// Convert the object into an MSBuild friendly string
        /// Arrays are supported.
        /// </summary>
        private static string ConvertToString(object valueToConvert)
        {
            if (valueToConvert != null)
            {
                Type valueType = valueToConvert.GetType();
                string convertedString;

                // If the type is a string, then there is nothing to do
                if (valueType == typeof(string))
                {
                    convertedString = (string)valueToConvert;
                }
                else if (valueToConvert is IDictionary)
                {
                    // If the return type is an IDictionary, then we convert this to
                    // a semi-colon delimited set of A=B pairs.
                    // Key and Value are converted to string and escaped
                    IDictionary dictionary = valueToConvert as IDictionary;
                    StringBuilder builder = new StringBuilder();

                    foreach (DictionaryEntry entry in dictionary)
                    {
                        if (builder.Length > 0)
                        {
                            builder.Append(';');
                        }

                        // convert and escape each key and value in the dictionary entry
                        builder.Append(EscapingUtilities.Escape(ConvertToString(entry.Key)));
                        builder.Append('=');
                        builder.Append(EscapingUtilities.Escape(ConvertToString(entry.Value)));
                    }

                    convertedString = builder.ToString();
                }
                else if (valueToConvert is IEnumerable)
                {
                    // If the return is enumerable, then we'll convert to semi-colon delimted elements
                    // each of which must be converted, so we'll recurse for each element
                    StringBuilder builder = new StringBuilder();

                    IEnumerable enumerable = (IEnumerable)valueToConvert;

                    foreach (object element in enumerable)
                    {
                        if (builder.Length > 0)
                        {
                            builder.Append(';');
                        }

                        // we need to convert and escape each element of the array
                        builder.Append(EscapingUtilities.Escape(ConvertToString(element)));
                    }

                    convertedString = builder.ToString();
                }
                else
                {
                    // The fall back is always to just convert to a string directly.
                    convertedString = valueToConvert.ToString();
                }

                return convertedString;
            }
            else
            {
                return String.Empty;
            }
        }

        /// <summary>
        /// Scan for the closing bracket that matches the one we've already skipped;
        /// essentially, pushes and pops on a stack of parentheses to do this.
        /// Takes the expression and the index to start at.
        /// Returns the index of the matching parenthesis, or -1 if it was not found.
        /// </summary>
        private static int ScanForClosingParenthesis(string expression, int index)
        {
            bool potentialPropertyFunction = false;
            bool potentialRegistryFunction = false;
            return ScanForClosingParenthesis(expression, index, out potentialPropertyFunction, out potentialRegistryFunction);
        }

        /// <summary>
        /// Scan for the closing bracket that matches the one we've already skipped;
        /// essentially, pushes and pops on a stack of parentheses to do this.
        /// Takes the expression and the index to start at.
        /// Returns the index of the matching parenthesis, or -1 if it was not found.
        /// Also returns flags to indicate if a propertyfunction or registry property is likely
        /// to be found in the expression
        /// </summary>
        private static int ScanForClosingParenthesis(string expression, int index, out bool potentialPropertyFunction, out bool potentialRegistryFunction)
        {
            int nestLevel = 1;
            int length = expression.Length;

            potentialPropertyFunction = false;
            potentialRegistryFunction = false;

            // Scan for our closing ')'
            while (index < length && nestLevel > 0)
            {
                char character = expression[index];

                if (character == '(')
                {
                    nestLevel++;
                }
                else if (character == ')')
                {
                    nestLevel--;
                }
                else if (character == '.' || character == '[' || character == '$')
                {
                    potentialPropertyFunction = true;
                }
                else if (character == ':')
                {
                    potentialRegistryFunction = true;
                }

                index++;
            }

            // We will have parsed past the ')', so step back one character
            index--;

            return (nestLevel == 0) ? index : -1;
        }
        
        /// <summary>
        /// Expand the body of the property, including any functions that it may contain
        /// </summary>
        private object ExpandPropertyBody(string propertyBody, object propertyValue, BuildPropertyGroup properties, ExpanderOptions options)
        {
            Function function = null;
            string propertyName = propertyBody;

            // Trim the body for comatibility reasons:
            // Spaces are not valid property name chars, but $( Foo ) is allowed, and should always expand to BLANK.
            // Do a very fast check for leading and trailing whitespace, and trim them from the property body if we have any.
            // But we will do a property name lookup on the propertyName that we held onto.
            if (Char.IsWhiteSpace(propertyBody[0]) || Char.IsWhiteSpace(propertyBody[propertyBody.Length - 1]))
            {
                propertyBody = propertyBody.Trim();
            }

            // If we don't have a clean propertybody then we'll do deeper checks to see
            // if what we have is a function
            if (!IsValidPropertyName(propertyBody))
            {
                if (propertyBody.Contains(".") || propertyBody[0] == '[')
                {
                    // This is a function
                    function = Function.ExtractPropertyFunction(propertyBody, propertyValue);

                    // We may not have been able to parse out a function
                    if (function != null)
                    {
                        // We will have either extracted the actual property name
                        // or realised that there is none (static function), and have recorded a null
                        propertyName = function.ExpressionRootName;
                    }
                    else
                    {
                        // In the event that we have been handed an unrecognized property body, throw
                        // an invalid function property exception.
                        ProjectErrorUtilities.ThrowInvalidProject(null, "InvalidFunctionPropertyExpression", propertyBody, String.Empty);
                        return null;
                    }
                }
                else
                {
                    // In the event that we have been handed an unrecognized property body, throw
                    // an invalid function property exception.
                    ProjectErrorUtilities.ThrowInvalidProject(null, "InvalidFunctionPropertyExpression", propertyBody, String.Empty);
                    return null;
                }
            }

            // Find the property value in our property collection.  This 
            // will automatically return "" (empty string) if the property
            // doesn't exist in the collection, and we're not executing a static function
            if (!String.IsNullOrEmpty(propertyName))
            {
                BuildProperty property;
                if (lookup != null)
                {
                    // We're using a lookup
                    property = lookup.GetProperty(propertyName);
                }
                else
                {
                    // We're only using a property group
                    property = properties[propertyName];
                }

                if (property == null)
                {
                    propertyValue = String.Empty;
                }
                else
                {
                    propertyValue = property.FinalValueEscaped;
                }
            }

            if (function != null)
            {
                // Because of the rich expansion capabilities of MSBuild, we need to keep things
                // as strings, since property expansion & string embedding can happen anywhere
                // propertyValue can be null here, when we're invoking a static function
                propertyValue = function.Execute(this, propertyValue, properties, options);
            }

            return propertyValue;
        }

        /// <summary>
        /// Look up a simple property reference by the name of the property, e.g. "Foo" when expanding $(Foo)
        /// </summary>
        private object LookupProperty(BuildPropertyGroup properties, string propertyName, XmlNode expressionNode)
        {
            // Regular property
            BuildProperty property;
            object propertyValue;

            if (lookup != null)
            {
                // We're using a lookup
                property = lookup.GetProperty(propertyName);
            }
            else
            {
                // We're only using a property group
                property = properties[propertyName];
            }

            if (property == null)
            {
                propertyValue = String.Empty;

                // Support at least $(MSBuildThisFile)
                if (expressionNode != null && String.Equals(propertyName, "MSBuildThisFile", StringComparison.OrdinalIgnoreCase))
                {
                    string thisFile = XmlUtilities.GetXmlNodeFile(expressionNode, String.Empty /* default */);

                    if (!String.IsNullOrEmpty(thisFile))
                    {
                        propertyValue = Path.GetFileName(thisFile);
                    }
                }
            }
            else
            {
                propertyValue = property.FinalValueEscaped;
            }

            return propertyValue;
        }

        /// <summary>
        /// Returns true if the supplied string contains a valid property name
        /// </summary>
        private static bool IsValidPropertyName(string propertyName)
        {
            if (propertyName.Length == 0 || !XmlUtilities.IsValidInitialElementNameCharacter(propertyName[0]))
            {
                return false;
            }

            for (int n = 1; n < propertyName.Length; n++)
            {
                if (!XmlUtilities.IsValidSubsequentElementNameCharacter(propertyName[n]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Given a string like "Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation", return the value at that location
        /// in the registry. If the value isn't found, returns String.Empty.
        /// Properties may refer to a registry location by using the syntax for example
        /// "$(Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation)", where "HKEY_LOCAL_MACHINE\Software\Vendor\Tools" is the key and
        /// "TaskLocation" is the name of the value.  The name of the value and the preceding "@" may be omitted if
        /// the default value is desired.
        /// </summary>
        /// <param name="registryLocation">Expression to expand, eg "Registry:HKEY_LOCAL_MACHINE\Software\Vendor\Tools@TaskLocation"</param>
        /// <param name="node">Location associated with the expression, for purposes of good error messages</param>
        /// <returns></returns>
        private string ExpandRegistryValue(string registryExpression, XmlNode node)
        {
            string registryLocation = registryExpression.Substring(9);
            
            // Split off the value name -- the part after the "@" sign. If there's no "@" sign, then it's the default value name
            // we want.
            int firstAtSignOffset = registryLocation.IndexOf('@');
            int lastAtSignOffset = registryLocation.LastIndexOf('@');

            ProjectErrorUtilities.VerifyThrowInvalidProject(firstAtSignOffset == lastAtSignOffset, node, "InvalidRegistryPropertyExpression", "$(" + registryExpression + ")", String.Empty);

            string valueName = lastAtSignOffset == -1 || lastAtSignOffset == registryLocation.Length - 1 
                ? null : registryLocation.Substring(lastAtSignOffset + 1);
            
            // If there's no '@', or '@' is first, then we'll use null or String.Empty for the location; otherwise
            // the location is the part before the '@'
            string registryKeyName = lastAtSignOffset != -1 ? registryLocation.Substring(0, lastAtSignOffset) : registryLocation;

            string result = String.Empty;
            if (registryKeyName != null)
            {
                // We rely on the '@' character to delimit the key and its value, but the registry
                // allows this character to be used in the names of keys and the names of values.
                // Hence we use our standard escaping mechanism to allow users to access such keys
                // and values.
                registryKeyName = EscapingUtilities.UnescapeAll(registryKeyName);

                if (valueName != null)
                {
                    valueName = EscapingUtilities.UnescapeAll(valueName);
                }

                try
                {
                    object valueFromRegistry = Registry.GetValue(registryKeyName,
                                                                 valueName,
                                                                 null /* default if key or value name is not found */);
                    if (null != valueFromRegistry)
                    {
                        // Convert the result to a string that is reasonable for MSBuild
                        result = ConvertToString(valueFromRegistry);
                    }
                    else
                    {
                        // This means either the key or value was not found in the registry.  In this case,
                        // we simply expand the property value to String.Empty to imitate the behavior of
                        // normal properties.
                        result = String.Empty;
                    }
                }
                catch (ArgumentException ex)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, node, "InvalidRegistryPropertyExpression", "$(" + registryExpression + ")", ex.Message);
                }
                catch (IOException ex)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, node, "InvalidRegistryPropertyExpression", "$(" + registryExpression + ")", ex.Message);
                }
                catch (SecurityException ex)
                {
                    ProjectErrorUtilities.VerifyThrowInvalidProject(false, node, "InvalidRegistryPropertyExpression", "$(" + registryExpression + ")", ex.Message);
                }
            }

            return result;
        }

        /// <summary>
        /// This class represents the function as extracted from a property expression
        /// It is also responsible for executing the function
        /// </summary>
        private class Function
        {
            /// <summary>
            /// The type that this function will act on
            /// </summary>
            private Type objectType;

            /// <summary>
            /// The name of the function
            /// </summary>
            private string name;

            /// <summary>
            /// The arguments for the function
            /// </summary>
            private string[] arguments;

            /// <summary>
            /// The expression that constitutes this function
            /// </summary>
            private string expression;

            /// <summary>
            /// The property name that is the context for this function
            /// </summary>
            private string expressionRootName;

            /// <summary>
            /// The binding flags that will be used during invocation of this function
            /// </summary>
            private BindingFlags bindingFlags;

            /// <summary>
            /// The remainder of the body once the function and arguments have been extracted
            /// </summary>
            private string remainder;

            /// <summary>
            /// Construct a function that will be executed during property evaluation
            /// </summary>
            public Function(Type objectType, string expression, string expressionRootName, string name, string[] arguments, BindingFlags bindingFlags, string remainder)
            {
                this.name = name;
                this.arguments = arguments;
                this.expressionRootName = expressionRootName;
                this.expression = expression;
                this.objectType = objectType;
                this.bindingFlags = bindingFlags;
                this.remainder = remainder;
            }

            /// <summary>
            /// Part of the extraction may result in the name of the property
            /// This accessor is used by the Expander
            /// Examples of expression root:
            ///     [System.Diagnostics.Process]::Start
            ///     SomeMSBuildProperty
            /// </summary>
            public string ExpressionRootName
            {
                get { return expressionRootName; }
            }

            /// <summary>
            /// The type of the instance on which this function acts
            /// </summary>
            public Type ObjectType
            {
                get { return objectType; }
            }

            /// <summary>
            /// Extract the function details from the given property function expression
            /// </summary>
            public static Function ExtractPropertyFunction(string expressionFunction, object propertyValue)
            {
                // If this a expression function rather than a static, then we'll capture the name of the property referenced
                string propertyName = null;

                // The type of the object that this function is part
                Type objectType = null;

                // By default the expression root is the whole function expression
                string expressionRoot = expressionFunction;

                // The arguments for this function start at the first '('
                // If there are no arguments, then we're a property getter
                int argumentStartIndex = expressionFunction.IndexOf('(');

                // If we have arguments, then we only want the content up to but not including the '('
                if (argumentStartIndex > -1)
                {
                    expressionRoot = expressionFunction.Substring(0, argumentStartIndex);
                }

                // We ended up with something we don't understand
                ProjectErrorUtilities.VerifyThrowInvalidProject(!String.IsNullOrEmpty(expressionRoot), null, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);

                // First we'll see if there is a static function being called
                // A static method is the content that follows the last "::", the rest being
                // the type
                int methodStartIndex = -1;

                // This is a static method call
                if (expressionRoot[0] == '[')
                {
                    int typeEndIndex = expressionRoot.IndexOf(']', 1);

                    if (typeEndIndex < 1)
                    {
                        // We ended up with something other than a function expression
                        ProjectErrorUtilities.ThrowInvalidProject(null, "InvalidFunctionStaticMethodSyntax", "$(" + expressionFunction + ")");
                    }

                    string typeName = expressionRoot.Substring(1, typeEndIndex - 1);
                    methodStartIndex = typeEndIndex + 1;

                    // Make an attempt to locate a type that matches the body of the expression.
                    // We won't throw on error here
                    objectType = GetTypeForStaticMethod(typeName);

                    if (objectType == null)
                    {
                        // We ended up with something other than a type
                        ProjectErrorUtilities.ThrowInvalidProject(null, "InvalidFunctionStaticMethodSyntax", "$(" + expressionFunction + ")");
                    }

                    if (expressionRoot.Length > methodStartIndex + 2 && expressionRoot[methodStartIndex] == ':' && expressionRoot[methodStartIndex + 1] == ':')
                    {
                        // skip over the "::"
                        methodStartIndex += 2;
                    }
                    else
                    {
                        // We ended up with something other than a static function expression
                        ProjectErrorUtilities.ThrowInvalidProject(null, "InvalidFunctionStaticMethodSyntax", "$(" + expressionFunction + ")");
                    }
                }
                else
                {
                    // No static function call was found, look for an instance function call next, such as in SomeStuff.ToLower()
                    methodStartIndex = expressionRoot.IndexOf('.');
                    if (methodStartIndex == -1)
                    {
                        // We don't have a function invocation in the expression root, return null
                        return null;
                    }
                    else
                    {
                        // skip over the '.';
                        methodStartIndex++;
                    }
                }

                // No type matched, therefore the content must be a property reference, or a recursive call as functions
                // are chained together
                if (objectType == null)
                {
                    int rootEndIndex = expressionRoot.IndexOf('.');
                    propertyName = expressionRoot.Substring(0, rootEndIndex);

                    // If propertyValue is null (we're not recursing), then we're expecting a valid property name
                    if (propertyValue == null && !IsValidPropertyName(propertyName))
                    {
                        // We extracted something that wasn't a valid property name, fail.
                        ProjectErrorUtilities.ThrowInvalidProject(null, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);
                    }

                    objectType = typeof(string);
                }

                // If we are recursively acting on a type that has been already produced
                // then pass that type inwards
                if (propertyValue != null)
                {
                    objectType = propertyValue.GetType();
                }

                Function function = ConstructFunction(expressionFunction, propertyName, objectType, argumentStartIndex, methodStartIndex);

                return function;
            }

            /// <summary>
            /// Execute the function on the given instance
            /// </summary>
            public object Execute(Expander expander, object objectInstance, BuildPropertyGroup properties, ExpanderOptions options)
            {
                object functionResult = String.Empty;

                object[] args = null;

                try
                {
                    // If there is no object instance, then the method invocation will be a static
                    if (objectInstance == null)
                    {
                        // Check that the function that we're going to call is valid to call
                        if (!IsStaticMethodAvailable(ObjectType, name))
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(null, "InvalidFunctionMethodUnavailable", name, ObjectType.FullName);
                        }

                        bindingFlags |= BindingFlags.Static;

                        // For our intrinsic function we need to support calling of internal methods
                        // since we don't want them to be public
                        if (objectType == typeof(Microsoft.Build.BuildEngine.IntrinsicFunctions))
                        {
                            bindingFlags |= BindingFlags.NonPublic;
                        }
                    }
                    else
                    {
                        bindingFlags |= BindingFlags.Instance;
                    }

                    // We have a methodinfo match, need to plug in the arguments

                    args = new object[arguments.Length];

                    // Assemble our arguments ready for passing to our method
                    for (int n = 0; n < arguments.Length; n++)
                    {
                        object argument = expander.ExpandPropertiesLeaveTypedAndEscaped(this.arguments[n], null);
                        string argumentValue = argument as string;

                        if (argumentValue != null)
                        {
                            // remove our 'quotes' from the escaped string, leaving escaped quotes intact
                            args[n] = EscapingUtilities.UnescapeAll(argumentValue.Trim('`', '"', '\''));
                        }
                        else
                        {
                            args[n] = argument;
                        }
                    }

                    // Handle special cases where the object type needs to affect the choice of method
                    // The default binder and method invoke, often chooses the incorrect Equals and CompareTo and 
                    // fails the comparison, because what we have on the right is generally a string.
                    // This special casing is to realize that its a comparison that is taking place and handle the
                    // argument type coercion accordingly; effectively pre-preparing the argument type so 
                    // that it matches the left hand side ready for the default binder�s method invoke.
                    if (objectInstance != null && args.Length == 1 && (String.Equals("Equals", this.name, StringComparison.OrdinalIgnoreCase) || String.Equals("CompareTo", this.name, StringComparison.OrdinalIgnoreCase)))
                    {
                        // change the type of the final unescaped string into the destination
                        args[0] = Convert.ChangeType(args[0], objectInstance.GetType(), CultureInfo.InvariantCulture);
                    }
                    
                    // If we've been asked for and instance to be constructed, then we
                    // need to locate an appropriate constructor and invoke it
                    if (String.Equals("new", this.name, StringComparison.OrdinalIgnoreCase))
                    {
                        functionResult = LateBindExecute(null /* no previous exception */, BindingFlags.Public | BindingFlags.Instance, null /* no instance for a constructor */, args, true /* is constructor */);
                    }
                    else
                    {
                        // Execute the function given converted arguments
                        // The only exception that we should catch to try a late bind here is missing method
                        // otherwise there is the potential of running a function twice!
                        try
                        {
                            // First use InvokeMember using the standard binder - this will match and coerce as needed
                            functionResult = objectType.InvokeMember(this.name, bindingFlags, Type.DefaultBinder, objectInstance, args, CultureInfo.InvariantCulture);
                        }
                        catch (MissingMethodException ex) // Don't catch and retry on any other exception
                        {
                            // If we're invoking a method, then there are deeper attempts that
                            // can be made to invoke the method
                            if ((bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                            {
                                // The standard binder failed, so do our best to coerce types into the arguments for the function
                                // This may happen if the types need coercion, but it may also happen if the object represents a type that contains open type parameters, that is, ContainsGenericParameters returns true. 
                                functionResult = LateBindExecute(ex, bindingFlags, objectInstance, args, false /* is not constructor */);
                            }
                            else
                            {
                                // We were asked to get a property or field, and we found that we cannot
                                // locate it. Since there is no further argument coersion possible
                                // we'll throw right now.
                                throw;
                            }
                        }
                    }

                    // If the result of the function call is a string, then we need to escape the result
                    // so that we maintain the "engine contains escaped data" state.
                    // The exception is that the user is explicitly calling MSBuild::Unescape or MSBuild::Escape
                    if (functionResult is string && !String.Equals("Unescape", name, StringComparison.OrdinalIgnoreCase) && !String.Equals("Escape", name, StringComparison.OrdinalIgnoreCase))
                    {
                        functionResult = EscapingUtilities.Escape((string)functionResult);
                    }
                    
                    // There's nothing left to deal within the function expression, return the result from the execution
                    if (String.IsNullOrEmpty(remainder))
                    {
                        return functionResult;
                    }

                    // Recursively expand the remaining property body after execution
                    return expander.ExpandPropertyBody(remainder, functionResult, properties, options);
                }
                // Exceptions coming from the actual function called are wrapped in a TargetInvocationException
                catch (TargetInvocationException ex)
                {
                    // We ended up with something other than a function expression
                    string partiallyEvaluated = GenerateStringOfMethodExecuted(expression, objectInstance, name, args);
                    ProjectErrorUtilities.ThrowInvalidProject(null, "InvalidFunctionPropertyExpression", partiallyEvaluated, ex.InnerException.Message.Replace("\r\n", " "));
                    return null;
                }
                // Any other exception was thrown by trying to call it
                catch (Exception ex)
                {
                    if (ExceptionHandling.NotExpectedFunctionException(ex))
                    {
                        throw;
                    }

                    // We ended up with something other than a function expression
                    string partiallyEvaluated = GenerateStringOfMethodExecuted(expression, objectInstance, name, args);
                    ProjectErrorUtilities.ThrowInvalidProject(null, "InvalidFunctionPropertyExpression", partiallyEvaluated, ex.Message);
                    return null;
                }
            }

            /// <summary>
            /// Make an attempt to create a string showing what we were trying to execute when we failed.
            /// This will show any intermediate evaluation which may help the user figure out what happened.
            /// </summary>
            private string GenerateStringOfMethodExecuted(string expression, object objectInstance, string name, object[] args)
            {
                string parameters = String.Empty;
                if (args != null)
                {
                    foreach (object arg in args)
                    {
                        if (arg == null)
                        {
                            parameters += "null";
                        }
                        else
                        {
                            string argString = arg.ToString();
                            if (arg is string && argString.Length == 0)
                            {
                                parameters += "''";
                            }
                            else
                            {
                                parameters += arg.ToString();
                            }
                        }

                        parameters += ", ";
                    }

                    if (parameters.Length > 2)
                    {
                        parameters = parameters.Substring(0, parameters.Length - 2);
                    }
                }

                if (objectInstance == null)
                {
                    string typeName = objectType.FullName;

                    // We don't want to expose the real type name of our intrinsics
                    // so we'll replace it with "MSBuild"
                    if (objectType == typeof(Microsoft.Build.BuildEngine.IntrinsicFunctions))
                    {
                        typeName = "MSBuild";
                    }

                    if ((bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                    {
                        return "[" + typeName + "]::" + name + "(" + parameters + ")";
                    }
                    else
                    {
                        return "[" + typeName + "]::" + name;
                    }
                }
                else
                {
                    string propertyValue = "\"" + objectInstance as string + "\"";

                    if ((bindingFlags & BindingFlags.InvokeMethod) == BindingFlags.InvokeMethod)
                    {
                        return propertyValue + "." + name + "(" + parameters + ")";
                    }
                    else
                    {
                        return propertyValue + "." + name;
                    }
                }
            }

            /// <summary>
            /// Return a Type object for the type we're trying to call static methods on
            /// </summary>
            private static Type GetTypeForStaticMethod(string typeName)
            {
                // Ultimately this will be a more in-depth lookup, including assembly name etc.
                // for now, we're only supporting a subset of what's in mscorlib + specific additional types
                // If the env var MSBUILDENABLEALLPROPERTYFUNCTIONS=1 then we'll allow pretty much anything
                Type objectType;
                Tuple<string, Type> functionType;

                // For whole types we support them being in different assemblies than mscorlib
                // Get the assembly qualified type name if one exists
                if (FunctionConstants.AvailableStaticMethods.TryGetValue(typeName, out functionType) && functionType != null)
                {
                    // We need at least one of these set
                    ErrorUtilities.VerifyThrow(functionType.Item1 != null || functionType.Item2 != null, "Function type information needs either string or type represented.");

                    // If we have the type information in Type form, then just return that
                    if (functionType.Item2 != null)
                    {
                        return functionType.Item2;
                    }
                    else if (functionType.Item1 != null)
                    {
                        // This is a case where the Type is not available at compile time, so
                        // we are forced to bind by name instead
                        typeName = functionType.Item1;

                        // Get the type from the assembly qualified type name from AvailableStaticMethods
                        objectType = Type.GetType(typeName, false /* do not throw TypeLoadException if not found */, true /* ignore case */);

                        // If we've used it once, chances are that we'll be using it again
                        // We can record the type here since we know it's available for calling from the fact that is was in the AvailableStaticMethods table
                        FunctionConstants.AvailableStaticMethods[typeName] = new Tuple<string, Type>(typeName, objectType);

                        return objectType;
                    }
                }

                // Get the type from mscorlib (or the currently running assembly)
                objectType = Type.GetType(typeName, false /* do not throw TypeLoadException if not found */, true /* ignore case */);

                if (objectType != null)
                {
                    // DO NOT CACHE THE TYPE HERE!
                    // We don't add the resolved type here in the AvailableStaticMethods. This is because that table is used
                    // during function parse, but only later during execution do we check for the ability to call specific methods on specific types.
                    return objectType;
                }

                // Note the following code path is only entered when MSBUILDENABLEALLPROPERTYFUNCTIONS == 1
                if (Environment.GetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS") == "1")
                {
                    // We didn't find the type, so go probing. First in System
                    if (objectType == null)
                    {
                        objectType = GetTypeFromAssembly(typeName, "System");
                    }

                    // Next in System.Core
                    if (objectType == null)
                    {
                        objectType = GetTypeFromAssembly(typeName, "System.Core");
                    }

                    // We didn't find the type, so try to find it using the namespace
                    if (objectType == null)
                    {
                        objectType = GetTypeFromAssemblyUsingNamespace(typeName);
                    }

                    if (objectType != null)
                    {
                        // If we've used it once, chances are that we'll be using it again
                        // We can cache the type here, since all functions are enabled
                        FunctionConstants.AvailableStaticMethods[typeName] = new Tuple<string, Type>(typeName, objectType);
                    }
                }

                return objectType;
            }

            /// <summary>
            /// Gets the specified type using the namespace to guess the assembly that its in
            /// </summary>
            private static Type GetTypeFromAssemblyUsingNamespace(string typeName)
            {
                string baseName = typeName;
                int assemblyNameEnd = baseName.LastIndexOf('.');
                Type foundType = null;

                ErrorUtilities.VerifyThrow(assemblyNameEnd > 0, "Invalid typename: {0}", typeName);

                // We will work our way up the namespace looking for an assembly that matches
                while (assemblyNameEnd > 0)
                {
                    string candidateAssemblyName = null;

                    candidateAssemblyName = baseName.Substring(0, assemblyNameEnd);

                    // Try to load the assembly with the computed name
                    foundType = GetTypeFromAssembly(typeName, candidateAssemblyName);

                    if (foundType != null)
                    {
                        // We have a match, so get the type from that assembly
                        return foundType;
                    }
                    else
                    {
                        // Keep looking as we haven't found a match yet
                        baseName = candidateAssemblyName;
                        assemblyNameEnd = baseName.LastIndexOf('.');
                    }
                }

                // We didn't find it, so we need to give up
                return null;
            }


            /// <summary>
            /// Get the specified type from the assembly partial name supplied
            /// </summary>
            private static Type GetTypeFromAssembly(string typeName, string candidateAssemblyName)
            {
                Type objectType = null;

                // Try to load the assembly with the computed name
#pragma warning disable 618
                // Unfortunately Assembly.Load is not an alternative to LoadWithPartialName, since
                // Assembly.Load requires the full assembly name to be passed to it.
                // Therefore we must ignore the deprecated warning.
                Assembly candidateAssembly = Assembly.LoadWithPartialName(candidateAssemblyName);
#pragma warning restore 618

                if (candidateAssembly != null)
                {
                    objectType = candidateAssembly.GetType(typeName, false /* do not throw TypeLoadException if not found */, true /* ignore case */);
                }

                return objectType;
            }
            
            /// <summary>
            /// Factory method to construct a function for property evaluation
            /// </summary>
            private static Function ConstructFunction(string expressionFunction, string expressionRootName, Type objectType, int argumentStartIndex, int methodStartIndex)
            {
                // The unevaluated and unexpanded arguments for this function
                string[] functionArguments;

                // The name of the function that will be invoked
                string functionToInvoke;

                // What's left of the expression once the function has been constructed
                string remainder = String.Empty;

                // The binding flags that we will use for this function's execution
                BindingFlags defaultBindingFlags = BindingFlags.IgnoreCase | BindingFlags.Public;

                // There are arguments that need to be passed to the function
                if (argumentStartIndex > -1 && !expressionFunction.Substring(methodStartIndex, argumentStartIndex - methodStartIndex).Contains("."))
                {
                    string argumentsContent;

                    // separate the function and the arguments
                    functionToInvoke = expressionFunction.Substring(methodStartIndex, argumentStartIndex - methodStartIndex).Trim();

                    // Skip the '('
                    argumentStartIndex++;

                    // Scan for the matching closing bracket, skipping any nested ones
                    int argumentsEndIndex = ScanForClosingParenthesis(expressionFunction, argumentStartIndex);

                    // We should never end up in this situation, since the brackets will have been
                    // validated at the very outermost level (in the initial property parse). However if we
                    // end up with unmatched brackets here for some reason.. it's an error!
                    ErrorUtilities.VerifyThrow(argumentsEndIndex != -1, "Unmatched braces when constructing function.", "$(" + expressionFunction + ")");

                    // We have been asked for a method invocation
                    defaultBindingFlags |= BindingFlags.InvokeMethod;

                    // It may be that there are '()' but no actual arguments content
                    if (argumentStartIndex == expressionFunction.Length - 1)
                    {
                        argumentsContent = String.Empty;
                        functionArguments = new string[0];
                    }
                    else
                    {
                        // we have content within the '()' so let's extract and deal with it
                        argumentsContent = expressionFunction.Substring(argumentStartIndex, argumentsEndIndex - argumentStartIndex);

                        // If there are no arguments, then just create an empty array
                        if (String.IsNullOrEmpty(argumentsContent))
                        {
                            functionArguments = new string[0];
                        }
                        else
                        {
                            // We will keep empty entries so that we can treat them as null
                            functionArguments = ExtractFunctionArguments(expressionFunction, argumentsContent);
                        }

                        remainder = expressionFunction.Substring(argumentsEndIndex + 1);
                    }
                }
                else
                {
                    int nextMethodIndex = expressionFunction.IndexOf('.', methodStartIndex);
                    int methodLength = expressionFunction.Length - methodStartIndex;

                    functionArguments = new string[0];

                    if (nextMethodIndex > 0)
                    {
                        methodLength = nextMethodIndex - methodStartIndex;
                        remainder = expressionFunction.Substring(nextMethodIndex);
                    }

                    string netPropertyName = expressionFunction.Substring(methodStartIndex, methodLength).Trim();

                    ProjectErrorUtilities.VerifyThrowInvalidProject(netPropertyName.Length > 0, null, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);

                    // We have been asked for a property or a field
                    defaultBindingFlags |= (BindingFlags.GetProperty | BindingFlags.GetField);

                    functionToInvoke = netPropertyName;
                }

                // either there are no functions left or what we have is another function
                if (String.IsNullOrEmpty(remainder) || remainder[0] == '.')
                {
                    // Construct a FunctionInfo will all the content that we just gathered
                    return new Function(objectType, expressionFunction, expressionRootName, functionToInvoke, functionArguments, defaultBindingFlags, remainder);
                }
                else
                {
                    // We ended up with something other than a function expression
                    ProjectErrorUtilities.ThrowInvalidProject(null, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);
                    return null;
                }
            }

            /// <summary>
            /// Extract the first level of arguments from the content.
            /// Splits the content passed in at commas.
            /// Returns an array of unexpanded arguments.
            /// If there are no arguments, returns an empty array.
            /// </summary>
            private static string[] ExtractFunctionArguments(string expressionFunction, string argumentsContent)
            {
                List<string> arguments = new List<string>();
                StringBuilder argumentBuilder = new StringBuilder(argumentsContent.Length); ;

                // Iterate over the contents of the arguments extracting the
                // the individual arguments as we go
                for (int n = 0; n < argumentsContent.Length; n++)
                {
                    // We found a property expression.. skip over all of it.
                    if ((n < argumentsContent.Length - 1) && (argumentsContent[n] == '$' && argumentsContent[n + 1] == '('))
                    {
                        int nestedPropertyStart = n;
                        n += 2; // skip over the opening '$('

                        // Scan for the matching closing bracket, skipping any nested ones
                        n = ScanForClosingParenthesis(argumentsContent, n);

                        ProjectErrorUtilities.VerifyThrowInvalidProject(n != 0, null, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);

                        argumentBuilder.Append(argumentsContent.Substring(nestedPropertyStart, (n - nestedPropertyStart) + 1));
                    }
                    else if (argumentsContent[n] == '`' || argumentsContent[n] == '"' || argumentsContent[n] == '\'')
                    {
                        int quoteStart = n;
                        n += 1; // skip over the opening quote

                        n = ScanForClosingQuote(argumentsContent[quoteStart], argumentsContent, n);

                        ProjectErrorUtilities.VerifyThrowInvalidProject(n != 0, null, "InvalidFunctionPropertyExpression", expressionFunction, String.Empty);

                        argumentBuilder.Append(argumentsContent.Substring(quoteStart, (n - quoteStart) + 1));
                    }
                    else if (argumentsContent[n] == ',')
                    {
                        // We have reached the end of the current argument, go ahead and add it
                        // to our list
                        AddArgument(arguments, argumentBuilder);
                        // Create a new argument builder ready for the next argument
                        argumentBuilder = new StringBuilder(argumentsContent.Length);
                    }
                    else
                    {
                        argumentBuilder.Append(argumentsContent[n]);
                    }
                }

                // This will either be the one and only argument, or the last one
                // so add it to our list
                AddArgument(arguments, argumentBuilder);

                return arguments.ToArray();
            }

            /// <summary>
            /// Skip all characters until we find the matching quote character
            /// </summary>
            private static int ScanForClosingQuote(char quoteChar, string expression, int index)
            {
                // Scan for our closing quoteChar
                while (index < expression.Length)
                {
                    if (expression[index] == quoteChar)
                    {
                        return index;
                    }
                    index++;
                }

                return -1;
            }

            /// <summary>
            /// Add the argument in the StringBuilder to the arguments list, handling nulls
            /// appropriately
            /// </summary>
            private static void AddArgument(List<string> arguments, StringBuilder argumentBuilder)
            {
                // If we don't have something that can be treated as an argument
                // then we should treat it as a null so that passing nulls
                // becomes possible through an empty argument between commas.
                ErrorUtilities.VerifyThrowArgumentNull(argumentBuilder, "argumentBuilder");
                // we reached the end of an argument, add the builder's final result
                // to our arguments. 
                string argValue = argumentBuilder.ToString().Trim();
                // We support passing of null through the argument constant value null
                if (String.Compare("null", argValue, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    arguments.Add(null);
                }
                else
                {
                    arguments.Add(argValue);
                }
            }

            /// <summary>
            /// Coerce the arguments according to the parameter types
            /// Will only return null if the coercion didn't work due to an InvalidCastException
            /// </summary>
            private static object[] CoerceArguments(object[] args, ParameterInfo[] parameters)
            {
                object[] coercedArguments = new object[args.Length];

                try
                {
                    // Do our best to coerce types into the arguments for the function
                    for (int n = 0; n < parameters.Length; n++)
                    {
                        if (args[n] == null)
                        {
                            // We can't coerce (object)null -- that's as general
                            // as it can get!
                            continue;
                        }

                        // Here we have special case conversions on a type basis
                        if (parameters[n].ParameterType == typeof(char[]))
                        {
                            coercedArguments[n] = args[n].ToString().ToCharArray();
                        }
                        else if (parameters[n].ParameterType.IsEnum && args[n] is string && ((string)args[n]).Contains("."))
                        {
                            Type enumType = parameters[n].ParameterType;
                            string typeLeafName = enumType.Name + ".";
                            string typeFullName = enumType.FullName + ".";

                            // Enum.parse expects commas between enum components
                            // We'll support the C# type | syntax too
                            // We'll also allow the user to specify the leaf or full type name on the enum
                            string argument = args[n].ToString().Replace('|', ',').Replace(typeFullName, "").Replace(typeLeafName, "");

                            // Parse the string representation of the argument into the destination enum                                
                            coercedArguments[n] = Enum.Parse(enumType, argument);
                        }
                        else
                        {
                            // change the type of the final unescaped string into the destination
                            coercedArguments[n] = Convert.ChangeType(args[n], parameters[n].ParameterType, CultureInfo.InvariantCulture);
                        }
                    }
                }
                catch (InvalidCastException)
                {
                    // The coercion failed therefore we return null
                    return null;
                }

                return coercedArguments;
            }

            /// <summary>
            /// For this initial implementation of inline functions, only very specific static methods on specific types are
            /// available
            /// </summary>
            private bool IsStaticMethodAvailable(Type objectType, string methodName)
            {
                if (objectType == typeof(Microsoft.Build.BuildEngine.IntrinsicFunctions))
                {
                    // These are our intrinsic functions, so we're OK with those
                    return true;
                }
                else
                {
                    string typeMethod = objectType.FullName + "::" + methodName;

                    if (FunctionConstants.AvailableStaticMethods.ContainsKey(objectType.FullName))
                    {
                        // Check our set for the type name
                        // This enables all statics on the given type
                        return true;
                    }
                    else if (FunctionConstants.AvailableStaticMethods.ContainsKey(typeMethod))
                    {
                        // Check for specific methods on types
                        return true;
                    }
                    else if (Environment.GetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS") == "1")
                    {
                        // If MSBUILDENABLEALLPROPERTYFUNCTION == 1, then anything goes
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Construct and instance of objectType based on the constructor or method arguments provided.
            /// Arguments must never be null.
            /// </summary>
            private object LateBindExecute(Exception ex, BindingFlags bindingFlags, object objectInstance /* null unless instance method */, object[] args, bool isConstructor)
            {
                ParameterInfo[] parameters = null;
                MethodBase[] members = null;
                MethodBase memberInfo = null;

                // First let's try for a method where all arguments are strings..
                Type[] types = new Type[arguments.Length];
                for (int n = 0; n < arguments.Length; n++)
                {
                    types[n] = typeof(string);
                }

                if (isConstructor)
                {
                    memberInfo = objectType.GetConstructor(bindingFlags, null, types, null);
                }
                else
                {
                    memberInfo = objectType.GetMethod(this.name, bindingFlags, null, types, null);
                }

                // If we didn't get a match on all string arguments,
                // search for a method with the right number of arguments
                if (memberInfo == null)
                {
                    // Gather all methods that may match
                    if (isConstructor)
                    {
                        members = objectType.GetConstructors(bindingFlags);
                    }
                    else
                    {
                        members = objectType.GetMethods(bindingFlags);
                    }

                    // Try to find a method with the right name, number of arguments and
                    // compatible argument types
                    object[] coercedArguments = null;
                    foreach (MethodBase member in members)
                    {
                        parameters = member.GetParameters();

                        // Simple match on name and number of params, we will be case insensitive
                        if (parameters.Length == this.arguments.Length)
                        {
                            if (isConstructor || String.Equals(member.Name, this.name, StringComparison.OrdinalIgnoreCase))
                            {
                                // we have a match on the name and argument number
                                // now let's try to coerce the arguments we have
                                // into the arguments on the matching method
                                coercedArguments = CoerceArguments(args, parameters);

                                if (coercedArguments != null)
                                {
                                    // We have a complete match
                                    memberInfo = member;
                                    args = coercedArguments;
                                    break;
                                }
                            }
                        }
                    }
                }

                object functionResult = null;

                // We have a match and coerced arguments, let's construct..
                if (memberInfo != null && args != null)
                {
                    if (isConstructor)
                    {
                        functionResult = ((ConstructorInfo)memberInfo).Invoke(args);
                    }
                    else
                    {
                        functionResult = ((MethodInfo)memberInfo).Invoke(objectInstance /* null if static method */, args);
                    }
                }
                else if (!isConstructor)
                {
                    throw ex;
                }

                if (functionResult == null && isConstructor)
                {
                    throw new TargetInvocationException(new MissingMethodException());
                }

                return functionResult;
            }
        }

        /// <summary>
        /// Expands all embedded item metadata in the given string, using the bucketed items.
        /// 
        /// This method leaves the expression escaped.  Callers may need to unescape on their own as appropriate.
        /// </summary>
        /// <remarks>
        /// This method is marked internal only for unit-testing purposes. Ideally
        /// it should be private.
        /// </remarks>
        /// <owner>SumedhK</owner>
        /// <param name="expression"></param>
        /// <returns>the expanded string</returns>
        internal string ExpandMetadataLeaveEscaped
            (
            string expression
            )
        {
            if ((options & ExpanderOptions.ExpandMetadata) != ExpanderOptions.ExpandMetadata)
            {
                return expression;
            }

            string result;

            // PERF NOTE: Regex matching is expensive, so if the string doesn't contain any item metadata references, just bail
            // out -- pre-scanning the string is actually cheaper than running the Regex, even when there are no matches!
            if (expression.IndexOf(ItemExpander.itemMetadataPrefix, StringComparison.Ordinal) == -1)
            {
                result = expression;
            }
            // if there are no item vectors in the string
            else if (expression.IndexOf(ItemExpander.itemVectorPrefix, StringComparison.Ordinal) == -1)
            {
                // run a simpler Regex to find item metadata references
                result = ItemExpander.itemMetadataPattern.Replace(expression, new MatchEvaluator(ExpandSingleMetadata));
            }
            // PERF NOTE: this is a highly targeted optimization for a common pattern observed during profiling
            // if the string is a list of item vectors with no separator specifications
            else if (ItemExpander.listOfItemVectorsWithoutSeparatorsPattern.IsMatch(expression))
            {
                // then even if the string contains item metadata references, those references will only be inside transform
                // expressions, and can be safely skipped
                result = expression;
            }
            else
            {
                // otherwise, run the more complex Regex to find item metadata references not contained in transforms
                result = ItemExpander.nonTransformItemMetadataPattern.Replace(expression, new MatchEvaluator(ExpandSingleMetadata));
            }

            return result;
        }

        /// <summary>
        /// Expands a single item metadata.
        /// </summary>
        /// <remarks>This method is a callback for Regex.Replace().</remarks>
        /// <owner>SumedhK</owner>
        /// <param name="itemMetadataMatch"></param>
        /// <returns>the expanded item metadata</returns>
        private string ExpandSingleMetadata(Match itemMetadataMatch)
        {
            ErrorUtilities.VerifyThrow(itemMetadataMatch.Success, "Need a valid item metadata.");

            string metadataName = itemMetadataMatch.Groups["NAME"].Value;
            string itemType = null;

            // check if the metadata is qualified with the item type
            if (itemMetadataMatch.Groups["ITEM_SPECIFICATION"].Length > 0)
            {
                itemType = itemMetadataMatch.Groups["TYPE"].Value;
            }

            // look up the metadata - we may not have a value for it
            string metadataValue = null;

            metadataValue = GetValueFromMetadataTable(itemType, metadataName, metadataValue);

            if (metadataValue == null)
            {
                metadataValue = GetDefaultMetadataValue(itemType, metadataName, metadataValue);
            }
            
            return metadataValue ?? String.Empty;
        }

        /// <summary>
        /// Retrieves any value we have in our metadata table for the metadata name specified.
        /// If no value is available, returns null.
        /// </summary>
        private string GetValueFromMetadataTable(string itemType, string metadataName, string metadataValue)
        {
            if (itemMetadata == null) return null;

            if (implicitMetadataItemType == null)
            {
                // metadata table has some qualified keys; if the expression is
                // qualified, look it up with a qualified key
                string key;
                if (itemType == null)
                {
                    key = metadataName;
                }
                else
                {
                    key = itemType + "." + metadataName;
                }

                itemMetadata.TryGetValue(key, out metadataValue);
            }
            else
            {
                // metadata table has all unqualified keys.
                // if we found a qualified metadata, it must match the type
                // of all the metadata in our table of unqualified metadata
                if (itemType == null || String.Equals(itemType, implicitMetadataItemType, StringComparison.OrdinalIgnoreCase))
                {
                    itemMetadata.TryGetValue(metadataName, out metadataValue);
                }
            }

            return metadataValue;
        }

        /// <summary>
        /// Retrieves any value we have for the specified metadata in any table of default metadata we've been assigned.
        /// If no value is available, returns null.
        /// </summary>
        private string GetDefaultMetadataValue(string itemType, string metadataName, string metadataValue)
        {
            if (specificItemDefinitionLibrary == null) return null;

            if (itemType == null || String.Equals(itemType, specificItemDefinitionLibrary.ItemType, StringComparison.OrdinalIgnoreCase))
            {
                metadataValue = specificItemDefinitionLibrary.GetDefaultMetadataValue(metadataName);
            }

            return metadataValue;
        }

        /// <summary>
        /// Takes the specified string and expands all item vectors embedded in it. The expansion is done in 2 passes:
        /// 1) the first pass expands only the item vectors that refer to the bucketed items
        /// 2) the second pass expands out the remaining item vectors using the project items
        ///
        /// This method leaves the expression escaped.  Callers may need to unescape on their own as appropriate.
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="expressionNode">The XML attribute containing the string we're trying to expand here.  Solely
        /// for the purposes of providing line/column number information when there's an error.</param>
        /// <returns>expanded string</returns>
        /// <owner>SumedhK</owner>
        private string ExpandItemsIntoStringLeaveEscaped
            (
            string expression,
            XmlNode expressionNode
            )
        {
            // An empty string always expands to empty
            if (String.IsNullOrEmpty(expression) || lookup == null)
            {
                return expression;
            }

            if ((options & ExpanderOptions.ExpandItems) != ExpanderOptions.ExpandItems)
            {
                return expression;
            }

            return ItemExpander.ExpandEmbeddedItemVectors(expression, expressionNode, lookup);
        }

        /// <summary>
        /// Attempts to extract the contents of the given item vector. Returns a virtual BuildItemGroup.
        /// This method leaves all the items escaped.  Caller may need to unescape them himself if appropriate.
        /// </summary>
        /// <param name="singleItemVectorExpression"></param>
        /// <param name="itemVectorAttribute">The XML attribute that contains the thing we're expanding here.  (Only needed
        /// for the purpose of logging good error messages with line/column information.</param>
        /// <returns>a virtual BuildItemGroup containing the items resulting from the expression, or null if the expression was invalid.</returns>
        /// <owner>SumedhK;RGoel</owner>
        internal BuildItemGroup ExpandSingleItemListExpressionIntoItemsLeaveEscaped
        (
            string singleItemVectorExpression,
            XmlAttribute itemVectorAttribute
        )
        {
            if (lookup == null)
            {
                return null;
            }

            Match throwAwayMatch;
            return this.ExpandSingleItemListExpressionIntoItemsLeaveEscaped(singleItemVectorExpression, itemVectorAttribute, out throwAwayMatch);
        }

        /// <summary>
        /// Attempts to extract the contents of the given item vector.
        /// </summary>
        /// <param name="singleItemVectorExpression"></param>
        /// <param name="itemVectorAttribute">The XML attribute that contains the thing we're expanding here.  (Only needed
        /// for the purpose of logging good error messages with line/column information.</param>
        /// <param name="itemVectorMatch"></param>
        /// <returns>a virtual BuildItemGroup containing the items resulting from the expression, or null if the expression was invalid.</returns>
        /// <owner>SumedhK;RGoel</owner>
        internal BuildItemGroup ExpandSingleItemListExpressionIntoItemsLeaveEscaped
        (
            string singleItemVectorExpression,
            XmlAttribute itemVectorAttribute,
            out Match itemVectorMatch
        )
        {
            ErrorUtilities.VerifyThrow(lookup != null, "Need items");

            if ((options & ExpanderOptions.ExpandItems) != ExpanderOptions.ExpandItems)
            {
                itemVectorMatch = null;
                return null;
            }

            return ItemExpander.ItemizeItemVector(singleItemVectorExpression, itemVectorAttribute, lookup, out itemVectorMatch);
        }
    }

    /// <summary>
    /// Indicates to an expander what exactly it should expand
    /// </summary>
    [Flags]
    internal enum ExpanderOptions
    {
        Invalid = 0x0,
        ExpandProperties = 0x1,
        ExpandItems = 0x2,
        ExpandPropertiesAndItems = ExpandProperties | ExpandItems,
        ExpandMetadata = 0x4,
        ExpandPropertiesAndMetadata = ExpandProperties | ExpandMetadata,
        ExpandAll = ExpandPropertiesAndItems | ExpandMetadata
    };
}


