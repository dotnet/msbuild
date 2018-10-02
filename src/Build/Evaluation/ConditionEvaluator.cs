// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace Microsoft.Build.Evaluation
{
    using ILoggingService = Microsoft.Build.BackEnd.Logging.ILoggingService;
    using BuildEventContext = Microsoft.Build.Framework.BuildEventContext;
    using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
    using ElementLocation = Microsoft.Build.Construction.ElementLocation;
    using Microsoft.Build.Execution;
    using Microsoft.Build.Shared;
    using Microsoft.Build.Shared.FileSystem;

    internal static class ConditionEvaluator
    {
        private static readonly Lazy<Regex> s_singlePropertyRegex = new Lazy<Regex>(
            () => new Regex(@"^\$\(([^\$\(\)]*)\)$", RegexOptions.Compiled));

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
        internal static void UpdateConditionedPropertiesTable
        (
            Dictionary<string, List<string>> conditionedPropertiesTable,   // List of possible values, keyed by property name

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
                // PERF: Avoid allocations from string.Split by forming spans between 'pieceStart' and 'pieceEnd'
                var pieceStart = 0;

                // Loop through each of the pieces.
                while (true)
                {
                    var pieceSeparator = leftValue.IndexOf('|', pieceStart);
                    var lastPiece = pieceSeparator < 0;
                    var pieceEnd = lastPiece ? leftValue.Length : pieceSeparator;

                    var singlePropertyMatch = s_singlePropertyRegex.Value.Match(leftValue, pieceStart, pieceEnd - pieceStart);

                    if (singlePropertyMatch.Success)
                    {
                        // Find the first vertical bar on the right-hand-side expression.
                        var indexOfVerticalBar = rightValueExpanded.IndexOf('|');
                        string rightValueExpandedPiece;

                        // If there was no vertical bar, then just use the remainder of the right-hand-side
                        // expression as the value of the property, and terminate the loop after this iteration.  
                        // Also, if we're on the last segment of the left-hand-side, then use the remainder
                        // of the right-hand-side expression as the value of the property.
                        if ((indexOfVerticalBar == -1) || lastPiece)
                        {
                            rightValueExpandedPiece = rightValueExpanded;
                            lastPiece = true;
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
                        var propertyName = singlePropertyMatch.Groups[1].ToString();

                        // Get the string collection for this property name, if one already exists.
                        List<string> conditionedPropertyValues;

                        // If this property is not already represented in the table, add a new entry
                        // for it.
                        if (!conditionedPropertiesTable.TryGetValue(propertyName, out conditionedPropertyValues))
                        {
                            conditionedPropertyValues = new List<string>();
                            conditionedPropertiesTable[propertyName] = conditionedPropertyValues;
                        }

                        // If the "rightValueExpanded" is not already in the string collection
                        // for this property name, add it now.
                        if (!conditionedPropertyValues.Contains(rightValueExpandedPiece))
                        {
                            conditionedPropertyValues.Add(rightValueExpandedPiece);
                        }
                    }

                    if (lastPiece)
                    {
                        break;
                    }

                    pieceStart = pieceSeparator + 1;
                }
            }
        }

        // Implements a pool of expression trees for each condition.
        // This is because an expression tree is a mutually exclusive resource (has non thread safe state while it evaluates).
        // During high demand when all expression trees are busy evaluating, a new expression tree is created and added to the pool.
        // The pool is represented by the ConcurrentStack.
        private struct ExpressionTreeForCurrentOptionsWithSize
        {
            // condition string -> pool of expression trees
            private readonly ConcurrentDictionary<string, ConcurrentStack<GenericExpressionNode>> _conditionPools;
            private int _mOptimisticSize;

            public int OptimisticSize => _mOptimisticSize;

            public ExpressionTreeForCurrentOptionsWithSize(ConcurrentDictionary<string, ConcurrentStack<GenericExpressionNode>> conditionPools)
            {
                this._conditionPools = conditionPools;
                _mOptimisticSize = conditionPools.Count;
            }

            public ConcurrentStack<GenericExpressionNode> GetOrAdd(string condition, Func<string, ConcurrentStack<GenericExpressionNode>> addFunc)
            {
                if (!_conditionPools.TryGetValue(condition, out var stack))
                {
                    // Count how many conditions there are in the cache.
                    // The condition evaluator will flush the cache when some threshold is exceeded.
                    Interlocked.Increment(ref _mOptimisticSize);
                    stack = _conditionPools.GetOrAdd(condition, addFunc);
                }

                return stack;
            }
        }

        // Cached expression trees for all the combinations of condition strings and parser options
        private static volatile ConcurrentDictionary<int, ExpressionTreeForCurrentOptionsWithSize> s_cachedExpressionTrees = new ConcurrentDictionary<int, ExpressionTreeForCurrentOptionsWithSize>();

        /// <summary>
        /// For debugging leaks, a way to disable caching expression trees, to reduce noise
        /// </summary>
        private static readonly bool s_disableExpressionCaching = (Environment.GetEnvironmentVariable("MSBUILDDONOTCACHEEXPRESSIONS") == "1");

        /// <summary>
        /// Evaluates a string representing a condition from a "condition" attribute.
        /// If the condition is a malformed string, it throws an InvalidProjectFileException.
        /// This method uses cached expression trees to avoid generating them from scratch every time it's called.
        /// This method is thread safe and is called from engine and task execution module threads
        /// </summary>
        internal static bool EvaluateCondition<P, I>
            (
            string condition,
            ParserOptions options,
            Expander<P, I> expander,
            ExpanderOptions expanderOptions,
            string evaluationDirectory,
            ElementLocation elementLocation,
            ILoggingService loggingServices,
            BuildEventContext buildEventContext,
            IFileSystem fileSystem,
            ProjectRootElementCache projectRootElementCache = null)
            where P : class, IProperty
            where I : class, IItem
        {
            return EvaluateConditionCollectingConditionedProperties(
                condition,
                options,
                expander,
                expanderOptions,
                null /* do not collect conditioned properties */,
                evaluationDirectory,
                elementLocation,
                loggingServices,
                buildEventContext,
                fileSystem,
                projectRootElementCache);
        }

        /// <summary>
        /// Evaluates a string representing a condition from a "condition" attribute.
        /// If the condition is a malformed string, it throws an InvalidProjectFileException.
        /// This method uses cached expression trees to avoid generating them from scratch every time it's called.
        /// This method is thread safe and is called from engine and task execution module threads
        /// Logging service may be null.
        /// </summary>
        internal static bool EvaluateConditionCollectingConditionedProperties<P, I>
        (
            string condition,
            ParserOptions options,
            Expander<P, I> expander,
            ExpanderOptions expanderOptions,
            Dictionary<string, List<string>> conditionedPropertiesTable,
            string evaluationDirectory,
            ElementLocation elementLocation,
            ILoggingService loggingServices,
            BuildEventContext buildEventContext,
            IFileSystem fileSystem,
            ProjectRootElementCache projectRootElementCache = null
        )
            where P : class, IProperty
            where I : class, IItem
        {
            ErrorUtilities.VerifyThrowArgumentNull(condition, "condition");
            ErrorUtilities.VerifyThrowArgumentNull(expander, "expander");
            ErrorUtilities.VerifyThrowArgumentLength(evaluationDirectory, "evaluationDirectory");
            ErrorUtilities.VerifyThrowArgumentNull(buildEventContext, "buildEventContext");

            // An empty condition is equivalent to a "true" condition.
            if (condition.Length == 0)
            {
                return true;
            }

            // If the condition wasn't empty, there must be a location for it
            ErrorUtilities.VerifyThrowArgumentNull(elementLocation, "elementLocation");

            // Get the expression tree cache for the current parsing options.
            var cachedExpressionTreesForCurrentOptions = s_cachedExpressionTrees.GetOrAdd(
                (int)options,
                _ => new ExpressionTreeForCurrentOptionsWithSize(new ConcurrentDictionary<string, ConcurrentStack<GenericExpressionNode>>(StringComparer.Ordinal)));

            cachedExpressionTreesForCurrentOptions = FlushCacheIfLargerThanThreshold(options, cachedExpressionTreesForCurrentOptions);

            // Get the pool of expressions for this condition.
            var expressionPool = cachedExpressionTreesForCurrentOptions.GetOrAdd(condition, _ => new ConcurrentStack<GenericExpressionNode>());

            // Try and see if there's an available expression tree in the pool.
            // If not, parse a new expression tree and add it back to the pool.
            if (!expressionPool.TryPop(out var parsedExpression))
            {
                var conditionParser = new Parser();

                #region REMOVE_COMPAT_WARNING
                conditionParser.LoggingServices = loggingServices;
                conditionParser.LogBuildEventContext = buildEventContext;
                #endregion

                parsedExpression = conditionParser.Parse(condition, options, elementLocation);
            }

            bool result;

            var state = new ConditionEvaluationState<P, I>(
                condition,
                expander,
                expanderOptions,
                conditionedPropertiesTable,
                evaluationDirectory,
                elementLocation,
                fileSystem,
                projectRootElementCache);

            // We are evaluating this expression now and it can cache some state for the duration,
            // so we don't want multiple threads working on the same expression
            lock (parsedExpression)
            {
                try
                {
                    result = parsedExpression.Evaluate(state);
                }
                finally
                {
                    parsedExpression.ResetState();
                    if (!s_disableExpressionCaching)
                    {
                        // Finished using the expression tree. Add it back to the pool so other threads can use it.
                        expressionPool.Push(parsedExpression);
                    }
                }
            }

            return result;
        }

        private static ExpressionTreeForCurrentOptionsWithSize FlushCacheIfLargerThanThreshold(
            ParserOptions options,
            ExpressionTreeForCurrentOptionsWithSize cachedExpressionTreesForCurrentOptions)
        {
            if (cachedExpressionTreesForCurrentOptions.OptimisticSize > 3000)
            {
                // VS stress tests could fill up this cache without end, for example if they use
                // random configuration names - those appear in conditional expressions.
                // So if we hit a limit that we should never hit in normal circumstances in VS,
                // and rarely, periodically hit in normal circumstances in large tree builds,
                // just clear out the cache. It can start repopulating again. Some kind of prioritized
                // aging isn't worth it: although the hit rate of these caches is excellent (nearly 100%)
                // the cost of reparsing expressions should the cache be cleared is not particularly large.
                // Loading Australian Government in VS, there are 3 of these tables, two with about 50
                // entries and one with about 650 entries. So 3000 seems large enough.
                cachedExpressionTreesForCurrentOptions = s_cachedExpressionTrees.AddOrUpdate(
                    (int)options,
                    _ =>
                        new ExpressionTreeForCurrentOptionsWithSize(
                            new ConcurrentDictionary<string, ConcurrentStack<GenericExpressionNode>>(StringComparer.Ordinal)),
                    (key, existing) =>
                    {
                        if (existing.OptimisticSize > 3000)
                        {
                            return
                                new ExpressionTreeForCurrentOptionsWithSize(
                                    new ConcurrentDictionary<string, ConcurrentStack<GenericExpressionNode>>(StringComparer.Ordinal));
                        }
                        else
                        {
                            return existing;
                        }
                    });
            }

            return cachedExpressionTreesForCurrentOptions;
        }

        internal interface IConditionEvaluationState
        {
            string Condition { get; }

            string EvaluationDirectory { get; }

            ElementLocation ElementLocation { get; }

            /// <summary>
            ///     Table of conditioned properties and their values.
            ///     Used to populate configuration lists in some project systems.
            ///     If this is null, as it is for command line builds, conditioned properties
            ///     are not recorded.
            /// </summary>
            Dictionary<string, List<string>> ConditionedPropertiesInProject { get; }

            /// <summary>
            ///     May return null if the expression would expand to non-empty and it broke out early.
            ///     Otherwise, returns the correctly expanded expression.
            /// </summary>
            string ExpandIntoStringBreakEarly(string expression);

            /// <summary>
            ///     Expands the specified expression into a list of TaskItem's.
            /// </summary>
            IList<TaskItem> ExpandIntoTaskItems(string expression);

            /// <summary>
            ///     Expands the specified expression into a string.
            /// </summary>
            string ExpandIntoString(string expression);

            /// <summary>
            ///     PRE cache
            /// </summary>
            ProjectRootElementCache LoadedProjectsCache { get; }

            IFileSystem FileSystem { get; }
        }

        /// <summary>
        /// All the state necessary for the evaluation of conditionals so that the expression tree 
        /// is stateless and reusable
        /// </summary>
        internal class ConditionEvaluationState<P, I> : IConditionEvaluationState
            where P : class, IProperty
            where I : class, IItem
        {
            private readonly Expander<P, I> _expander;
            private readonly ExpanderOptions _expanderOptions;

            /// <summary>
            /// Condition that was parsed. This does not belong here,
            /// it belongs to the expression tree, not the condition evaluation state.
            /// </summary>
            public string Condition { get; }

            public string EvaluationDirectory { get; }

            public ElementLocation ElementLocation { get; }

            public IFileSystem FileSystem { get; }

            /// <summary>
            /// Table of conditioned properties and their values.
            /// Used to populate configuration lists in some project systems.
            /// If this is null, as it is for command line builds, conditioned properties
            /// are not recorded.
            /// </summary>
            public Dictionary<string, List<string>> ConditionedPropertiesInProject { get; }

            /// <summary>
            /// PRE collection. 
            /// </summary>
            public ProjectRootElementCache LoadedProjectsCache { get; }

            internal ConditionEvaluationState
                (
                string condition,
                Expander<P, I> expander,
                ExpanderOptions expanderOptions,
                Dictionary<string, List<string>> conditionedPropertiesInProject,
                string evaluationDirectory,
                ElementLocation elementLocation,
                IFileSystem fileSystem,
                ProjectRootElementCache projectRootElementCache = null
                )
            {
                ErrorUtilities.VerifyThrowArgumentNull(condition, "condition");
                ErrorUtilities.VerifyThrowArgumentNull(expander, "expander");
                ErrorUtilities.VerifyThrowArgumentNull(evaluationDirectory, "evaluationDirectory");
                ErrorUtilities.VerifyThrowArgumentNull(elementLocation, "elementLocation");

                Condition = condition;
                _expander = expander;
                _expanderOptions = expanderOptions;
                ConditionedPropertiesInProject = conditionedPropertiesInProject; // May be null
                EvaluationDirectory = evaluationDirectory;
                ElementLocation = elementLocation;
                LoadedProjectsCache = projectRootElementCache;
                FileSystem = fileSystem;
            }

            /// <summary>
            /// May return null if the expression would expand to non-empty and it broke out early.
            /// Otherwise, returns the correctly expanded expression.
            /// </summary>
            public string ExpandIntoStringBreakEarly(string expression)
            {
                var originalValue = _expander.WarnForUninitializedProperties;

                expression = _expander.ExpandIntoStringAndUnescape(expression, _expanderOptions | ExpanderOptions.BreakOnNotEmpty, ElementLocation);

                _expander.WarnForUninitializedProperties = originalValue;

                return expression;
            }

            /// <summary>
            /// Expands the properties and items in the specified expression into a list of taskitems.
            /// </summary>
            /// <param name="expression">The expression to expand.</param>
            /// <returns>A list of items.</returns>
            public IList<TaskItem> ExpandIntoTaskItems(string expression)
            {
                var originalValue = _expander.WarnForUninitializedProperties;

                var items = _expander.ExpandIntoTaskItemsLeaveEscaped(expression, _expanderOptions, ElementLocation);

                _expander.WarnForUninitializedProperties = originalValue;

                return items;
            }

            /// <summary>
            /// Expands the specified expression into a string.
            /// </summary>
            /// <param name="expression">The expression to expand.</param>
            /// <returns>The expanded string.</returns>
            public string ExpandIntoString(string expression)
            {
                var originalValue = _expander.WarnForUninitializedProperties;

                expression = _expander.ExpandIntoStringAndUnescape(expression, _expanderOptions, ElementLocation);

                _expander.WarnForUninitializedProperties = originalValue;

                return expression;
            }
        }
    }
}
