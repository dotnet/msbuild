// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
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
    /// Helper to throw an InternalErrorException when the specified parameter is not a rooted path.
    /// This should be used ONLY if this would indicate a bug in MSBuild rather than
    /// anything caused by user action.
    /// </summary>
    /// <param name="value">Parameter that should be a rooted path.</param>
    public static void VerifyThrowInternalRooted(string value)
    {
        if (!Path.IsPathRooted(value))
        {
            InternalError.Throw($"{value} unexpectedly not a rooted path");
        }
    }
}
