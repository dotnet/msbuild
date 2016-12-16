using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.Cli.CommandLine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Tools.New3
{
    internal static class AppExtensions
    {
        public static CommandOption Help(this CommandLineApplication app)
        {
            return app.Option("-h|--help", "Displays help for this command.", CommandOptionType.NoValue);
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
                    using (Stream s = File.OpenRead(argFile))
                    using (TextReader r = new StreamReader(s, Encoding.UTF8, true, 4096, true))
                    using (JsonTextReader reader = new JsonTextReader(r))
                    {
                        JObject obj = JObject.Load(reader);

                        foreach (JProperty property in obj.Properties())
                        {
                            if(property.Value.Type == JTokenType.String)
                            {
                                IList<string> values = new List<string>();
                                values.Add(property.Value.ToString());
                                // adding 2 dashes to the file-based params
                                // won't work right if there's a param that should have 1 dash
                                //
                                // TOOD: come up with a better way to deal with this
                                parameters["--" + property.Name] = values;
                            }
                        }
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
                    throw new Exception("Parameter names must start with -- or -");
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

                IList<string> valueList;
                if (!parameters.TryGetValue(key, out valueList))
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