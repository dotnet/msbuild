// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework.Utilities;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Debug tracing interface for coordinator communication diagnostics.
///  Output is gated on the MSBUILDDEBUGCOMM environment variable and is
///  intended for development-time troubleshooting, not user-visible logging.
/// </summary>
internal interface ICoordinatorDebugOutput
{
    bool IsEnabled { get; }

    void WriteLine(string message);

    void WriteLine([InterpolatedStringHandlerArgument("")] ref WriteLineInterpolatedStringHandler handler);

    [InterpolatedStringHandler]
    ref struct WriteLineInterpolatedStringHandler
    {
        private StringBuilderHelper _builder;

        public WriteLineInterpolatedStringHandler(int literalLength, int formattedCount, ICoordinatorDebugOutput output, out bool isEnabled)
        {
            isEnabled = output.IsEnabled;
            _builder = isEnabled ? new(literalLength) : default;
        }

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public readonly string GetFormattedText()
            => _builder.GetFormattedText();
    }
}
