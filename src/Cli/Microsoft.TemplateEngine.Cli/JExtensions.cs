// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
#endif
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine
{
    internal static class JExtensions
    {
        internal static string? ToString(this JToken? token, string? key)
        {
            if (key == null)
            {
                if (token == null || token.Type != JTokenType.String)
                {
                    return null;
                }

                return token.ToString();
            }

            if (token is not JObject obj)
            {
                return null;
            }

            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken? element) || element.Type != JTokenType.String)
            {
                return null;
            }

            return element.ToString();
        }

        internal static bool TryGetValue(this JToken? token, string? key, out JToken? result)
        {
            result = null;

            // determine which token to bool-ify
            if (token == null)
            {
                return false;
            }
            else if (key == null)
            {
                result = token;
            }
            else if (!((JObject)token).TryGetValue(key, StringComparison.OrdinalIgnoreCase, out result))
            {
                return false;
            }

            return true;
        }

        internal static bool TryParseBool(this JToken token, out bool result)
        {
            result = false;
            return (token.Type == JTokenType.Boolean || token.Type == JTokenType.String)
                   &&
                   bool.TryParse(token.ToString(), out result);
        }

        internal static bool ToBool(this JToken? token, string? key = null, bool defaultValue = false)
        {
            if (!token.TryGetValue(key, out JToken? checkToken))
            {
                return defaultValue;
            }

            if (!checkToken!.TryParseBool(out bool result))
            {
                result = defaultValue;
            }

            return result;
        }

        internal static int ToInt32(this JToken? token, string? key = null, int defaultValue = 0)
        {
            int value;
            if (key == null)
            {
                if (token == null || token.Type != JTokenType.Integer || !int.TryParse(token.ToString(), out value))
                {
                    return defaultValue;
                }

                return value;
            }

            if (token is not JObject obj)
            {
                return defaultValue;
            }

            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken? element))
            {
                return defaultValue;
            }
            else if (element.Type == JTokenType.Integer)
            {
                return element.ToInt32();
            }
            else if (int.TryParse(element.ToString(), out value))
            {
                return value;
            }

            return defaultValue;
        }

        internal static T ToEnum<T>(this JToken token, string? key = null, T defaultValue = default)
            where T : struct
        {
            string? val = token.ToString(key);
            if (val == null || !Enum.TryParse(val, out T result))
            {
                return defaultValue;
            }

            return result;
        }

        internal static Guid ToGuid(this JToken token, string? key = null, Guid defaultValue = default)
        {
            string? val = token.ToString(key);
            if (val == null || !Guid.TryParse(val, out Guid result))
            {
                return defaultValue;
            }

            return result;
        }

        internal static IEnumerable<JProperty> PropertiesOf(this JToken? token, string? key = null)
        {
            JObject? currentJObj = token as JObject;
            if (currentJObj == null)
            {
                return Array.Empty<JProperty>();
            }

            if (key != null)
            {
                if (!currentJObj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken? element))
                {
                    return Array.Empty<JProperty>();
                }
                currentJObj = element as JObject;
            }
            if (currentJObj == null)
            {
                return Array.Empty<JProperty>();
            }

            return currentJObj.Properties();
        }

        internal static T? Get<T>(this JToken? token, string? key)
            where T : JToken
        {
            if (token is not JObject obj || key == null)
            {
                return default;
            }

            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out JToken? res))
            {
                return default;
            }

            return res as T;
        }

        internal static IReadOnlyList<string> ArrayAsStrings(this JToken? token, string? propertyName = null)
        {
            if (propertyName != null)
            {
                token = token.Get<JArray>(propertyName);
            }

            if (token is not JArray arr)
            {
                return Array.Empty<string>();
            }

            List<string> values = new();

            foreach (JToken item in arr)
            {
                if (item != null && item.Type == JTokenType.String)
                {
                    values.Add(item.ToString());
                }
            }

            return values;
        }

        internal static JObject ReadObject(this IPhysicalFileSystem fileSystem, string path)
        {
            using (Stream fileStream = fileSystem.OpenRead(path))
            using (var textReader = new StreamReader(fileStream, System.Text.Encoding.UTF8, true))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                return JObject.Load(jsonReader);
            }
        }

        internal static void WriteObject(this IPhysicalFileSystem fileSystem, string path, object obj)
        {
            using (Stream fileStream = fileSystem.CreateFile(path))
            using (var textWriter = new StreamWriter(fileStream, System.Text.Encoding.UTF8))
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(jsonWriter, obj);
            }
        }

        internal static bool TryParse(this string arg, out JToken? token)
        {
            try
            {
                token = JToken.Parse(arg);
                return true;
            }
            catch
            {
                token = null;
                return false;
            }
        }

    }
}
