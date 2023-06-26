// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.TemplateEngine.Cli.TabularOutput;

namespace Microsoft.TemplateEngine.Cli.Commands
{
    public static class SharedOptionsFactory
    {
        public static Option AsHidden(this Option o)
        {
            o.IsHidden = true;
            return o;
        }

        public static Option<T> AsHidden<T>(this Option<T> o)
        {
            o.IsHidden = true;
            return o;
        }

        public static Option<T> WithDescription<T>(this Option<T> o, string description)
        {
            o.Description = description;
            return o;
        }

        public static Option<T> DisableAllowMultipleArgumentsPerToken<T>(this Option<T> o)
        {
            o.AllowMultipleArgumentsPerToken = false;
            return o;
        }

        internal static Option<bool> CreateInteractiveOption()
        {
            return new Option<bool>("--interactive")
            {
                Arity = new ArgumentArity(0, 1),
                Description = SymbolStrings.Option_Interactive
            };
        }

        internal static Option<string[]> CreateAddSourceOption()
        {
            return new(new[] { "--add-source", "--nuget-source" })
            {
                Arity = new ArgumentArity(1, 99),
                Description = SymbolStrings.Option_AddSource,
                AllowMultipleArgumentsPerToken = true,
            };
        }

        internal static Option<bool> CreateForceOption()
        {
            return new(new[] { "--force" })
            {
                Arity = new ArgumentArity(0, 1),
                Description = SymbolStrings.TemplateCommand_Option_Force,
            };
        }

        internal static Option<string> CreateAuthorOption()
        {
            return new(new[] { "--author" })
            {
                Arity = new ArgumentArity(1, 1),
                Description = SymbolStrings.Option_AuthorFilter
            };
        }

        internal static Option<string> CreateBaselineOption()
        {
            return new(new[] { "--baseline" })
            {
                Arity = new ArgumentArity(1, 1),
                Description = SymbolStrings.Option_BaselineFilter,
                IsHidden = true
            };
        }

        internal static Option<string> CreateLanguageOption()
        {
            return new(new[] { "--language", "-lang" })
            {
                Arity = new ArgumentArity(1, 1),
                Description = SymbolStrings.Option_LanguageFilter
            };
        }

        internal static Option<string> CreateTypeOption()
        {
            return new(new[] { "--type" })
            {
                Arity = new ArgumentArity(1, 1),
                Description = SymbolStrings.Option_TypeFilter
            };
        }

        internal static Option<string> CreateTagOption()
        {
            return new(new[] { "--tag" })
            {
                Arity = new ArgumentArity(1, 1),
                Description = SymbolStrings.Option_TagFilter
            };
        }

        internal static Option<string> CreatePackageOption()
        {
            return new(new[] { "--package" })
            {
                Arity = new ArgumentArity(1, 1),
                Description = SymbolStrings.Option_PackageFilter
            };
        }

        internal static Option<bool> CreateColumnsAllOption()
        {
            return new(new[] { "--columns-all" })
            {
                Arity = new ArgumentArity(0, 1),
                Description = SymbolStrings.Option_ColumnsAll
            };
        }

        internal static Option<string[]> CreateColumnsOption()
        {
            Option<string[]> option = new(new[] { "--columns" }, ParseCommaSeparatedValues)
            {
                Arity = new ArgumentArity(1, 4),
                Description = SymbolStrings.Option_Columns,
                AllowMultipleArgumentsPerToken = true,
            };
            option.AcceptOnlyFromAmong(
                TabularOutputSettings.ColumnNames.Author,
                TabularOutputSettings.ColumnNames.Language,
                TabularOutputSettings.ColumnNames.Type,
                TabularOutputSettings.ColumnNames.Tags);
            return option;
        }

        internal static string[] ParseCommaSeparatedValues(ArgumentResult result)
        {
            List<string> values = new();
            foreach (string value in result.Tokens.Select(t => t.Value))
            {
                values.AddRange(value.Split(",", StringSplitOptions.TrimEntries).Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            return values.ToArray();
        }
    }
}
