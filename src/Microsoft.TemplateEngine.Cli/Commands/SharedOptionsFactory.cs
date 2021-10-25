// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal static class SharedOptionsFactory
    {
        internal static Option<bool> GetInteractiveOption()
        {
            return new Option<bool>("--interactive")
            {
                Description = LocalizableStrings.OptionDescriptionInteractive
            };
        }

        internal static Option<IReadOnlyList<string>> GetAddSourceOption()
        {
            return new(new[] { "--add-source", "--nuget-source" })
            {
                Description = LocalizableStrings.OptionDescriptionNuGetSource,
                AllowMultipleArgumentsPerToken = true,
            };
        }

        internal static Option<T> AsHidden<T>(this Option<T> o)
        {
            o.IsHidden = true;
            return o;
        }

        internal static Option<T> DisableAllowMultipleArgumentsPerToken<T>(this Option<T> o)
        {
            o.AllowMultipleArgumentsPerToken = false;
            return o;
        }
    }
}
