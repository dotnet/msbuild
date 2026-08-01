// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Framework;

/// <summary>
/// Exposes the invariant template and ordered values associated with a structured build event.
/// </summary>
/// <remarks>
/// The structured state is intentionally independent from <see cref="BuildEventArgs.Message"/>.
/// Loggers often aggregate or filter by template without needing the complete display string, and
/// <see cref="BuildEventArgs.Message"/> may be materialized lazily. Keeping this contract separate
/// also prevents lazy message materialization from discarding machine-readable names and values.
/// </remarks>
public interface IStructuredBuildEventArgs
{
    /// <summary>
    /// Gets the invariant message template whose named holes correspond positionally to
    /// <see cref="StructuredValues"/>.
    /// </summary>
    /// <remarks>
    /// This is the structured-logging equivalent of Microsoft.Extensions.Logging's
    /// <c>{OriginalFormat}</c> convention. It is nullable because ordinary extended events do not
    /// necessarily carry structured state.
    /// </remarks>
    string? OriginalFormat { get; }

    /// <summary>
    /// Gets the ordered, uniquely named values captured for the message template.
    /// Values are formatted using the invariant culture before they cross a process boundary.
    /// </summary>
    /// <remarks>
    /// An ordered list preserves the relationship between template occurrences and values without
    /// depending on dictionary enumeration behavior. Names are unique so consumers may safely build
    /// a lookup when order is not relevant. Null values remain null rather than being conflated with
    /// an empty string.
    /// </remarks>
    IReadOnlyList<KeyValuePair<string, string?>>? StructuredValues { get; }
}
