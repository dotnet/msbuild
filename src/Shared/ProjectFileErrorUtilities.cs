// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains methods that are useful for error checking and validation of project files.
    /// </summary>
    internal static class ProjectFileErrorUtilities
    {
        /// <summary>
        /// This method is used to flag errors in the project file being processed. Do NOT use this method in place of
        /// ErrorUtilities.VerifyThrow(), because ErrorUtilities.VerifyThrow() is used to flag internal/programming errors.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <param name="projectFile">The invalid project file.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="args">Extra arguments for formatting the error message.</param>
        internal static void ThrowInvalidProjectFile(
            BuildEventFileInfo projectFile,
            string resourceName,
            params object[] args)
        {
            ThrowInvalidProjectFile(null, projectFile, resourceName, args);
        }

        /// <summary>
        /// This method is used to flag errors in the project file being processed. Do NOT use this method in place of
        /// ErrorUtilities.VerifyThrow(), because ErrorUtilities.VerifyThrow() is used to flag internal/programming errors.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <param name="projectFile">The invalid project file.</param>
        /// <param name="innerException">Any inner exception. May be null.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="args">Extra arguments for formatting the error message.</param>
        internal static void ThrowInvalidProjectFile(
            BuildEventFileInfo projectFile,
            Exception innerException,
            string resourceName,
            params object[] args)
        {
            VerifyThrowInvalidProjectFile(false, null, projectFile, innerException, resourceName, args);
        }

        /// <summary>
        /// This method is used to flag errors in the project file being processed. Do NOT use this method in place of
        /// ErrorUtilities.VerifyThrow(), because ErrorUtilities.VerifyThrow() is used to flag internal/programming errors.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <param name="condition">The condition to check.</param>
        /// <param name="projectFile">The invalid project file.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="args">Extra arguments for formatting the error message.</param>
        internal static void VerifyThrowInvalidProjectFile(
            bool condition,
            BuildEventFileInfo projectFile,
            string resourceName,
            params object[] args)
        {
            VerifyThrowInvalidProjectFile(condition, null, projectFile, resourceName, args);
        }

        /// <summary>
        /// This method is used to flag errors in the project file being processed. Do NOT use this method in place of
        /// ErrorUtilities.VerifyThrow(), because ErrorUtilities.VerifyThrow() is used to flag internal/programming errors.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <param name="errorSubCategoryResourceName">The resource string for the error sub-category (can be null).</param>
        /// <param name="projectFile">The invalid project file.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="args">Extra arguments for formatting the error message.</param>
        internal static void ThrowInvalidProjectFile(
            string errorSubCategoryResourceName,
            BuildEventFileInfo projectFile,
            string resourceName,
            params object[] args)
        {
            VerifyThrowInvalidProjectFile(false, errorSubCategoryResourceName, projectFile, null, resourceName, args);
        }

        /// <summary>
        /// This method is used to flag errors in the project file being processed. Do NOT use this method in place of
        /// ErrorUtilities.VerifyThrow(), because ErrorUtilities.VerifyThrow() is used to flag internal/programming errors.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <param name="condition">The condition to check.</param>
        /// <param name="errorSubCategoryResourceName">The resource string for the error sub-category (can be null).</param>
        /// <param name="projectFile">The invalid project file.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="args">Extra arguments for formatting the error message.</param>
        internal static void VerifyThrowInvalidProjectFile(
            bool condition,
            string errorSubCategoryResourceName,
            BuildEventFileInfo projectFile,
            string resourceName,
            params object[] args)
        {
            VerifyThrowInvalidProjectFile(condition, errorSubCategoryResourceName, projectFile, null, resourceName, args);
        }

        /// <summary>
        /// This method is used to flag errors in the project file being processed. Do NOT use this method in place of
        /// ErrorUtilities.VerifyThrow(), because ErrorUtilities.VerifyThrow() is used to flag internal/programming errors.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <param name="condition">The condition to check.</param>
        /// <param name="errorSubCategoryResourceName">The resource string for the error sub-category (can be null).</param>
        /// <param name="projectFile">The invalid project file.</param>
        /// <param name="innerException">The inner <see cref="Exception"/>.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="args">Extra arguments for formatting the error message.</param>
        internal static void VerifyThrowInvalidProjectFile(
            bool condition,
            string errorSubCategoryResourceName,
            BuildEventFileInfo projectFile,
            Exception innerException,
            string resourceName,
            params object[] args)
        {
            ErrorUtilities.VerifyThrow(projectFile != null, "Must specify the invalid project file. If project file is not available, use VerifyThrowInvalidProject() and pass in the XML node instead.");

#if DEBUG
            if (errorSubCategoryResourceName != null)
            {
                ResourceUtilities.VerifyResourceStringExists(errorSubCategoryResourceName);
            }

            ResourceUtilities.VerifyResourceStringExists(resourceName);
#endif
            if (!condition)
            {
                string errorSubCategory = errorSubCategoryResourceName is null ? null : AssemblyResources.GetString(errorSubCategoryResourceName);
                string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out string errorCode, out string helpKeyword, resourceName, args);

                throw new InvalidProjectFileException(projectFile.File, projectFile.Line, projectFile.Column, projectFile.EndLine, projectFile.EndColumn, message, errorSubCategory, errorCode, helpKeyword, innerException);
            }
        }
    }
}
