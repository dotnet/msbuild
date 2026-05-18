// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security;
using System.Threading;
using System.Xml;
using System.Xml.Schema;

namespace Microsoft.Build.Framework;

internal static class ExceptionHandling
{
    /// <summary>
    /// If the given exception is "ignorable under some circumstances" return false.
    /// Otherwise it's "really bad", and return true.
    /// This makes it possible to catch(Exception ex) without catching disasters.
    /// </summary>
    /// <param name="e"> The exception to check. </param>
    /// <returns> True if exception is critical. </returns>
    internal static bool IsCriticalException(Exception e)
    {
        if (e is OutOfMemoryException
         || e is StackOverflowException
         || e is ThreadAbortException
         || e is ThreadInterruptedException
         || e is AccessViolationException
         || e is CriticalTaskException
         || e is InternalErrorException)
        {
            // Ideally we would include NullReferenceException, because it should only ever be thrown by CLR (use ArgumentNullException for arguments)
            // but we should handle it if tasks and loggers throw it.

            // ExecutionEngineException has been deprecated by the CLR
            return true;
        }

        // Check if any critical exceptions
        var aggregateException = e as AggregateException;

        if (aggregateException != null)
        {
            // If the aggregate exception contains a critical exception it is considered a critical exception
            if (aggregateException.InnerExceptions.Any(innerException => IsCriticalException(innerException)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// If the given exception is file IO related or expected return false.
    /// Otherwise, return true.
    /// </summary>
    /// <param name="e">The exception to check.</param>
    /// <returns>True if exception is not IO related or expected otherwise false.</returns>
    internal static bool NotExpectedException(Exception e)
    {
        return !IsIoRelatedException(e);
    }

    /// <summary>
    /// Determine whether the exception is file-IO related.
    /// </summary>
    /// <param name="e">The exception to check.</param>
    /// <returns>True if exception is IO related.</returns>
    internal static bool IsIoRelatedException(Exception e)
    {
        // These all derive from IOException
        //     DirectoryNotFoundException
        //     DriveNotFoundException
        //     EndOfStreamException
        //     FileLoadException
        //     FileNotFoundException
        //     PathTooLongException
        //     PipeException
        return e is UnauthorizedAccessException
               || e is NotSupportedException
               || (e is ArgumentException && !(e is ArgumentNullException))
               || e is SecurityException
               || e is IOException;
    }

    /// <summary> Checks if the exception is an XML one. </summary>
    /// <param name="e"> Exception to check. </param>
    /// <returns> True if exception is related to XML parsing. </returns>
    internal static bool IsXmlException(Exception e)
    {
        return e is XmlException
#if FEATURE_SECURITY_PERMISSIONS
            || e is XmlSyntaxException
#endif
            || e is XmlSchemaException
            || e is UriFormatException; // XmlTextReader for example uses this under the covers
    }

    /// <summary>
    /// If the given exception is file IO related or Xml related return false.
    /// Otherwise, return true.
    /// </summary>
    /// <param name="e">The exception to check.</param>
    internal static bool NotExpectedIoOrXmlException(Exception e)
    {
        if
        (
            IsXmlException(e)
            || !NotExpectedException(e))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// If the given exception is reflection-related return false.
    /// Otherwise, return true.
    /// </summary>
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
            || e is TargetParameterCountException   // thrown when the number of parameters for an invocation does not match the number expected
            || e is InvalidCastException
            || e is AmbiguousMatchException         // thrown when binding to a member results in more than one member matching the binding criteria
            || e is CustomAttributeFormatException  // thrown if a custom attribute on a data type is formatted incorrectly
            || e is InvalidFilterCriteriaException  // thrown in FindMembers when the filter criteria is not valid for the type of filter you are using
            || e is TargetException                 // thrown when an attempt is made to invoke a non-static method on a null object.  This may occur because the caller does not
                                                    //     have access to the member, or because the target does not define the member, and so on.
            || e is MissingFieldException           // thrown when code in a dependent assembly attempts to access a missing field in an assembly that was modified.
            || !NotExpectedException(e))             // Reflection can throw IO exceptions if the assembly cannot be opened
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Serialization has been observed to throw TypeLoadException as
    /// well as SerializationException and IO exceptions. (Obviously
    /// it has to do reflection but it ought to be wrapping the exceptions.)
    /// </summary>
    internal static bool NotExpectedSerializationException(Exception e)
    {
        if
        (
            e is SerializationException ||
            !NotExpectedReflectionException(e))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns false if this is a known exception thrown by the registry API.
    /// </summary>
    internal static bool NotExpectedRegistryException(Exception e)
    {
        if (e is SecurityException
         || e is UnauthorizedAccessException
         || e is IOException
         || e is ObjectDisposedException
         || e is ArgumentException)
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
