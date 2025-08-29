// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using Microsoft.Build.BuildEngine.Shared;

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// Evaluates a function expression, such as "Exists('foo')"
    /// </summary>
    internal sealed class FunctionCallExpressionNode : OperatorExpressionNode
    {
        private ArrayList arguments;
        private string functionName;

        private FunctionCallExpressionNode() { }

        internal FunctionCallExpressionNode(string functionName, ArrayList arguments)
        {
            this.functionName = functionName;
            this.arguments = arguments;
        }

        /// <summary>
        /// Evaluate node as boolean
        /// </summary>
        internal override bool BoolEvaluate(ConditionEvaluationState state)
        {
            if (String.Equals(functionName, "exists", StringComparison.OrdinalIgnoreCase))
            {
                // Check we only have one argument
                VerifyArgumentCount(1, state);

                // Expand properties and items, and verify the result is an appropriate scalar
                string expandedValue = ExpandArgumentForScalarParameter("exists", (GenericExpressionNode)arguments[0], state);

                if (Project.PerThreadProjectDirectory != null && !String.IsNullOrEmpty(expandedValue))
                {
                    try
                    {
                        expandedValue = Path.GetFullPath(Path.Combine(Project.PerThreadProjectDirectory, expandedValue));
                    }
                    catch (Exception e) // Catching Exception, but rethrowing unless it's an IO related exception.
                    {
                        if (ExceptionHandling.NotExpectedException(e))
                        {
                            throw;
                        }

                        // Ignore invalid characters or path related exceptions

                        // We will ignore the PathTooLong exception caused by GetFullPath becasue in single proc this code 
                        // is not executed and the condition is just evaluated to false as File.Exists and Directory.Exists does not throw in this situation. 
                        // To be consistant with that we will return a false in this case also.
                        // DevDiv Bugs: 46035

                        return false;
                    }
                }

                // Both Exists functions return false if the value is null or empty
                return File.Exists(expandedValue) || Directory.Exists(expandedValue);
            }
            else if (String.Equals(functionName, "HasTrailingSlash", StringComparison.OrdinalIgnoreCase))
            {
                // Check we only have one argument
                VerifyArgumentCount(1, state);

                // Expand properties and items, and verify the result is an appropriate scalar
                string expandedValue = ExpandArgumentForScalarParameter("HasTrailingSlash", (GenericExpressionNode)arguments[0], state);

                // Is the last character a backslash?
                if (expandedValue.Length != 0)
                {
                    char lastCharacter = expandedValue[expandedValue.Length - 1];
                    // Either back or forward slashes satisfy the function: this is useful for URL's
                    return lastCharacter == Path.DirectorySeparatorChar || lastCharacter == Path.AltDirectorySeparatorChar;
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
                    state.conditionAttribute,
                    "UndefinedFunctionCall",
                    state.parsedCondition,
                    this.functionName);

                return false;
            }
        }

        /// <summary>
        /// Expands properties and items in the argument, and verifies that the result is consistent
        /// with a scalar parameter type.
        /// </summary>
        /// <param name="function">Function name for errors</param>
        /// <param name="argumentNode">Argument to be expanded</param>
        /// <returns>Scalar result</returns>
        /// <owner>danmose</owner>
        private string ExpandArgumentForScalarParameter(string function, GenericExpressionNode argumentNode, ConditionEvaluationState state)
        {
            string argument = argumentNode.GetUnexpandedValue(state);

            List<TaskItem> items = state.expanderToUse.ExpandAllIntoTaskItems(argument, state.conditionAttribute);

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
                ProjectErrorUtilities.VerifyThrowInvalidProject(false,
                    state.conditionAttribute,
                    "CannotPassMultipleItemsIntoScalarFunction", function, argument,
                    state.expanderToUse.ExpandAllIntoString(argument, state.conditionAttribute));
            }

            return expandedValue;
        }

        /// <summary>
        /// Check that the number of function arguments is correct.
        /// </summary>
        /// <param name="expected"></param>
        private void VerifyArgumentCount(int expected, ConditionEvaluationState state)
        {
            ProjectErrorUtilities.VerifyThrowInvalidProject
                (arguments.Count == expected,
                 state.conditionAttribute,
                 "IncorrectNumberOfFunctionArguments",
                 state.parsedCondition,
                 arguments.Count,
                 expected);
        }
    }
}
