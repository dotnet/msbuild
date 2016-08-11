// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Security;
using System.Diagnostics;
using System.Reflection;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// Utility methods for classifying and handling exceptions.
    /// </summary>
    /// <owner>JomoF</owner>
    internal static class ExceptionHandling
    {
        /// <summary>
        /// If the given exception is "ignorable under some circumstances" return false.
        /// Otherwise it's "really bad", and return true.
        /// This makes it possible to catch(Exception ex) without catching disasters.
        /// </summary>
        internal static bool IsCriticalException(Exception e)
        {
            if
            (
                e is StackOverflowException
                || e is OutOfMemoryException
                || e is AccessViolationException
                // ExecutionEngineException has been deprecated by the CLR
            )
            {
                return true;
            }

            return false;
        }


        /// <summary>
        /// If the given exception is file IO related or expected return false.
        /// Otherwise, return true.
        /// </summary>
        /// <param name="e">The exception to check.</param>
        internal static bool NotExpectedException(Exception e)
        {
            if
            (
                e is UnauthorizedAccessException
                || e is ArgumentNullException
                || e is PathTooLongException
                || e is DirectoryNotFoundException
                || e is NotSupportedException
                || e is ArgumentException
                || e is SecurityException
                || e is IOException
            )
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// If the given exception is reflection-related return the exception -- or in the case
        /// of TargetInvocationException, return the inner exception.
        /// Otherwise, return null.
        /// </summary>
        /// <remarks>
        /// The reason we return the exception rather than a bool is that some exceptions need to
        /// be "unwrapped" and we want this method to handle that for us.
        /// </remarks>
        /// <param name="e">The exception to check.</param>
        internal static bool NotExpectedReflectionException(Exception e)
        {
            // We are explicitly not handling TargetInvocationException. Those are just wrappers around
            // exceptions thrown by the called code (such as a task or logger) which callers will typically
            // want to treat differently.

            if
            (
                e is TypeLoadException                  // thrown when the common language runtime cannot find the assembly, the type within the assembly, or cannot load the type
                || e is MethodAccessException           // thrown when a class member is not found or access to the member is not permitted
                || e is MissingMethodException          // thrown when code in a dependent assembly attempts to access a missing method in an assembly that was modified
                || e is MemberAccessException           // thrown when a class member is not found or access to the member is not permitted
                || e is BadImageFormatException         // thrown when the file image of a DLL or an executable program is invalid
                || e is ReflectionTypeLoadException     // thrown by the Module.GetTypes method if any of the classes in a module cannot be loaded
                || e is CustomAttributeFormatException  // thrown if a custom attribute on a data type is formatted incorrectly
                || e is TargetParameterCountException   // thrown when the number of parameters for an invocation does not match the number expected
                || e is InvalidCastException
                || e is AmbiguousMatchException         // thrown when binding to a member results in more than one member matching the binding criteria
                || e is InvalidFilterCriteriaException  // thrown in FindMembers when the filter criteria is not valid for the type of filter you are using
                || e is TargetException                 // thrown when an attempt is made to invoke a non-static method on a null object.  This may occur because the caller does not 
                                                        //     have access to the member, or because the target does not define the member, and so on.
                || e is MissingFieldException           // thrown when code in a dependent assembly attempts to access a missing field in an assembly that was modified.
                || !NotExpectedException(e)             // Reflection can throw IO exceptions if the assembly cannot be opened

            )
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns false if this is a known exception thrown by function evaluation
        /// </summary>
        internal static bool NotExpectedFunctionException(Exception e)
        {
            if (e is InvalidCastException
             || e is ArgumentNullException
             || e is FormatException
             || e is InvalidOperationException
             || !NotExpectedReflectionException(e))
            {
                return false;
            }

            return true;
        }
    }
}
