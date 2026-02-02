// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains methods that are useful for error checking and validation.
    /// </summary>
    internal static class ErrorUtilities
    {
        /// <summary>
        /// Throws InternalErrorException.
        /// This is only for situations that would mean that there is a bug in MSBuild itself.
        /// </summary>
        [DoesNotReturn]
        internal static void ThrowInternalError(string message, params object?[]? args)
        {
            throw new InternalErrorException(ResourceUtilities.FormatString(message, args));
        }

        /// <summary>
        /// Throws InternalErrorException.
        /// This is only for situations that would mean that there is a bug in MSBuild itself.
        /// </summary>
        [DoesNotReturn]
        internal static void ThrowInternalError(string message, Exception? innerException, params object?[]? args)
        {
            throw new InternalErrorException(ResourceUtilities.FormatString(message, args), innerException);
        }

        /// <summary>
        /// Throws InternalErrorException.
        /// Indicates the code path followed should not have been possible.
        /// This is only for situations that would mean that there is a bug in MSBuild itself.
        /// </summary>
        [DoesNotReturn]
        internal static void ThrowInternalErrorUnreachable()
        {
            throw new InternalErrorException("Unreachable?");
        }

        /// <summary>
        /// Helper to throw an InternalErrorException when the specified parameter is null.
        /// This should be used ONLY if this would indicate a bug in MSBuild rather than
        /// anything caused by user action.
        /// </summary>
        /// <param name="parameter">The value of the argument.</param>
        /// <param name="parameterName">Parameter that should not be null</param>
        internal static void VerifyThrowInternalNull([NotNull] object? parameter, [CallerArgumentExpression(nameof(parameter))] string? parameterName = null)
        {
            if (parameter is null)
            {
                ThrowInternalError("{0} unexpectedly null", parameterName);
            }
        }

        /// <summary>
        /// Helper to throw an InternalErrorException when the specified parameter is null or zero length.
        /// This should be used ONLY if this would indicate a bug in MSBuild rather than
        /// anything caused by user action.
        /// </summary>
        /// <param name="parameterValue">The value of the argument.</param>
        /// <param name="parameterName">Parameter that should not be null or zero length</param>
        internal static void VerifyThrowInternalLength([NotNull] string? parameterValue, [CallerArgumentExpression(nameof(parameterValue))] string? parameterName = null)
        {
            VerifyThrowInternalNull(parameterValue, parameterName);

            if (parameterValue.Length == 0)
            {
                ThrowInternalError("{0} unexpectedly empty", parameterName);
            }
        }

        /// <summary>
        /// This method should be used in places where one would normally put
        /// an "assert". It should be used to validate that our assumptions are
        /// true, where false would indicate that there must be a bug in our
        /// code somewhere. This should not be used to throw errors based on bad
        /// user input or anything that the user did wrong.
        /// </summary>
        internal static void VerifyThrow([DoesNotReturnIf(false)] bool condition, string unformattedMessage)
        {
            if (!condition)
            {
                ThrowInternalError(unformattedMessage, null, null);
            }
        }

        /// <summary>
        /// Overload for one string format argument.
        /// </summary>
        internal static void VerifyThrow([DoesNotReturnIf(false)] bool condition, string unformattedMessage, object arg0)
        {
            if (!condition)
            {
                ThrowInternalError(unformattedMessage, arg0);
            }
        }

        /// <summary>
        /// Throws an InvalidOperationException with the specified resource string
        /// </summary>
        /// <param name="resourceName">Resource to use in the exception</param>
        /// <param name="args">Formatting args.</param>
        [DoesNotReturn]
        internal static void ThrowInvalidOperation(string resourceName, params object?[]? args)
        {
            throw new InvalidOperationException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword(resourceName, args));
        }

        /// <summary>
        /// Throws an ArgumentException that can include an inner exception.
        ///
        /// PERF WARNING: calling a method that takes a variable number of arguments
        /// is expensive, because memory is allocated for the array of arguments -- do
        /// not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <remarks>
        /// This method is thread-safe.
        /// </remarks>
        /// <param name="innerException">Can be null.</param>
        /// <param name="resourceName"></param>
        /// <param name="args"></param>
        [DoesNotReturn]
        internal static void ThrowArgument(Exception? innerException, string resourceName, params object?[]? args)
        {
            throw new ArgumentException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword(resourceName, args), innerException);
        }

        /// <summary>
        /// Overload for one string format argument.
        /// </summary>
        internal static void VerifyThrowArgument([DoesNotReturnIf(false)] bool condition, string resourceName, object arg0)
        {
            VerifyThrowArgument(condition, null, resourceName, arg0);
        }

        /// <summary>
        /// Overload for one string format argument.
        /// </summary>
        internal static void VerifyThrowArgument([DoesNotReturnIf(false)] bool condition, Exception? innerException, string resourceName, object arg0)
        {
            ResourceUtilities.VerifyResourceStringExists(resourceName);

            if (!condition)
            {
                ThrowArgument(innerException, resourceName, arg0);
            }
        }

        /// <summary>
        /// Throws an ArgumentNullException if the given string parameter is null
        /// and ArgumentException if it has zero length.
        /// </summary>
        internal static void VerifyThrowArgumentLength([NotNull] string? parameter, [CallerArgumentExpression(nameof(parameter))] string? parameterName = null)
        {
            VerifyThrowArgumentNull(parameter, parameterName);

            if (parameter.Length == 0)
            {
                ThrowArgumentLength(parameterName);
            }
        }

        [DoesNotReturn]
        private static void ThrowArgumentLength(string? parameterName)
        {
            throw new ArgumentException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("Shared.ParameterCannotHaveZeroLength", parameterName));
        }

        /// <summary>
        /// Throws an ArgumentNullException if the given parameter is null.
        /// </summary>
        internal static void VerifyThrowArgumentNull([NotNull] object? parameter, [CallerArgumentExpression(nameof(parameter))] string? parameterName = null)
        {
            VerifyThrowArgumentNull(parameter, parameterName, "Shared.ParameterCannotBeNull");
        }

        /// <summary>
        /// Throws an ArgumentNullException if the given parameter is null.
        /// </summary>
        internal static void VerifyThrowArgumentNull([NotNull] object? parameter, string? parameterName, string resourceName)
        {
            ResourceUtilities.VerifyResourceStringExists(resourceName);
            if (parameter is null)
            {
                ThrowArgumentNull(parameterName, resourceName);
            }
        }

        [DoesNotReturn]
        internal static void ThrowArgumentNull(string? parameterName, string resourceName)
        {
            // Most ArgumentNullException overloads append its own rather clunky multi-line message. So use the one overload that doesn't.
            throw new ArgumentNullException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword(resourceName, parameterName), (Exception?)null);
        }
    }
}
