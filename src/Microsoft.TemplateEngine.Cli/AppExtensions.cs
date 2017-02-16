// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli
{
    internal static class AppExtensions
    {
        public static CommandOption Help(this CommandLineApplication app)
        {
            return app.Option("-h|--help", LocalizableStrings.DisplaysHelp, CommandOptionType.NoValue);
        }

        public static IReadOnlyDictionary<string, IList<string>> ParseExtraArgs(this CommandLineApplication app, IList<string> extraArgFileNames)
        {
            Dictionary<string, IList<string>> parameters = new Dictionary<string, IList<string>>();

            // Note: If the same param is specified multiple times across the files, last-in-wins
            // TODO: consider another course of action.
            if (extraArgFileNames.Count > 0)
            {
                foreach (string argFile in extraArgFileNames)
                {
                    if (!File.Exists(argFile))
                    {
                        throw new CommandParserException(string.Format(LocalizableStrings.ArgsFileNotFound, argFile), argFile);
                    }

                    try
                    {
                        using (Stream s = File.OpenRead(argFile))
                        using (TextReader r = new StreamReader(s, Encoding.UTF8, true, 4096, true))
                        using (JsonTextReader reader = new JsonTextReader(r))
                        {
                            JObject obj = JObject.Load(reader);

                            foreach (JProperty property in obj.Properties())
                            {
                                if (property.Value.Type == JTokenType.String)
                                {
                                    IList<string> values = new List<string>
                                {
                                    property.Value.ToString()
                                };

                                    // adding 2 dashes to the file-based params
                                    // won't work right if there's a param that should have 1 dash
                                    //
                                    // TOOD: come up with a better way to deal with this
                                    parameters["--" + property.Name] = values;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new CommandParserException(string.Format(LocalizableStrings.ArgsFileWrongFormat, argFile), argFile, ex);
                    }
                }
            }

            for (int i = 0; i < app.RemainingArguments.Count; ++i)
            {
                string key = app.RemainingArguments[i];
                CommandOption arg = app.Options.FirstOrDefault(x => x.Template.Split('|').Any(y => string.Equals(y, key, StringComparison.OrdinalIgnoreCase)));
                bool handled = false;

                if (arg != null)
                {
                    if (arg.OptionType != CommandOptionType.NoValue)
                    {
                        handled = arg.TryParse(app.RemainingArguments[i + 1]);
                        ++i;
                    }
                    else
                    {
                        handled = arg.TryParse(null);
                    }
                }

                if (handled)
                {
                    continue;
                }

                if (!key.StartsWith("-", StringComparison.Ordinal))
                {
                    throw new CommandParserException(string.Format(LocalizableStrings.MultipleArgsSpecifiedError, key), key);
                }

                // Check the next value. If it doesn't start with a '-' then it's a value for the current param.
                // Otherwise it's its own param.
                string value = null;
                if (app.RemainingArguments.Count > i + 1)
                {
                    value = app.RemainingArguments[i + 1];

                    if (value.StartsWith("-", StringComparison.Ordinal))
                    {
                        value = null;
                    }
                    else
                    {
                        ++i;
                    }
                }

                if (!parameters.TryGetValue(key, out IList<string> valueList))
                {
                    valueList = new List<string>();
                    parameters.Add(key, valueList);
                }

                valueList.Add(value);
            }

            return parameters;
        }
    }
}