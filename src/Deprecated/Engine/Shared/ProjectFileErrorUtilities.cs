// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// This class contains methods that are useful for error checking and validation of project files.
    /// </summary>
    /// <owner>SumedhK</owner>
    internal static class ProjectFileErrorUtilities
    {
        /// <summary>
        /// This method is used to flag errors in the project file being processed. Do NOT use this method in place of
        /// ErrorUtilities.VerifyThrow(), because ErrorUtilities.VerifyThrow() is used to flag internal/programming errors.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <owner>SumedhK</owner>
        /// <param name="condition">The condition to check.</param>
        /// <param name="projectFile">The invalid project file.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="args">Extra arguments for formatting the error message.</param>
        internal static void VerifyThrowInvalidProjectFile
        (
            bool condition,
            BuildEventFileInfo projectFile,
            string resourceName,
            params object[] args
        )
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
        /// <owner>SumedhK</owner>
        /// <param name="condition">The condition to check.</param>
        /// <param name="errorSubCategoryResourceName">The resource string for the error sub-category (can be null).</param>
        /// <param name="projectFile">The invalid project file.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="args">Extra arguments for formatting the error message.</param>
        internal static void VerifyThrowInvalidProjectFile
        (
            bool condition,
            string errorSubCategoryResourceName,
            BuildEventFileInfo projectFile,
            string resourceName,
            params object[] args
        )
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
                string errorSubCategory = null;

                if (errorSubCategoryResourceName != null)
                {
                    errorSubCategory = AssemblyResources.GetString(errorSubCategoryResourceName);
                }

                string errorCode;
                string helpKeyword;
                string message = ResourceUtilities.FormatResourceString(out errorCode, out helpKeyword, resourceName, args);

                throw new InvalidProjectFileException(projectFile.File, projectFile.Line, projectFile.Column, projectFile.EndLine, projectFile.EndColumn, message, errorSubCategory, errorCode, helpKeyword);
            }
        }
    }
}
