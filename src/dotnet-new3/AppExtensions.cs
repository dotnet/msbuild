using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace dotnet_new3
{
    internal static class AppExtensions
    {
        public static CommandOption Help(this CommandLineApplication app)
        {
            return app.Option("-h|--help", "Displays help for this command.", CommandOptionType.NoValue);
        }

        public static IReadOnlyDictionary<string, string> ParseExtraArgs(this CommandLineApplication app, CommandOption extraArgs)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();

            if (extraArgs.HasValue())
            {
                foreach (string argFile in extraArgs.Values)
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
                                parameters[property.Name] = property.Value.ToString();
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

                bool wellFormedParam = true;
                if (!key.StartsWith("--", StringComparison.Ordinal))
                {
                    if (!key.StartsWith("-p:", StringComparison.Ordinal))
                    {
                        if (key.StartsWith("-", StringComparison.Ordinal)
                            && (key.Length != 2 || !Char.IsLetter(key[1])))
                        {
                            wellFormedParam = false;
                        }
                    }
                    else
                    {
                        wellFormedParam = false;
                    }
                }

                if (!wellFormedParam)
                {
                    throw new Exception($"Invalid parameter name [${key}]. Parameter names must begin with '--', '-p:', or be exactly '-<single letter>'");
                }

                string value = null;
                if (app.RemainingArguments.Count > i + 1)
                {
                    value = app.RemainingArguments[i + 1];
                    //if (value.StartsWith("--", StringComparison.Ordinal))
                    if (value.StartsWith("-", StringComparison.Ordinal))
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