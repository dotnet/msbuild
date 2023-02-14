// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

public static class KnownDaemonTypes
{
    public const string Docker = nameof(Docker);
    public static readonly string[] SupportedLocalDaemonTypes = new [] { Docker };
}
