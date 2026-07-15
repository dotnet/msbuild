// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
#if !FEATURE_MSIOREDIST
using System.IO;
#endif
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.NET.StringTools;

#if FEATURE_MSIOREDIST
// File is intentionally NOT aliased — all typeof() comparisons use fully-qualified
// System.IO.File to match the types registered in AvailableStaticMethods.
using Directory = Microsoft.IO.Directory;
using Path = Microsoft.IO.Path;
#endif

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
{
    private static partial class ItemExpander
    {
        /// <summary>
        /// The set of functions that called during an item transformation, e.g. @(CLCompile->ContainsMetadata('MetaName', 'metaValue')).
        /// </summary>
        private static partial class Transforms
        {
            /// <summary>
            /// The number of characters added by a quoted expression.
            /// 3 characters for
            ///  </summary>
            private const int QuotedExpressionSurroundCharCount = 3;

            /// <summary>
            /// A precomputed lookup of item spec modifiers wrapped in regex strings.
            /// This allows us to completely skip of Regex parsing when the inner string matches a known modifier.
            /// IsDerivableItemSpecModifier doesn't currently support Span lookups, so we have to manually map these.
            /// </summary>
            private static readonly FrozenDictionary<string, string> s_itemSpecModifiers = new Dictionary<string, string>()
            {
                [$"%({ItemSpecModifiers.FullPath})"] = ItemSpecModifiers.FullPath,
                [$"%({ItemSpecModifiers.RootDir})"] = ItemSpecModifiers.RootDir,
                [$"%({ItemSpecModifiers.Filename})"] = ItemSpecModifiers.Filename,
                [$"%({ItemSpecModifiers.Extension})"] = ItemSpecModifiers.Extension,
                [$"%({ItemSpecModifiers.RelativeDir})"] = ItemSpecModifiers.RelativeDir,
                [$"%({ItemSpecModifiers.Directory})"] = ItemSpecModifiers.Directory,
                [$"%({ItemSpecModifiers.RecursiveDir})"] = ItemSpecModifiers.RecursiveDir,
                [$"%({ItemSpecModifiers.Identity})"] = ItemSpecModifiers.Identity,
                [$"%({ItemSpecModifiers.ModifiedTime})"] = ItemSpecModifiers.ModifiedTime,
                [$"%({ItemSpecModifiers.CreatedTime})"] = ItemSpecModifiers.CreatedTime,
                [$"%({ItemSpecModifiers.AccessedTime})"] = ItemSpecModifiers.AccessedTime,
                [$"%({ItemSpecModifiers.DefiningProjectFullPath})"] = ItemSpecModifiers.DefiningProjectFullPath,
                [$"%({ItemSpecModifiers.DefiningProjectDirectory})"] = ItemSpecModifiers.DefiningProjectDirectory,
                [$"%({ItemSpecModifiers.DefiningProjectName})"] = ItemSpecModifiers.DefiningProjectName,
                [$"%({ItemSpecModifiers.DefiningProjectExtension})"] = ItemSpecModifiers.DefiningProjectExtension,
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// A thread-static string builder for use in ExpandQuotedExpressionFunction.
            /// In theory we should be able to use shared instance, but in a profile it appears something higher in
            /// the call-stack is already borrowing the instance, so it ends up always allocating.
            /// This should not be used outside of ExpandQuotedExpressionFunction unless validated to not conflict.
            /// </summary>
            [ThreadStatic]
            private static SpanBasedStringBuilder s_includeBuilder;

            /// <summary>
            /// A reference to the last extracted expression function to save on Regex-related allocations.
            /// In many cases, the expression is exactly the same as the previous.
            /// </summary>
            private static string s_lastParsedQuotedExpression;

            /// <summary>
            /// Intrinsic function that adds the number of items in the list.
            /// </summary>
            internal static void Count(List<TransformEntry> input, List<TransformEntry> output)
                => output.Add(new TransformEntry(input.Count.ToString(CultureInfo.InvariantCulture), item: null));

            /// <summary>
            /// Intrinsic function that adds the specified built-in modifer value of the items in input
            /// Each entry pairs the current item include with the item under transformation.
            /// </summary>
            internal static void ItemSpecModifierFunction(
                List<TransformEntry> input,
                List<TransformEntry> output,
                string[] arguments,
                bool includeNullEntries,
                string functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                foreach (TransformEntry item in input)
                {
                    // If the item include has become empty,
                    // this is the end of the pipeline for this item
                    if (String.IsNullOrEmpty(item.Value))
                    {
                        continue;
                    }

                    string result = null;

                    try
                    {
                        // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                        // In that case,
                        // 1. in multiprocess mode we're safe to get the current directory as we'll be running on TaskItems which
                        // only exist within a target where we can trust the current directory
                        // 2. in single process mode we get the project directory set for the thread
                        string directoryToUse = item.Item.ProjectDirectory ?? FileUtilities.CurrentThreadWorkingDirectory ?? Directory.GetCurrentDirectory();
                        string definingProjectEscaped = item.Item.GetMetadataValueEscaped(ItemSpecModifiers.DefiningProjectFullPath);

                        result = ItemSpecModifiers.GetItemSpecModifier(item.Value, functionName, directoryToUse, definingProjectEscaped);
                    }
                    // InvalidOperationException is how GetItemSpecModifier communicates invalid conditions upwards, so
                    // we do not want to rethrow in that case.
                    catch (Exception e) when (!ExceptionHandling.NotExpectedException(e) || e is InvalidOperationException)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidItemFunctionExpression", functionName, item.Value, e.Message);
                    }

                    if (!String.IsNullOrEmpty(result))
                    {
                        // GetItemSpecModifier will have returned us an escaped string
                        // there is nothing more to do than yield it into the pipeline
                        output.Add(new TransformEntry(result, item.Item));
                    }
                    else if (includeNullEntries)
                    {
                        output.Add(new TransformEntry(null, item.Item));
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds the subset of items that actually exist on disk.
            /// </summary>
            internal static void Exists(
                List<TransformEntry> input,
                List<TransformEntry> output,
                string[] arguments,
                string functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                foreach (TransformEntry item in input)
                {
                    if (String.IsNullOrEmpty(item.Value))
                    {
                        continue;
                    }

                    // Unescape as we are passing to the file system
                    string unescapedPath = EscapingUtilities.UnescapeAll(item.Value);

                    string rootedPath = null;
                    try
                    {
                        // If we're a projectitem instance then we need to get
                        // the project directory and be relative to that
                        if (Path.IsPathRooted(unescapedPath))
                        {
                            rootedPath = unescapedPath;
                        }
                        else
                        {
                            // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                            // In that case,
                            // 1. in multiprocess mode we're safe to get the current directory as we'll be running on TaskItems which
                            // only exist within a target where we can trust the current directory
                            // 2. in single process mode we get the project directory set for the thread
                            string baseDirectoryToUse = item.Item.ProjectDirectory ?? FileUtilities.CurrentThreadWorkingDirectory ?? String.Empty;
                            rootedPath = Path.Combine(baseDirectoryToUse, unescapedPath);
                        }
                    }
                    catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidItemFunctionExpression", functionName, item.Value, e.Message);
                    }

                    if (FileSystems.Default.FileOrDirectoryExists(rootedPath))
                    {
                        output.Add(item);
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that combines the existing paths of the input items with a given relative path.
            /// </summary>
            internal static void Combine(
                List<TransformEntry> input,
                List<TransformEntry> output,
                string[] arguments,
                string functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                string relativePath = arguments[0];

                foreach (TransformEntry item in input)
                {
                    if (String.IsNullOrEmpty(item.Value))
                    {
                        continue;
                    }

                    // Unescape as we are passing to the file system
                    string unescapedPath = EscapingUtilities.UnescapeAll(item.Value);
                    string combinedPath = Path.Combine(unescapedPath, relativePath);
                    string escapedPath = EscapingUtilities.Escape(combinedPath);
                    output.Add(new TransformEntry(escapedPath, null));
                }
            }

            /// <summary>
            /// Intrinsic function that adds all ancestor directories of the given items.
            /// </summary>
            internal static void GetPathsOfAllDirectoriesAbove(
                List<TransformEntry> input,
                List<TransformEntry> output,
                string[] arguments,
                string functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                // Phase 1: find all the applicable directories.

                SortedSet<string> directories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (TransformEntry item in input)
                {
                    if (String.IsNullOrEmpty(item.Value))
                    {
                        continue;
                    }

                    string directoryName = null;

                    // Unescape as we are passing to the file system
                    string unescapedPath = EscapingUtilities.UnescapeAll(item.Value);

                    try
                    {
                        string rootedPath;

                        // If we're a projectitem instance then we need to get
                        // the project directory and be relative to that
                        if (Path.IsPathRooted(unescapedPath))
                        {
                            rootedPath = unescapedPath;
                        }
                        else
                        {
                            // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                            // In that case,
                            // 1. in multiprocess mode we're safe to get the current directory as we'll be running on TaskItems which
                            // only exist within a target where we can trust the current directory
                            // 2. in single process mode we get the project directory set for the thread
                            string baseDirectoryToUse = item.Item.ProjectDirectory ?? FileUtilities.CurrentThreadWorkingDirectory ?? String.Empty;
                            rootedPath = Path.Combine(baseDirectoryToUse, unescapedPath);
                        }

                        // Normalize the path to remove elements like "..".
                        // Otherwise we run the risk of returning two or more different paths that represent the
                        // same directory.
                        rootedPath = FileUtilities.NormalizePath(rootedPath);
                        directoryName = Path.GetDirectoryName(rootedPath);
                    }
                    catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidItemFunctionExpression", functionName, item.Value, e.Message);
                    }

                    while (!String.IsNullOrEmpty(directoryName))
                    {
                        if (directories.Contains(directoryName))
                        {
                            // We've already got this directory (and all its ancestors) in the set.
                            break;
                        }

                        directories.Add(directoryName);
                        directoryName = Path.GetDirectoryName(directoryName);
                    }
                }

                // Phase 2: Go through the directories and return them in order

                foreach (string directoryPath in directories)
                {
                    string escapedDirectoryPath = EscapingUtilities.Escape(directoryPath);
                    output.Add(new TransformEntry(escapedDirectoryPath, null));
                }
            }

            /// <summary>
            /// Intrinsic function that adds the DirectoryName of the items in input
            /// UNDONE: This can be removed in favor of a built-in %(DirectoryName) metadata in future.
            /// </summary>
            internal static void DirectoryName(
                List<TransformEntry> input,
                List<TransformEntry> output,
                string[] arguments,
                bool includeNullEntries,
                string functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                Dictionary<string, string> directoryNameTable = new Dictionary<string, string>(input.Count, StringComparer.OrdinalIgnoreCase);

                foreach (TransformEntry item in input)
                {
                    // If the item include has become empty,
                    // this is the end of the pipeline for this item
                    if (String.IsNullOrEmpty(item.Value))
                    {
                        continue;
                    }

                    string directoryName;
                    if (!directoryNameTable.TryGetValue(item.Value, out directoryName))
                    {
                        // Unescape as we are passing to the file system
                        string unescapedPath = EscapingUtilities.UnescapeAll(item.Value);

                        try
                        {
                            string rootedPath;

                            // If we're a projectitem instance then we need to get
                            // the project directory and be relative to that
                            if (Path.IsPathRooted(unescapedPath))
                            {
                                rootedPath = unescapedPath;
                            }
                            else
                            {
                                // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                                // In that case,
                                // 1. in multiprocess mode we're safe to get the current directory as we'll be running on TaskItems which
                                // only exist within a target where we can trust the current directory
                                // 2. in single process mode we get the project directory set for the thread
                                string baseDirectoryToUse = item.Item.ProjectDirectory ?? FileUtilities.CurrentThreadWorkingDirectory ?? String.Empty;
                                rootedPath = Path.Combine(baseDirectoryToUse, unescapedPath);
                            }

                            directoryName = Path.GetDirectoryName(rootedPath);
                        }
                        catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidItemFunctionExpression", functionName, item.Value, e.Message);
                        }

                        // Escape as this is going back into the engine
                        directoryName = EscapingUtilities.Escape(directoryName);
                        directoryNameTable[unescapedPath] = directoryName;
                    }

                    if (!String.IsNullOrEmpty(directoryName))
                    {
                        // return a result through the enumerator
                        output.Add(new TransformEntry(directoryName, item.Item));
                    }
                    else if (includeNullEntries)
                    {
                        output.Add(new TransformEntry(null, item.Item));
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds the contents of the metadata in specified in argument[0].
            /// </summary>
            internal static void Metadata(
                List<TransformEntry> input,
                List<TransformEntry> output,
                string[] arguments,
                bool includeNullEntries,
                string functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                string metadataName = arguments[0];

                foreach (TransformEntry item in input)
                {
                    if (item.Item != null)
                    {
                        string metadataValue = null;

                        try
                        {
                            metadataValue = item.Item.GetMetadataValueEscaped(metadataName);
                        }
                        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                        {
                            // Blank metadata name
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                        }

                        if (!String.IsNullOrEmpty(metadataValue))
                        {
                            // It may be that the itemspec has unescaped ';'s in it so we need to split here to handle
                            // that case.
                            if (metadataValue.Contains(';'))
                            {
                                var splits = ExpressionShredder.SplitSemiColonSeparatedList(metadataValue);

                                foreach (string itemSpec in splits)
                                {
                                    // return a result through the enumerator
                                    output.Add(new TransformEntry(itemSpec, item.Item));
                                }
                            }
                            else
                            {
                                // return a result through the enumerator
                                output.Add(new TransformEntry(metadataValue, item.Item));
                            }
                        }
                        else if (metadataValue != String.Empty && includeNullEntries)
                        {
                            output.Add(new TransformEntry(metadataValue, item.Item));
                        }
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds only the items from input that have a distinct Include
            /// Using a case sensitive comparison.
            /// </summary>
            internal static void DistinctWithCase(
                List<TransformEntry> input,
                List<TransformEntry> output,
                string[] arguments,
                string functionName,
                IElementLocation elementLocation)
                => DistinctWithComparer(input, output, arguments, StringComparer.Ordinal, functionName, elementLocation);

            /// <summary>
            /// Intrinsic function that adds only the items from input that have a distinct Include
            /// Using a case insensitive comparison.
            /// </summary>
            internal static void Distinct(
                List<TransformEntry> input,
                List<TransformEntry> output,
                string[] arguments,
                string functionName,
                IElementLocation elementLocation)
                => DistinctWithComparer(input, output, arguments, StringComparer.OrdinalIgnoreCase, functionName, elementLocation);

            /// <summary>
            /// Intrinsic function that adds only the items from input that have a distinct Include
            /// using the specified comparer.
            /// </summary>
            private static void DistinctWithComparer(
                List<TransformEntry> input,
                List<TransformEntry> output,
                string[] arguments,
                StringComparer comparer,
                string functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                // This dictionary will ensure that we only return one result per unique itemspec
                HashSet<string> seenItems = new HashSet<string>(input.Count, comparer);

                foreach (TransformEntry item in input)
                {
                    if (item.Value != null && seenItems.Add(item.Value))
                    {
                        output.Add(item);
                    }
                }
            }

            /// <summary>
            /// Intrinsic function reverses the item list.
            /// </summary>
            internal static void Reverse(
                List<TransformEntry> input,
                List<TransformEntry> output,
                string[] arguments,
                string functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                for (int i = input.Count - 1; i >= 0; i--)
                {
                    output.Add(input[i]);
                }
            }

            /// <summary>
            /// Intrinsic function that transforms expressions like the %(foo) in @(Compile->'%(foo)').
            /// </summary>
            internal static void ExpandQuotedExpressionFunction(
                List<TransformEntry> input,
                List<TransformEntry> output,
                string[] arguments,
                bool includeNullEntries,
                string functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                string quotedExpressionFunction = arguments[0];
                OneOrMultipleMetadataMatches matches = GetQuotedExpressionMatches(quotedExpressionFunction, elementLocation);

                // This is just a sanity check in case a code change causes something in the call stack to take this reference.
                SpanBasedStringBuilder includeBuilder = s_includeBuilder ?? new SpanBasedStringBuilder();
                s_includeBuilder = null;

                foreach (TransformEntry item in input)
                {
                    string include = null;

                    // If we've been handed a null entry by an upstream transform
                    // then we don't want to try to tranform it with an itemspec modification.
                    // Simply allow the null to be passed along (if, we are including nulls as specified by includeNullEntries
                    if (item.Value != null)
                    {
                        int curIndex = 0;

                        switch (matches.Type)
                        {
                            case MetadataMatchType.None:
                                // If we didn't match anything, just use the original string.
                                include = quotedExpressionFunction;
                                break;

                            // If we matched on a full string, we don't have to concatenate anything.
                            case MetadataMatchType.ExactSingle:
                                include = GetMetadataValueFromMatch(matches.Single, item.Value, item.Item, elementLocation, ref curIndex);
                                break;

                            // If we matched on a partial string, just replace the single group.
                            case MetadataMatchType.InexactSingle:
                                includeBuilder.Append(quotedExpressionFunction, 0, matches.Single.Index);
                                includeBuilder.Append(
                                    GetMetadataValueFromMatch(matches.Single, item.Value, item.Item, elementLocation, ref curIndex));
                                includeBuilder.Append(quotedExpressionFunction, curIndex, quotedExpressionFunction.Length - curIndex);
                                include = includeBuilder.ToString();
                                includeBuilder.Clear();
                                break;

                            // Otherwise, iteratively replace each match group.
                            case MetadataMatchType.Multiple:
                                foreach (MetadataMatch match in matches.Multiple)
                                {
                                    includeBuilder.Append(quotedExpressionFunction, curIndex, match.Index - curIndex);
                                    includeBuilder.Append(
                                        GetMetadataValueFromMatch(match, item.Value, item.Item, elementLocation, ref curIndex));
                                }

                                includeBuilder.Append(quotedExpressionFunction, curIndex, quotedExpressionFunction.Length - curIndex);
                                include = includeBuilder.ToString();
                                includeBuilder.Clear();
                                break;
                            default:
                                break;
                        }
                    }

                    // Include may be empty. Historically we have created items with empty include
                    // and ultimately set them on tasks, but we don't do that anymore as it's broken.
                    // Instead we optionally add a null, so that input and output lists are the same length; this allows
                    // the caller to possibly do correlation.

                    // We pass in the existing item so we can copy over its metadata
                    if (!string.IsNullOrEmpty(include))
                    {
                        output.Add(new TransformEntry(include, item.Item));
                    }
                    else if (includeNullEntries)
                    {
                        output.Add(new TransformEntry(value: null, item.Item));
                    }
                }

                s_includeBuilder = includeBuilder;
            }

            /// <summary>
            /// Scans the input string for unqualified metadata references of the form <c>%(Name)</c>.
            /// In the vast majority of cases, we'll only have 1-2 matches.
            /// Qualified metadata (e.g. <c>%(ItemType.Name)</c>) is not allowed in transforms and will throw.
            /// </summary>
            private static OneOrMultipleMetadataMatches GetQuotedExpressionMatches(string quotedExpressionFunction, IElementLocation elementLocation)
            {
                // Start with fast paths to avoid any allocations.
                if (TryGetCachedMetadataMatch(quotedExpressionFunction, out string cachedName)
                    || s_itemSpecModifiers.TryGetValue(quotedExpressionFunction, out cachedName))
                {
                    return new OneOrMultipleMetadataMatches(cachedName);
                }

                // Scan for %(Name) references manually.
                int firstIndex = quotedExpressionFunction.IndexOf("%(", StringComparison.Ordinal);
                if (firstIndex == -1)
                {
                    return OneOrMultipleMetadataMatches.None;
                }

                List<MetadataMatch> multipleMatches = null;
                MetadataMatch? firstMatch = null;

                int pos = firstIndex;
                while (pos < quotedExpressionFunction.Length - 1)
                {
                    if (quotedExpressionFunction[pos] != '%' || quotedExpressionFunction[pos + 1] != '(')
                    {
                        pos++;
                        continue;
                    }

                    int refEnd = pos + 2;

                    if (!ExpressionShredder.TryParseMetadataExpression(quotedExpressionFunction, ref refEnd, quotedExpressionFunction.Length, out string itemType, out string name))
                    {
                        pos += 2;
                        continue;
                    }

                    // Qualified metadata is not allowed in transforms.
                    if (itemType != null)
                    {
                        string matchValue = quotedExpressionFunction.Substring(pos, refEnd - pos);
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "QualifiedMetadataInTransformNotAllowed", matchValue, name);
                    }

                    int matchLength = refEnd - pos;

                    if (firstMatch == null)
                    {
                        firstMatch = new MetadataMatch(pos, matchLength, name);
                    }
                    else
                    {
                        multipleMatches ??= [firstMatch.Value];
                        multipleMatches.Add(new MetadataMatch(pos, matchLength, name));
                    }

                    pos = refEnd;
                }

                if (multipleMatches != null)
                {
                    return new OneOrMultipleMetadataMatches(multipleMatches);
                }

                if (firstMatch != null)
                {
                    MetadataMatch match = firstMatch.Value;
                    OneOrMultipleMetadataMatches singleMatch = new(quotedExpressionFunction, match.Index, match.Length, match.Name);

                    if (singleMatch.Type == MetadataMatchType.ExactSingle && !ItemSpecModifiers.IsItemSpecModifier(match.Name))
                    {
                        s_lastParsedQuotedExpression = match.Name;
                    }

                    return singleMatch;
                }

                return new OneOrMultipleMetadataMatches();
            }

            /// <summary>
            /// Given a string such as %(ReferenceAssembly), check if the inner substring matches the cached value.
            /// If so, return the cached substring without allocating.
            /// </summary>
            /// <remarks>
            /// <see cref="ExpandQuotedExpressionFunction"/> often receives the same expression for multiple calls.
            /// To save on regex overhead, we cache the last substring extracted from a regex match.
            /// This is thread-safe as long as all checks work on a consistent local reference.
            /// </remarks>
            private static bool TryGetCachedMetadataMatch(string stringToCheck, out string cachedMatch)
            {
                // Pull a local reference first in case the cached value is swapped.
                cachedMatch = s_lastParsedQuotedExpression;
                if (string.IsNullOrEmpty(cachedMatch))
                {
                    return false;
                }

                // Quickly cancel out of definite misses.
                int length = stringToCheck.Length;
                if (length == cachedMatch.Length + QuotedExpressionSurroundCharCount
                    && stringToCheck[0] == '%' && stringToCheck[1] == '(' && stringToCheck[length - 1] == ')')
                {
                    // If the inner slice is a hit, don't allocate a string.
                    ReadOnlySpan<char> span = stringToCheck.AsSpan(2, length - QuotedExpressionSurroundCharCount);
                    if (span.SequenceEqual(cachedMatch.AsSpan()))
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Intrinsic function that transforms expressions by invoking methods of System.String on the itemspec
            /// of the item in the pipeline.
            /// </summary>
            internal static void ExecuteStringFunction(
                Expander<P, I> expander,
                List<TransformEntry> input,
                List<TransformEntry> output,
                string[] arguments,
                bool includeNullEntries,
                string functionName,
                IElementLocation elementLocation)
            {
                // Transform: expression is like @(Compile->'%(foo)'), so create completely new items,
                // using the Include from the source items
                foreach (TransformEntry item in input)
                {
                    Function function = new Function(
                        typeof(string),
                        item.Value,
                        item.Value,
                        functionName,
                        arguments,
                        BindingFlags.Public | BindingFlags.InvokeMethod,
                        string.Empty,
                        expander.PropertiesUseTracker,
                        expander._fileSystem,
                        expander._loggingContext);

                    object result = function.Execute(item.Value, expander._properties, ExpanderOptions.ExpandAll, elementLocation);

                    string include = PropertyExpander.ConvertToString(result);

                    // We pass in the existing item so we can copy over its metadata
                    if (include.Length > 0)
                    {
                        output.Add(new TransformEntry(include, item.Item));
                    }
                    else if (includeNullEntries)
                    {
                        output.Add(new TransformEntry(null, item.Item));
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds the items from input with their metadata cleared, i.e. only the itemspec is retained.
            /// </summary>
            internal static void ClearMetadata(
                List<TransformEntry> input,
                List<TransformEntry> output,
                string[] arguments,
                bool includeNullEntries,
                string functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                foreach (TransformEntry item in input)
                {
                    if (includeNullEntries || item.Value != null)
                    {
                        output.Add(new TransformEntry(item.Value, null));
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds only those items that have a not-blank value for the metadata specified
            /// Using a case insensitive comparison.
            /// </summary>
            internal static void HasMetadata(
                List<TransformEntry> input,
                List<TransformEntry> output,
                string[] arguments,
                string functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                string metadataName = arguments[0];

                foreach (TransformEntry item in input)
                {
                    string metadataValue = null;

                    try
                    {
                        metadataValue = item.Item.GetMetadataValueEscaped(metadataName);
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                    {
                        // Blank metadata name
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                    }

                    // GetMetadataValueEscaped returns empty string for missing metadata,
                    // but IItem specifies it should return null
                    if (!string.IsNullOrEmpty(metadataValue))
                    {
                        // return a result through the enumerator
                        output.Add(item);
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds only those items have the given metadata value
            /// Using a case insensitive comparison.
            /// </summary>
            internal static void WithMetadataValue(
                List<TransformEntry> input,
                List<TransformEntry> output,
                string[] arguments,
                string functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 2, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                string metadataName = arguments[0];
                string metadataValueToFind = arguments[1];

                foreach (TransformEntry item in input)
                {
                    string metadataValue = null;

                    try
                    {
                        metadataValue = item.Item.GetMetadataValueEscaped(metadataName);
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                    {
                        // Blank metadata name
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                    }

                    if (metadataValue != null && String.Equals(metadataValue, metadataValueToFind, StringComparison.OrdinalIgnoreCase))
                    {
                        // return a result through the enumerator
                        output.Add(item);
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds those items don't have the given metadata value
            /// Using a case insensitive comparison.
            /// </summary>
            internal static void WithoutMetadataValue(
                List<TransformEntry> input,
                List<TransformEntry> output,
                string[] arguments,
                string functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 2, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                string metadataName = arguments[0];
                string metadataValueToFind = arguments[1];

                foreach (TransformEntry item in input)
                {
                    string metadataValue = null;

                    try
                    {
                        metadataValue = item.Item.GetMetadataValueEscaped(metadataName);
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                    {
                        // Blank metadata name
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                    }

                    if (!String.Equals(metadataValue, metadataValueToFind, StringComparison.OrdinalIgnoreCase))
                    {
                        // return a result through the enumerator
                        output.Add(item);
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds a boolean to indicate if any of the items have the given metadata value
            /// Using a case insensitive comparison.
            /// </summary>
            internal static void AnyHaveMetadataValue(
                List<TransformEntry> input,
                List<TransformEntry> output,
                string[] arguments,
                string functionName,
                IElementLocation elementLocation)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 2, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                string metadataName = arguments[0];
                string metadataValueToFind = arguments[1];
                bool metadataFound = false;

                foreach (TransformEntry item in input)
                {
                    if (item.Item != null)
                    {
                        string metadataValue = null;

                        try
                        {
                            metadataValue = item.Item.GetMetadataValueEscaped(metadataName);
                        }
                        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                        {
                            // Blank metadata name
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                        }

                        if (metadataValue != null && String.Equals(metadataValue, metadataValueToFind, StringComparison.OrdinalIgnoreCase))
                        {
                            metadataFound = true;

                            // return a result through the enumerator
                            output.Add(new TransformEntry("true", item.Item));

                            // break out as soon as we found a match
                            return;
                        }
                    }
                }

                if (!metadataFound)
                {
                    // We did not locate an item with the required metadata
                    output.Add(new TransformEntry("false", null));
                }
            }

            /// <summary>
            /// Expands the metadata in the match provided into a string result.
            /// The match is expected to be the content of a transform.
            /// For example, representing "%(Filename.obj)" in the original expression "@(Compile->'%(Filename.obj)')".
            /// </summary>
            private static string GetMetadataValueFromMatch(
                MetadataMatch match,
                string itemSpec,
                IItem sourceOfMetadata,
                IElementLocation elementLocation,
                ref int curIndex)
            {
                string value = null;
                try
                {
                    if (ItemSpecModifiers.IsDerivableItemSpecModifier(match.Name))
                    {
                        // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                        // In that case,
                        // 1. in multiprocess mode we're safe to get the current directory as we'll be running on TaskItems which
                        // only exist within a target where we can trust the current directory
                        // 2. in single process mode we get the project directory set for the thread
                        string directoryToUse = sourceOfMetadata.ProjectDirectory ?? FileUtilities.CurrentThreadWorkingDirectory ?? Directory.GetCurrentDirectory();
                        string definingProjectEscaped = sourceOfMetadata.GetMetadataValueEscaped(ItemSpecModifiers.DefiningProjectFullPath);

                        value = ItemSpecModifiers.GetItemSpecModifier(itemSpec, match.Name, directoryToUse, definingProjectEscaped);
                    }
                    else
                    {
                        value = sourceOfMetadata.GetMetadataValueEscaped(match.Name);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", match.Name, ex.Message);
                }

                curIndex = match.Index + match.Length;
                return value;
            }
        }
    }
}
