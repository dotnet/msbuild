// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Internal;
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
        /// Assumed.True(), because Assumed.True() is used to flag internal/programming errors.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <param name="location">The location in the invalid project file.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="args">Extra arguments for formatting the error message.</param>
        internal static void ThrowInvalidProjectFile(
            IElementLocation location,
            string resourceName,
            params object[] args)
        {
            ThrowInvalidProjectFile(DiagnosticSubcategory.None, location, resourceName, args);
        }

        /// <summary>
        /// This method is used to flag errors in the project file being processed. Do NOT use this method in place of
        /// Assumed.True(), because Assumed.True() is used to flag internal/programming errors.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <param name="location">The location in the invalid project file.</param>
        /// <param name="innerException">Any inner exception. May be null.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="args">Extra arguments for formatting the error message.</param>
        internal static void ThrowInvalidProjectFile(
            IElementLocation location,
            Exception innerException,
            string resourceName,
            params object[] args)
        {
            VerifyThrowInvalidProjectFile(false, DiagnosticSubcategory.None, location, innerException, resourceName, args);
        }

        /// <summary>
        /// This method is used to flag errors in the project file being processed. Do NOT use this method in place of
        /// Assumed.True(), because Assumed.True() is used to flag internal/programming errors.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <param name="condition">The condition to check.</param>
        /// <param name="location">The location in the invalid project file.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="args">Extra arguments for formatting the error message.</param>
        internal static void VerifyThrowInvalidProjectFile(
            bool condition,
            IElementLocation location,
            string resourceName,
            params object[] args)
        {
            VerifyThrowInvalidProjectFile(condition, DiagnosticSubcategory.None, location, resourceName, args);
        }

        /// <summary>
        /// This method is used to flag errors in the project file being processed. Do NOT use this method in place of
        /// Assumed.True(), because Assumed.True() is used to flag internal/programming errors.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
        /// <param name="location">The location in the invalid project file.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="args">Extra arguments for formatting the error message.</param>
        internal static void ThrowInvalidProjectFile(
            DiagnosticSubcategory subcategory,
            IElementLocation location,
            string resourceName,
            params object[] args)
        {
            VerifyThrowInvalidProjectFile(false, subcategory, location, null, resourceName, args);
        }

        /// <summary>
        /// This method is used to flag errors in the project file being processed. Do NOT use this method in place of
        /// Assumed.True(), because Assumed.True() is used to flag internal/programming errors.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <param name="condition">The condition to check.</param>
        /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
        /// <param name="location">The location in the invalid project file.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="args">Extra arguments for formatting the error message.</param>
        internal static void VerifyThrowInvalidProjectFile(
            bool condition,
            DiagnosticSubcategory subcategory,
            IElementLocation location,
            string resourceName,
            params object[] args)
        {
            VerifyThrowInvalidProjectFile(condition, subcategory, location, null, resourceName, args);
        }

        /// <summary>
        /// This method is used to flag errors in the project file being processed. Do NOT use this method in place of
        /// Assumed.True(), because Assumed.True() is used to flag internal/programming errors.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <param name="condition">The condition to check.</param>
        /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
        /// <param name="location">The location in the invalid project file.</param>
        /// <param name="innerException">The inner <see cref="Exception"/>.</param>
        /// <param name="resourceName">The resource string for the error message.</param>
        /// <param name="args">Extra arguments for formatting the error message.</param>
        internal static void VerifyThrowInvalidProjectFile(
            bool condition,
            DiagnosticSubcategory subcategory,
            IElementLocation location,
            Exception innerException,
            string resourceName,
            params object[] args)
        {
            Assumed.NotNull(location, "Must specify the invalid project file. If project file is not available, use VerifyThrowInvalidProject() and pass in the XML node instead.");

#if DEBUG
            ResourceUtilities.VerifyResourceStringExists(resourceName);
#endif
            if (!condition)
            {
                string subcategoryText = subcategory.GetDisplayString();
                string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(out string errorCode, out string helpKeyword, resourceName, args);

                throw new InvalidProjectFileException(
                    location.File,
                    location.Line,
                    location.Column,
                    endLineNumber: 0,
                    endColumnNumber: 0,
                    message,
                    subcategoryText,
                    errorCode,
                    helpKeyword,
                    innerException);
            }
        }
    }
}
