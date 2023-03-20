// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Represents a diagnostic message that could be parsed by VS.
/// https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-diagnostic-format-for-tasks?view=vs-2022
/// </summary>
internal static class DiagnosticMessage
{
    public static string Warning(string code, string text) => Create("warning", code, text);

    public static string Error(string code, string text) => Create("error", code, text);

    public static string WarningFromResourceWithCode(string resourceName, params object?[] args) => CreateFromResourceWithCode("warning", resourceName, args);

    public static string ErrorFromResourceWithCode(string resourceName, params object?[] args) => CreateFromResourceWithCode("error", resourceName, args);

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

    private static string CreateFromResourceWithCode(string category, string resourceName, params object?[] args)
    {
        string textWithCode = Resource.FormatString(resourceName, args);

        StringBuilder builder = new();

        builder.Append("Containerize : "); // tool name as the origin
        builder.Append(category);
        builder.Append(' ');
        builder.Append(textWithCode);

        return builder.ToString();
    }
}
