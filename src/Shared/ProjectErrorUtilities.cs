﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains methods that are useful for error checking and
    /// validation of project files.
    /// </summary>
    /// <remarks>
    /// FUTURE: This class could except an optional inner exception to put in the
    /// InvalidProjectFileException, which could make debugging a host easier in some circumstances.
    /// </remarks>
    internal static class ProjectErrorUtilities
    {
        /// <summary>
        /// This method is used to flag errors in the project file being processed.
        /// Do NOT use this method in place of ErrorUtilities.VerifyThrow(), because
        /// ErrorUtilities.VerifyThrow() is used to flag internal/programming errors.
        /// </summary>
        /// <param name="condition">The condition to check.</param>
        /// <param name="elementLocation">The <see cref="IElementLocation"/> of the element.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        internal static void VerifyThrowInvalidProject(bool condition, IElementLocation elementLocation, string resourceName)
        {
            VerifyThrowInvalidProject(condition, null, elementLocation, resourceName);
        }

        /// <summary>
        /// Overload for one string format argument.
        /// </summary>
        /// <param name="elementLocation">The <see cref="IElementLocation"/> of the element.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        internal static void ThrowInvalidProject<T1>(IElementLocation elementLocation, string resourceName, T1 arg0)
        {
            ThrowInvalidProject(null, elementLocation, resourceName, arg0);
        }

        /// <summary>
        /// Overload for one string format argument.
        /// </summary>
        /// <param name="condition">The condition to check.</param>
        /// <param name="elementLocation">The <see cref="IElementLocation"/> of the element.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        internal static void VerifyThrowInvalidProject<T1>(bool condition, IElementLocation elementLocation, string resourceName, T1 arg0)
        {
            VerifyThrowInvalidProject(condition, null, elementLocation, resourceName, arg0);
        }

        /// <summary>
        /// Overload for two string format arguments.
        /// </summary>
        /// <param name="elementLocation">The <see cref="IElementLocation"/> of the element.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        internal static void ThrowInvalidProject<T1, T2>(IElementLocation elementLocation, string resourceName, T1 arg0, T2 arg1)
        {
            ThrowInvalidProject(null, elementLocation, resourceName, arg0, arg1);
        }

        /// <summary>
        /// Overload for three string format arguments.
        /// </summary>
        /// <param name="elementLocation">The <see cref="IElementLocation"/> of the element.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        internal static void ThrowInvalidProject<T1, T2, T3>(IElementLocation elementLocation, string resourceName, T1 arg0, T2 arg1, T3 arg2)
        {
            ThrowInvalidProject(null, elementLocation, resourceName, arg0, arg1, arg2);
        }

        /// <summary>
        /// Overload for four string format arguments.
        /// </summary>
        /// <param name="elementLocation">The <see cref="IElementLocation"/> of the element.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        internal static void ThrowInvalidProject<T1, T2, T3, T4>(IElementLocation elementLocation, string resourceName, T1 arg0, T2 arg1, T3 arg2, T4 arg3)
        {
            ThrowInvalidProject(null, elementLocation, resourceName, arg0, arg1, arg2, arg3);
        }

        /// <summary>
        /// Overload for if there are more than four string format arguments.
        /// </summary>
        /// <param name="elementLocation">The <see cref="IElementLocation"/> of the element.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="args"></param>
        internal static void ThrowInvalidProject(IElementLocation elementLocation, string resourceName, params object[] args)
        {
            ThrowInvalidProject(null, elementLocation, resourceName, args);
        }

        /// <summary>
        /// Overload for two string format arguments.
        /// </summary>
        /// <param name="condition">The condition to check.</param>
        /// <param name="elementLocation">The <see cref="IElementLocation"/> of the element.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        internal static void VerifyThrowInvalidProject<T1, T2>(bool condition, IElementLocation elementLocation, string resourceName, T1 arg0, T2 arg1)
        {
            VerifyThrowInvalidProject(condition, null, elementLocation, resourceName, arg0, arg1);
        }

        /// <summary>
        /// Overload for three string format arguments.
        /// </summary>
        /// <param name="condition">The condition to check.</param>
        /// <param name="elementLocation">The <see cref="IElementLocation"/> of the element.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        internal static void VerifyThrowInvalidProject<T1, T2, T3>(bool condition, IElementLocation elementLocation, string resourceName, T1 arg0, T2 arg1, T3 arg2)
        {
            VerifyThrowInvalidProject(condition, null, elementLocation, resourceName, arg0, arg1, arg2);
        }

        /// <summary>
        /// Overload for four string format arguments.
        /// </summary>
        /// <param name="condition">The condition to check.</param>
        /// <param name="elementLocation">The <see cref="IElementLocation"/> of the element.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        internal static void VerifyThrowInvalidProject<T1, T2, T3, T4>(bool condition, IElementLocation elementLocation, string resourceName, T1 arg0, T2 arg1, T3 arg2, T4 arg3)
        {
            VerifyThrowInvalidProject(condition, null, elementLocation, resourceName, arg0, arg1, arg2, arg3);
        }

        /// <summary>
        /// This method is used to flag errors in the project file being processed.
        /// Do NOT use this method in place of ErrorUtilities.VerifyThrow(), because
        /// ErrorUtilities.VerifyThrow() is used to flag internal/programming errors.
        /// </summary>
        /// <param name="condition">The condition to check.</param>
        /// <param name="errorSubCategoryResourceName">The resource string for the
        /// error sub-category (can be null).</param>
        /// <param name="elementLocation">The <see cref="IElementLocation"/> of the element.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        internal static void VerifyThrowInvalidProject(bool condition, string errorSubCategoryResourceName, IElementLocation elementLocation, string resourceName)
        {
            if (!condition)
            {
                ThrowInvalidProject(errorSubCategoryResourceName, elementLocation, resourceName, null);
            }
        }

        /// <summary>
        /// Overload for one string format argument.
        /// </summary>
        /// <param name="condition">The condition to check.</param>
        /// <param name="errorSubCategoryResourceName">The resource string for the
        /// error sub-category (can be null).</param>
        /// <param name="elementLocation">The <see cref="IElementLocation"/> of the element.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        internal static void VerifyThrowInvalidProject<T1>(bool condition, string errorSubCategoryResourceName, IElementLocation elementLocation, string resourceName, T1 arg0)
        {
            if (!condition)
            {
                ThrowInvalidProject(errorSubCategoryResourceName, elementLocation, resourceName, arg0);
            }
        }

        /// <summary>
        /// Overload for two string format arguments.
        /// </summary>
        /// <param name="condition">The condition to check.</param>
        /// <param name="errorSubCategoryResourceName">The resource string for the
        /// error sub-category (can be null).</param>
        /// <param name="elementLocation">The <see cref="IElementLocation"/> of the element.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        internal static void VerifyThrowInvalidProject<T1, T2>(bool condition, string errorSubCategoryResourceName, IElementLocation elementLocation, string resourceName, T1 arg0, T2 arg1)
        {
            if (!condition)
            {
                ThrowInvalidProject(errorSubCategoryResourceName, elementLocation, resourceName, arg0, arg1);
            }
        }

        /// <summary>
        /// Overload for three string format arguments.
        /// </summary>
        /// <param name="condition">The condition to check.</param>
        /// <param name="errorSubCategoryResourceName">The resource string for the
        /// error sub-category (can be null).</param>
        /// <param name="elementLocation">The <see cref="IElementLocation"/> of the element.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        internal static void VerifyThrowInvalidProject<T1, T2, T3>(bool condition, string errorSubCategoryResourceName, IElementLocation elementLocation, string resourceName, T1 arg0, T2 arg1, T3 arg2)
        {
            if (!condition)
            {
                ThrowInvalidProject(errorSubCategoryResourceName, elementLocation, resourceName, arg0, arg1, arg2);
            }
        }

        /// <summary>
        /// Overload for four string format arguments.
        /// </summary>
        /// <param name="condition">The condition to check.</param>
        /// <param name="errorSubCategoryResourceName">The resource string for the
        /// error sub-category (can be null).</param>
        /// <param name="elementLocation">The <see cref="IElementLocation"/> of the element.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        internal static void VerifyThrowInvalidProject<T1, T2, T3, T4>(bool condition, string errorSubCategoryResourceName, IElementLocation elementLocation, string resourceName, T1 arg0, T2 arg1, T3 arg2, T4 arg3)
        {
            if (!condition)
            {
                ThrowInvalidProject(errorSubCategoryResourceName, elementLocation, resourceName, arg0, arg1, arg2, arg3);
            }
        }

        /// <summary>
        /// Throws an InvalidProjectFileException using the given data.
        /// 
        /// PERF WARNING: calling a method that takes a variable number of arguments
        /// is expensive, because memory is allocated for the array of arguments -- do
        /// not call this method repeatedly in performance-critical scenarios
        /// 
        /// </summary>
        /// <param name="errorSubCategoryResourceName">The resource string for the
        /// error sub-category (can be null).</param>
        /// <param name="elementLocation">The <see cref="IElementLocation"/> of the element.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="args">Extra arguments for formatting the error message.</param>
        private static void ThrowInvalidProject(string errorSubCategoryResourceName, IElementLocation elementLocation, string resourceName, params object[] args)
        {
            ErrorUtilities.VerifyThrowInternalNull(elementLocation, nameof(elementLocation));
#if DEBUG
            if (errorSubCategoryResourceName != null)
            {
                ResourceUtilities.VerifyResourceStringExists(errorSubCategoryResourceName);
            }

            ResourceUtilities.VerifyResourceStringExists(resourceName);
#endif
            string errorSubCategory = errorSubCategoryResourceName is null ? null : AssemblyResources.GetString(errorSubCategoryResourceName);

            string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out string errorCode, out string helpKeyword, resourceName, args);

            throw new InvalidProjectFileException(elementLocation.File, elementLocation.Line, elementLocation.Column, 0 /* Unknown end line */, 0 /* Unknown end column */, message, errorSubCategory, errorCode, helpKeyword);
        }
    }
}
