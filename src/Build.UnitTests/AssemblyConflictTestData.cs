// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.Build.UnitTests;

internal static class AssemblyConflictTestData
{
    internal static AssemblyConflictMessageFormats MessageFormats { get; } = new(
        "There was a conflict between \"{0}\" and \"{1}\".",
        "\"{0}\" was chosen because it had a higher version.",
        "\"{0}\" was chosen because it was primary and \"{1}\" was not.",
        "MSB3243: No way to resolve conflict between \"{0}\" and \"{1}\". Choosing \"{0}\" arbitrarily.",
        "References which depend on \"{0}\" [{1}].",
        "References which depend on or have been unified to \"{0}\" [{1}].",
        "Unresolved primary reference with an item include of \"{0}\".",
        "Project file item includes which caused reference \"{0}\".",
        "Found conflicts between different versions of \"{0}\" that could not be resolved.\n{1}");
}
