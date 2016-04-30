using System;
using System.Collections.Generic;
using Microsoft.Extensions.CommandLineUtils;

namespace dotnet_new3
{
    internal static class AppExtensions
    {
        public static CommandOption Help(this CommandLineApplication app)
        {
            return app.Option("-h|--help", "Displays help for this command.", CommandOptionType.NoValue);
        }

        public static IReadOnlyDictionary<string, string> ParseExtraArgs(this CommandLineApplication app)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            for (int i = 0; i < app.RemainingArguments.Count; ++i)
            {
                string key = app.RemainingArguments[i];

                if (!key.StartsWith("--", StringComparison.Ordinal))
                {
                    throw new Exception("Parameter names must start with --");
                }

                string value = null;
                if (app.RemainingArguments.Count > i + 1)
                {
                    value = app.RemainingArguments[i + 1];
                    if (value.StartsWith("--", StringComparison.Ordinal))
                    {
                        value = null;
                    }
                    else
                    {
                        ++i;
                    }
                }

                parameters[key.Substring(2)] = value;
            }

            return parameters;
        }
    }
}