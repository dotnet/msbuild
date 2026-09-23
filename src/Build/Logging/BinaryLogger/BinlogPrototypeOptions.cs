// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Logging;

[Flags]
internal enum BinlogPrototypeOptions
{
    None = 0,
    MetadataCache = 1,
    FastCompression = 2,
    AsyncCompression = 4
}

internal static class BinlogPrototypeConfiguration
{
    internal const string EnvironmentVariable = "MSBUILDBINLOGPROTOTYPE";

    internal static BinlogPrototypeOptions Read()
    {
        string? value = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrEmpty(value))
        {
            return BinlogPrototypeOptions.None;
        }

        const BinlogPrototypeOptions all = BinlogPrototypeOptions.MetadataCache |
            BinlogPrototypeOptions.FastCompression | BinlogPrototypeOptions.AsyncCompression;
        if (!Enum.TryParse(value, ignoreCase: true, out BinlogPrototypeOptions options) || (options & ~all) != 0)
        {
            throw new ArgumentOutOfRangeException(EnvironmentVariable, value, null);
        }

        return options;
    }
}
