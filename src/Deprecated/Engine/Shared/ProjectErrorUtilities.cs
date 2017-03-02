// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;

/******************************************************************************
 * 
 *                              !! WARNING !!
 * 
 * This class depends on the build engine assembly! Do not share this class
 * into any assembly that is not supposed to take a dependency on the build
 * engine assembly!
 * 
 * 
 ******************************************************************************/
using Microsoft.Build.BuildEngine;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// This class contains methods that are useful for error checking and
    /// validation of project files.
    /// </summary>
    /// <owner>SumedhK</owner>
    internal static class ProjectErrorUtilities
    {
        /// <summary>
        /// This method is used to flag errors in the project file being processed.
        /// Do NOT use this method in place of ErrorUtilities.VerifyThrow(), because
        /// ErrorUtilities.VerifyThrow() is used to flag internal/programming errors.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="condition">The condition to check.</param>
        /// <param name="xmlNode">The invalid project node (can be null).</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        internal static void VerifyThrowInvalidProject
        (
            bool condition,
            XmlNode xmlNode,
            string resourceName
        )
        {
            VerifyThrowInvalidProject(condition, null, xmlNode, resourceName);
        }

        /// <summary>
        /// Overload for one string format argument.
        /// </summary>
        /// <param name="xmlNode">The invalid project node (can be null).</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        internal static void ThrowInvalidProject
        (
            XmlNode xmlNode,
            string resourceName,
            object arg0
        )
        {
            VerifyThrowInvalidProject(false, null, xmlNode, resourceName, arg0);
        }

        /// <summary>
        /// Overload for one string format argument.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="condition">The condition to check.</param>
        /// <param name="xmlNode">The invalid project node (can be null).</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        internal static void VerifyThrowInvalidProject
        (
            bool condition,
            XmlNode xmlNode,
            string resourceName,
            object arg0
        )
        {
            VerifyThrowInvalidProject(condition, null, xmlNode, resourceName, arg0);
        }

        /// <summary>
        /// Overload for two string format arguments.
        /// </summary>
        /// <param name="xmlNode">The invalid project node (can be null).</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        internal static void ThrowInvalidProject
        (
            XmlNode xmlNode,
            string resourceName,
            object arg0,
            object arg1
        )
        {
            VerifyThrowInvalidProject(false, null, xmlNode, resourceName, arg0, arg1);
        }

        /// <summary>
        /// Overload for two string format arguments.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="condition">The condition to check.</param>
        /// <param name="xmlNode">The invalid project node (can be null).</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        internal static void VerifyThrowInvalidProject
        (
            bool condition,
            XmlNode xmlNode,
            string resourceName,
            object arg0,
            object arg1
        )
        {
            VerifyThrowInvalidProject(condition, null, xmlNode, resourceName, arg0, arg1);
        }

        /// <summary>
        /// Overload for three string format arguments.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="condition">The condition to check.</param>
        /// <param name="xmlNode">The invalid project node (can be null).</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        internal static void VerifyThrowInvalidProject
        (
            bool condition,
            XmlNode xmlNode,
            string resourceName,
            object arg0,
            object arg1,
            object arg2
        )
        {
            VerifyThrowInvalidProject(condition, null, xmlNode, resourceName, arg0, arg1, arg2);
        }

        /// <summary>
        /// Overload for four string format arguments.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="condition">The condition to check.</param>
        /// <param name="xmlNode">The invalid project node (can be null).</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        internal static void VerifyThrowInvalidProject
        (
            bool condition,
            XmlNode xmlNode,
            string resourceName,
            object arg0,
            object arg1,
            object arg2,
            object arg3
        )
        {
            VerifyThrowInvalidProject(condition, null, xmlNode, resourceName, arg0, arg1, arg2, arg3);
        }

        /// <summary>
        /// This method is used to flag errors in the project file being processed.
        /// Do NOT use this method in place of ErrorUtilities.VerifyThrow(), because
        /// ErrorUtilities.VerifyThrow() is used to flag internal/programming errors.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="condition">The condition to check.</param>
        /// <param name="errorSubCategoryResourceName">The resource string for the
        /// error sub-category (can be null).</param>
        /// <param name="xmlNode">The invalid project node (can be null).</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        internal static void VerifyThrowInvalidProject
        (
            bool condition,
            string errorSubCategoryResourceName,
            XmlNode xmlNode,
            string resourceName
        )
        {
            if (!condition)
            {
                // PERF NOTE: explicitly passing null for the arguments array
                // prevents memory allocation
                ThrowInvalidProject(errorSubCategoryResourceName, xmlNode, resourceName, null);
            }
        }

        /// <summary>
        /// Overload for one string format argument.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="condition">The condition to check.</param>
        /// <param name="errorSubCategoryResourceName">The resource string for the
        /// error sub-category (can be null).</param>
        /// <param name="xmlNode">The invalid project node (can be null).</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        internal static void VerifyThrowInvalidProject
        (
            bool condition,
            string errorSubCategoryResourceName,
            XmlNode xmlNode,
            string resourceName,
            object arg0
        )
        {
            // PERF NOTE: check the condition here instead of pushing it into
            // the ThrowInvalidProject() method, because that method always
            // allocates memory for its variable array of arguments
            if (!condition)
            {
                ThrowInvalidProject(errorSubCategoryResourceName, xmlNode, resourceName, arg0);
            }
        }

        /// <summary>
        /// Overload for two string format arguments.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="condition">The condition to check.</param>
        /// <param name="errorSubCategoryResourceName">The resource string for the
        /// error sub-category (can be null).</param>
        /// <param name="xmlNode">The invalid project node (can be null).</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        internal static void VerifyThrowInvalidProject
        (
            bool condition,
            string errorSubCategoryResourceName,
            XmlNode xmlNode,
            string resourceName,
            object arg0,
            object arg1
        )
        {
            // PERF NOTE: check the condition here instead of pushing it into
            // the ThrowInvalidProject() method, because that method always
            // allocates memory for its variable array of arguments
            if (!condition)
            {
                ThrowInvalidProject(errorSubCategoryResourceName, xmlNode, resourceName, arg0, arg1);
            }
        }

        /// <summary>
        /// Overload for three string format arguments.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="condition">The condition to check.</param>
        /// <param name="errorSubCategoryResourceName">The resource string for the
        /// error sub-category (can be null).</param>
        /// <param name="xmlNode">The invalid project node (can be null).</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        internal static void VerifyThrowInvalidProject
        (
            bool condition,
            string errorSubCategoryResourceName,
            XmlNode xmlNode,
            string resourceName,
            object arg0,
            object arg1,
            object arg2
        )
        {
            // PERF NOTE: check the condition here instead of pushing it into
            // the ThrowInvalidProject() method, because that method always
            // allocates memory for its variable array of arguments
            if (!condition)
            {
                ThrowInvalidProject(errorSubCategoryResourceName, xmlNode, resourceName, arg0, arg1, arg2);
            }
        }

        /// <summary>
        /// Overload for four string format arguments.
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="condition">The condition to check.</param>
        /// <param name="errorSubCategoryResourceName">The resource string for the
        /// error sub-category (can be null).</param>
        /// <param name="xmlNode">The invalid project node (can be null).</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        internal static void VerifyThrowInvalidProject
        (
            bool condition,
            string errorSubCategoryResourceName,
            XmlNode xmlNode,
            string resourceName,
            object arg0,
            object arg1,
            object arg2,
            object arg3
        )
        {
            // PERF NOTE: check the condition here instead of pushing it into
            // the ThrowInvalidProject() method, because that method always
            // allocates memory for its variable array of arguments
            if (!condition)
            {
                ThrowInvalidProject(errorSubCategoryResourceName, xmlNode, resourceName, arg0, arg1, arg2, arg3);
            }
        }

        /// <summary>
        /// Throws an InvalidProjectFileException using the given data.
        /// 
        /// PERF WARNING: calling a method that takes a variable number of arguments
        /// is expensive, because memory is allocated for the array of arguments -- do
        /// not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="errorSubCategoryResourceName">The resource string for the
        /// error sub-category (can be null).</param>
        /// <param name="xmlNode">The invalid project node (can be null).</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="args">Extra arguments for formatting the error message.</param>
        private static void ThrowInvalidProject
        (
            string errorSubCategoryResourceName,
            XmlNode xmlNode,
            string resourceName,
            params object[] args
        )
        {
#if DEBUG
            if (errorSubCategoryResourceName != null)
            {
                ResourceUtilities.VerifyResourceStringExists(errorSubCategoryResourceName);
            }

            ResourceUtilities.VerifyResourceStringExists(resourceName);
#endif
            string errorSubCategory = null;

            if (errorSubCategoryResourceName != null)
            {
                errorSubCategory = AssemblyResources.GetString(errorSubCategoryResourceName);
            }

            string errorCode;
            string helpKeyword;
            string message = ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, resourceName, args);

            throw new InvalidProjectFileException(xmlNode, message, errorSubCategory, errorCode, helpKeyword);
        }
    }
}
