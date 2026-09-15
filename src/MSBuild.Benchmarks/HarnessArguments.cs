// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace MSBuild.Benchmarks;

/// <summary>
/// Argument parsing shared by the evaluation input harness entry points.
/// </summary>
internal static class HarnessArguments
{
    internal static string Take(List<string> args, string name)
    {
        int index = args.IndexOf(name);
        if (index < 0 || index + 1 >= args.Count)
        {
            throw new ArgumentException($"Missing required argument '{name}'.");
        }

        string value = args[index + 1];
        args.RemoveRange(index, 2);
        return value;
    }

    internal static Dictionary<string, string> TakeGlobalProperties(List<string> args)
    {
        Dictionary<string, string> properties = new(StringComparer.OrdinalIgnoreCase);
        int index;
        while ((index = args.IndexOf("--global-property")) >= 0)
        {
            if (index + 1 >= args.Count)
            {
                throw new ArgumentException("Missing value for '--global-property'.");
            }

            string assignment = args[index + 1];
            args.RemoveRange(index, 2);
            int separator = assignment.IndexOf('=');
            if (separator <= 0)
            {
                throw new ArgumentException($"Global property '{assignment}' must use the form Name=Value.");
            }

            properties[assignment.Substring(0, separator)] = assignment.Substring(separator + 1);
        }

        return properties;
    }

    internal static void ExpectEmpty(List<string> args)
    {
        if (args.Count != 0)
        {
            throw new ArgumentException($"Unexpected arguments: {string.Join(" ", args)}");
        }
    }

    internal static string Quote(string value) => string.Concat("\"", value.Replace("\"", "\\\""), "\"");
}
