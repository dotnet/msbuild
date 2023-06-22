// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Framework;

/// <summary>
/// Interface for Extended EventArgs to allow enriching particular events with extended data.
/// Deriving from EventArgs will be deprecated soon and using Extended EventArgs is recommended for custom Event Args.
/// </summary>
public interface IExtendedBuildEventArgs
{
    /// <summary>
    /// Unique string identifying type of extended data so receiver side knows how to interpret, deserialize and handle <see cref="ExtendedData"/>.
    /// </summary>
    string ExtendedType { get; set; }

    /// <summary>
    /// Metadata of <see cref="ExtendedData"/>.
    /// Example usage:
    ///   - data which needed in custom code to properly routing this message without interpreting/deserializing <see cref="ExtendedData"/>.
    ///   - simple extended data can be transferred in form of dictionary key-value per one extended property.
    /// </summary>
    Dictionary<string, string?>? ExtendedMetadata { get; set; }

    /// <summary>
    /// Transparent data as string.
    /// Custom code is responsible to serialize and deserialize this string to structured data - if needed.
    /// Custom code can use any serialization they deem safe - e.g. json for textual data, base64 for binary data...
    /// </summary>
    string? ExtendedData { get; set; }
}
