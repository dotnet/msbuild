// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable
using System.Collections.Generic;

namespace Microsoft.Build.Logging;

internal class ExtendedDataFields
{
    public string ExtendedType { get; set; }
    public IDictionary<string, string> ExtendedMetadata { get; set; }
    public string ExtendedData { get; set; }
}
