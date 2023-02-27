// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Represents a diagnostic message that could be parsed by VS.
/// </summary>
internal static class DiagnosticMessage
{
    public static string Warning(string code, string text) => Create("warning", code, text);

    public static string Error(string code, string text) => Create("error", code, text);

    private static string Create(string category, string code, string text)
    {
        StringBuilder builder = new();

        builder.Append("Containerize : "); // tool name as the origin
        builder.Append(category);
        builder.Append(' ');
        builder.Append(code);
        builder.Append(" : ");
        builder.Append(text);

        return builder.ToString();
    }
}

