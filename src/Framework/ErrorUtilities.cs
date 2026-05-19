// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework.Utilities;

namespace Microsoft.Build.Framework;

// TODO: this should be unified with Shared\ErrorUtilities.cs, but it is hard to untangle everything
//       because some of the errors there will use localized resources from different assemblies,
//       which won't be referenceable in Framework.

internal static class FrameworkErrorUtilities
{
    private static readonly bool s_enableMSBuildDebugTracing = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDENABLEDEBUGTRACING"));

    public static void DebugTraceMessage(string category, string message)
    {
        if (s_enableMSBuildDebugTracing)
        {
            Trace.WriteLine(message, category);
        }
    }

    public static void DebugTraceMessage(string category, ref DebugTraceInterpolatedStringHandler handler)
    {
        if (s_enableMSBuildDebugTracing)
        {
            Trace.WriteLine(handler.GetFormattedText(), category);
        }
    }

    /// <summary>
    ///  Interpolated string handler used by <see cref="DebugTraceMessage(string, ref DebugTraceInterpolatedStringHandler)"/>
    ///  to defer string formatting unless tracing is enabled.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct DebugTraceInterpolatedStringHandler
    {
        private StringBuilderHelper _builder;

        public DebugTraceInterpolatedStringHandler(int literalLength, int formattedCount, out bool isEnabled)
        {
            isEnabled = s_enableMSBuildDebugTracing;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    /// This method should be used in places where one would normally put
    /// an "assert". It should be used to validate that our assumptions are
    /// true, where false would indicate that there must be a bug in our
    /// code somewhere. This should not be used to throw errors based on bad
    /// user input or anything that the user did wrong.
    /// </summary>
    public static void VerifyThrow([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            ThrowInternalError(message);
        }
    }

    public static void VerifyThrow(
        [DoesNotReturnIf(false)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] ref IsTrueInterpolatedStringHandler handler)
    {
        if (!condition)
        {
            ThrowInternalError(handler.GetFormattedText());
        }
    }

    [InterpolatedStringHandler]
    public ref struct IsTrueInterpolatedStringHandler
    {
        private StringBuilderHelper _builder;

        public IsTrueInterpolatedStringHandler(int literalLength, int formattedCount, bool condition, out bool isEnabled)
        {
            isEnabled = !condition;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }

    /// <summary>
    /// Helper to throw an InternalErrorException when the specified parameter is null.
    /// This should be used ONLY if this would indicate a bug in MSBuild rather than
    /// anything caused by user action.
    /// </summary>
    /// <param name="parameter">The value of the argument.</param>
    /// <param name="parameterName">Parameter that should not be null.</param>
    public static void VerifyThrowInternalNull(
        [NotNull] object? parameter,
        [CallerArgumentExpression(nameof(parameter))] string? parameterName = null)
    {
        if (parameter is null)
        {
            ThrowInternalError($"{parameterName} unexpectedly null");
        }
    }

    /// <summary>
    /// Helper to throw an InternalErrorException when the specified parameter is null or zero length.
    /// This should be used ONLY if this would indicate a bug in MSBuild rather than
    /// anything caused by user action.
    /// </summary>
    /// <param name="parameterValue">The value of the argument.</param>
    /// <param name="parameterName">Parameter that should not be null or zero length</param>
    public static void VerifyThrowInternalLength(
        [NotNull] string? parameterValue,
        [CallerArgumentExpression(nameof(parameterValue))] string? parameterName = null)
    {
        VerifyThrowInternalNull(parameterValue, parameterName);

        if (parameterValue.Length == 0)
        {
            ThrowInternalError($"{parameterName} unexpectedly empty");
        }
    }

    public static void VerifyThrowInternalLength<T>(
        [NotNull] T[]? parameterValue,
        [CallerArgumentExpression(nameof(parameterValue))] string? parameterName = null)
    {
        VerifyThrowInternalNull(parameterValue, parameterName);

        if (parameterValue.Length == 0)
        {
            ThrowInternalError($"{parameterName} unexpectedly empty");
        }
    }

    /// <summary>
    /// Helper to throw an InternalErrorException when the specified parameter is not a rooted path.
    /// This should be used ONLY if this would indicate a bug in MSBuild rather than
    /// anything caused by user action.
    /// </summary>
    /// <param name="value">Parameter that should be a rooted path.</param>
    public static void VerifyThrowInternalRooted(string value)
    {
        if (!Path.IsPathRooted(value))
        {
            ThrowInternalError($"{value} unexpectedly not a rooted path");
        }
    }

    /// <summary>
    /// Throws InternalErrorException.
    /// This is only for situations that would mean that there is a bug in MSBuild itself.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowInternalError(string message)
        => throw new InternalErrorException(message);

    /// <summary>
    /// Throws InternalErrorException.
    /// This is only for situations that would mean that there is a bug in MSBuild itself.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowInternalError(ref UnconditionalInterpolatedStringHandler handler)
        => ThrowInternalError(handler.GetFormattedText());

    /// <summary>
    /// Throws InternalErrorException.
    /// This is only for situations that would mean that there is a bug in MSBuild itself.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowInternalError(string message, Exception innerException)
        => throw new InternalErrorException(message, innerException);

    /// <summary>
    /// Throws InternalErrorException.
    /// This is only for situations that would mean that there is a bug in MSBuild itself.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowInternalError(ref UnconditionalInterpolatedStringHandler handler, Exception innerException)
        => ThrowInternalError(handler.GetFormattedText(), innerException);

    /// <summary>
    /// Throws InternalErrorException.
    /// Indicates the code path followed should not have been possible.
    /// This is only for situations that would mean that there is a bug in MSBuild itself.
    /// </summary>
    [DoesNotReturn]
    public static void ThrowInternalErrorUnreachable()
        => ThrowInternalError("Unreachable?");

    /// <summary>
    /// Throws InternalErrorException.
    /// Indicates the code path followed should not have been possible.
    /// This is only for situations that would mean that there is a bug in MSBuild itself.
    /// </summary>
    public static void VerifyThrowInternalErrorUnreachable([DoesNotReturnIf(false)] bool condition)
    {
        if (!condition)
        {
            ThrowInternalErrorUnreachable();
        }
    }
}
