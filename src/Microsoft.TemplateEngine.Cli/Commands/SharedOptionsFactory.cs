// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Cli.TabularOutput;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    internal static class SharedOptionsFactory
    {
        internal static Option<bool> CreateInteractiveOption()
        {
            return new Option<bool>("--interactive")
            {
                Arity = new ArgumentArity(0, 1),
                Description = LocalizableStrings.OptionDescriptionInteractive
            };
        }

        internal static Option<IReadOnlyList<string>> CreateAddSourceOption()
        {
            return new(new[] { "--add-source", "--nuget-source" })
            {
                Arity = new ArgumentArity(0, 99),
                Description = LocalizableStrings.OptionDescriptionNuGetSource,
                AllowMultipleArgumentsPerToken = true,
            };
        }

        internal static Option<string> CreateAuthorOption()
        {
            return new(new[] { "--author" })
            {
                Arity = new ArgumentArity(1, 1),
                Description = LocalizableStrings.OptionDescriptionAuthorFilter
            };
        }

        internal static Option<string> CreateBaselineOption()
        {
            return new(new[] { "--baseline" })
            {
                Arity = new ArgumentArity(1, 1),
                Description = LocalizableStrings.OptionDescriptionBaseline,
                IsHidden = true
            };
        }

        internal static Option<string> CreateLanguageOption()
        {
            return new(new[] { "--language", "-lang" })
            {
                Arity = new ArgumentArity(1, 1),
                Description = LocalizableStrings.OptionDescriptionLanguageFilter
            };
        }

        internal static Option<string> CreateTypeOption()
        {
            return new(new[] { "--type" })
            {
                Arity = new ArgumentArity(1, 1),
                Description = LocalizableStrings.OptionDescriptionTypeFilter
            };
        }

        internal static Option<string> CreateTagOption()
        {
            return new(new[] { "--tag" })
            {
                Arity = new ArgumentArity(1, 1),
                Description = LocalizableStrings.OptionDescriptionTagFilter
            };
        }

        internal static Option<string> CreatePackageOption()
        {
            return new(new[] { "--package" })
            {
                Arity = new ArgumentArity(1, 1),
                Description = LocalizableStrings.OptionDescriptionPackageFilter
            };
        }

        internal static Option<bool> CreateColumnsAllOption()
        {
            return new(new[] { "--columns-all" })
            {
                Arity = new ArgumentArity(0, 1),
                Description = LocalizableStrings.OptionDescriptionColumnsAll
            };
        }

        internal static Option<IReadOnlyList<string>> CreateColumnsOption()
        {
            Option<IReadOnlyList<string>> option = new(new[] { "--columns" }, ParseCommaSeparatedValues)
            {
                Arity = new ArgumentArity(1, 4),
                Description = LocalizableStrings.OptionDescriptionColumns,
                AllowMultipleArgumentsPerToken = true,
            };
            option.FromAmong(
                TabularOutputSettings.ColumnNames.Author,
                TabularOutputSettings.ColumnNames.Language,
                TabularOutputSettings.ColumnNames.Type,
                TabularOutputSettings.ColumnNames.Tags);
            return option;
        }

        internal static Option<string> CreateOutputOption()
        {
            return new Option<string>(new string[] { "-o", "--output" })
            {
                Description = LocalizableStrings.OptionDescriptionOutput,
                IsRequired = false,
                Arity = new ArgumentArity(1, 1)
            };
        }

        internal static IReadOnlyList<string> ParseCommaSeparatedValues(ArgumentResult result)
        {
            List<string> values = new List<string>();
            foreach (var value in result.Tokens.Select(t => t.Value))
            {
                values.AddRange(value.Split(",", StringSplitOptions.TrimEntries).Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            return values;
        }

        internal static Option AsHidden(this Option o)
        {
            o.IsHidden = true;
            return o;
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
