// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Shared;

using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Evaluates a function expression, such as "Exists('foo')"
    /// </summary>
    internal sealed class FunctionCallExpressionNode : OperatorExpressionNode
    {
        private readonly List<GenericExpressionNode> _arguments;
        private readonly string _functionName;

        internal FunctionCallExpressionNode(string functionName, List<GenericExpressionNode> arguments)
        {
            _functionName = functionName;
            _arguments = arguments;
        }

        /// <summary>
        /// Evaluate node as boolean
        /// </summary>
        internal override bool BoolEvaluate(ConditionEvaluator.IConditionEvaluationState state)
        {
            if (String.Equals(_functionName, "exists", StringComparison.OrdinalIgnoreCase))
            {
                // Check we only have one argument
                VerifyArgumentCount(1, state);

                try
                {
                    // Expand the items and use DefaultIfEmpty in case there is nothing returned
                    // Then check if everything is not null (because the list was empty), not
                    // already loaded into the cache, and exists
                    List<string> list = ExpandArgumentAsFileList(_arguments[0], state);
                    if (list == null)
                    {
                        return false;
                    }

                    foreach (var item in list)
                    {
                        if (item == null || !(state.LoadedProjectsCache?.TryGet(item) != null || FileUtilities.FileOrDirectoryExistsNoThrow(item, state.FileSystem)))
                        {
                            return false;
                        }
                    }

                    return true;
                }
                catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                {
                    // Ignore invalid characters or path related exceptions

                    // We will ignore the PathTooLong exception caused by GetFullPath because in single proc this code
                    // is not executed and the condition is just evaluated to false as File.Exists and Directory.Exists does not throw in this situation. 
                    // To be consistant with that we will return a false in this case also.
                    // DevDiv Bugs: 46035

                    return false;
                }
            }
            else if (String.Equals(_functionName, "HasTrailingSlash", StringComparison.OrdinalIgnoreCase))
            {
                // Check we only have one argument
                VerifyArgumentCount(1, state);

                // Expand properties and items, and verify the result is an appropriate scalar
                string expandedValue = ExpandArgumentForScalarParameter("HasTrailingSlash", _arguments[0], state);

                // Is the last character a backslash?
                if (expandedValue.Length != 0)
                {
                    char lastCharacter = expandedValue[expandedValue.Length - 1];
                    // Either back or forward slashes satisfy the function: this is useful for URL's
                    return lastCharacter == Path.DirectorySeparatorChar || lastCharacter == Path.AltDirectorySeparatorChar || lastCharacter == '\\';
                }
                else
                {
                    return false;
                }
            }
            // We haven't implemented any other "functions"
            else
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(
                    false,
                    state.ElementLocation,
                    "UndefinedFunctionCall",
                    state.Condition,
                    _functionName);

                return false;
            }
        }

        /// <summary>
        /// Expands properties and items in the argument, and verifies that the result is consistent
        /// with a scalar parameter type.
        /// </summary>
        /// <param name="function">Function name for errors</param>
        /// <param name="argumentNode">Argument to be expanded</param>
        /// <param name="state"></param>
        /// <param name="isFilePath">True if this is afile name and the path should be normalized</param>
        /// <returns>Scalar result</returns>
        private static string ExpandArgumentForScalarParameter(string function, GenericExpressionNode argumentNode, ConditionEvaluator.IConditionEvaluationState state,
            bool isFilePath = true)
        {
            string argument = argumentNode.GetUnexpandedValue(state);

            // Fix path before expansion
            if (isFilePath)
            {
                argument = FileUtilities.FixFilePath(argument);
            }

            IList<TaskItem> items = state.ExpandIntoTaskItems(argument);

            string expandedValue = String.Empty;

            if (items.Count == 0)
            {
                // Empty argument, that's fine.
            }
            else if (items.Count == 1)
            {
                expandedValue = items[0].ItemSpec;
            }
            else // too many items for the function
            {
                // We only allow a single item to be passed into a scalar parameter.
                ProjectErrorUtilities.ThrowInvalidProject(
                    state.ElementLocation,
                    "CannotPassMultipleItemsIntoScalarFunction", function, argument,
                    state.ExpandIntoString(argument));
            }

            return expandedValue;
        }

        private List<string> ExpandArgumentAsFileList(GenericExpressionNode argumentNode, ConditionEvaluator.IConditionEvaluationState state, bool isFilePath = true)
        {
            string argument = argumentNode.GetUnexpandedValue(state);

            // Fix path before expansion
            if (isFilePath)
            {
                argument = FileUtilities.FixFilePath(argument);
            }

            IList<TaskItem> expanded = state.ExpandIntoTaskItems(argument);
            var expandedCount = expanded.Count;

            if (expandedCount == 0)
            {
                return null;
            }

            var list = new List<string>(capacity: expandedCount);
            for (var i = 0; i < expandedCount; i++)
            {
                var item = expanded[i];
                if (state.EvaluationDirectory != null && !Path.IsPathRooted(item.ItemSpec))
                {
                    list.Add(Path.GetFullPath(Path.Combine(state.EvaluationDirectory, item.ItemSpec)));
                }
                else
                {
                    list.Add(item.ItemSpec);
                }
            }

            return list;
        }

        /// <summary>
        /// Check that the number of function arguments is correct.
        /// </summary>
        private void VerifyArgumentCount(int expected, ConditionEvaluator.IConditionEvaluationState state)
        {
            ProjectErrorUtilities.VerifyThrowInvalidProject
                (_arguments.Count == expected,
                 state.ElementLocation,
                 "IncorrectNumberOfFunctionArguments",
                 state.Condition,
                 _arguments.Count,
                 expected);
        }
    }
}
