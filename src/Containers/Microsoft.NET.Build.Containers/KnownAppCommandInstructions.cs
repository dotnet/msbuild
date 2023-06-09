// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

internal static class KnownAppCommandInstructions
{
    public const string DefaultArgs = nameof(DefaultArgs);
    public const string Entrypoint = nameof(Entrypoint);
    public const string None = nameof(None);

    public static readonly string[] SupportedAppCommandInstructions = new [] { Entrypoint, DefaultArgs, None };
}
