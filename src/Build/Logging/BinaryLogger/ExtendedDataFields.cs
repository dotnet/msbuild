// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Logging;

internal class ExtendedDataFields
{
    public ExtendedDataFields(string extendedType, IDictionary<string, string>? extendedMetadata, string? extendedData)
    {
        ExtendedType = extendedType;
        ExtendedMetadata = extendedMetadata;
        ExtendedData = extendedData;
    }

    public string ExtendedType { get; }
    public IDictionary<string, string?>? ExtendedMetadata { get; }
    public string? ExtendedData { get; }
}
