// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#if !NET6_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
#endif
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#nullable enable

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

            JObject? obj = token as JObject;

            if (obj == null)
            {
                return null;
            }

            JToken? element;
            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out element) || element.Type != JTokenType.String)
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
            int value = defaultValue;
            if (key == null)
            {
                if (token == null || token.Type != JTokenType.Integer || !int.TryParse(token.ToString(), out value))
                {
                    return defaultValue;
                }

                return value;
            }

            JObject? obj = token as JObject;

            if (obj == null)
            {
                return defaultValue;
            }

            JToken? element;
            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out element))
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

        internal static T ToEnum<T>(this JToken token, string? key = null, T defaultValue = default(T))
            where T : struct
        {
            string? val = token.ToString(key);
            T result;
            if (val == null || !Enum.TryParse(val, out result))
            {
                return defaultValue;
            }

            return result;
        }

        internal static Guid ToGuid(this JToken token, string? key = null, Guid defaultValue = default(Guid))
        {
            string? val = token.ToString(key);
            Guid result;
            if (val == null || !Guid.TryParse(val, out result))
            {
                return defaultValue;
            }

            return result;
        }

        internal static IEnumerable<JProperty> PropertiesOf(this JToken? token, string? key = null)
        {
            JObject? obj = token as JObject;

            if (obj == null)
            {
                return Array.Empty<JProperty>();
            }

            if (key != null)
            {
                JToken? element;
                if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out element))
                {
                    return Array.Empty<JProperty>();
                }

                obj = element as JObject;
            }

            if (obj == null)
            {
                return Array.Empty<JProperty>();
            }

            return obj.Properties();
        }

        internal static T? Get<T>(this JToken? token, string? key)
            where T : JToken
        {
            JObject? obj = token as JObject;

            if (obj == null || key == null)
            {
                return default(T);
            }

            JToken? res;
            if (!obj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out res))
            {
                return default(T);
            }

            return res as T;
        }

        internal static IReadOnlyDictionary<string, string> ToStringDictionary(this JToken token, StringComparer? comparer = null, string? propertyName = null)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(comparer ?? StringComparer.Ordinal);

            foreach (JProperty property in token.PropertiesOf(propertyName))
            {
                if (property.Value == null || property.Value.Type != JTokenType.String)
                {
                    continue;
                }

                result[property.Name] = property.Value.ToString();
            }

            return result;
        }

        // Leaves the values as JTokens.
        internal static IReadOnlyDictionary<string, JToken> ToJTokenDictionary(this JToken token, StringComparer? comparaer = null, string? propertyName = null)
        {
            Dictionary<string, JToken> result = new Dictionary<string, JToken>(comparaer ?? StringComparer.Ordinal);

            foreach (JProperty property in token.PropertiesOf(propertyName))
            {
                result[property.Name] = property.Value;
            }

            return result;
        }

        internal static TemplateParameterPrecedence ToTemplateParameterPrecedence(this JToken jObject, string? key)
        {
            if (!jObject.TryGetValue(key, out JToken? checkToken))
            {
                return TemplateParameterPrecedence.Default;
            }

            PrecedenceDefinition precedenceDefinition = (PrecedenceDefinition)checkToken.ToInt32(nameof(PrecedenceDefinition));
            string? isRequiredCondition = checkToken.ToString(nameof(TemplateParameterPrecedence.IsRequiredCondition));
            string? isEnabledCondition = checkToken.ToString(nameof(TemplateParameterPrecedence.IsEnabledCondition));
            bool isRequired = checkToken.ToBool(nameof(TemplateParameterPrecedence.IsRequired));

            return new TemplateParameterPrecedence(precedenceDefinition, isRequiredCondition, isEnabledCondition, isRequired);
        }

        internal static IReadOnlyList<string> ArrayAsStrings(this JToken? token, string? propertyName = null)
        {
            if (propertyName != null)
            {
                token = token.Get<JArray>(propertyName);
            }

            JArray? arr = token as JArray;

            if (arr == null)
            {
                return Array.Empty<string>();
            }

            List<string> values = new List<string>();

            foreach (JToken item in arr)
            {
                if (item != null && item.Type == JTokenType.String)
                {
                    values.Add(item.ToString());
                }
            }

            return values;
        }

        internal static IReadOnlyList<Guid> ArrayAsGuids(this JToken? token, string? propertyName = null)
        {
            if (propertyName != null)
            {
                token = token.Get<JArray>(propertyName);
            }

            JArray? arr = token as JArray;

            if (arr == null)
            {
                return Array.Empty<Guid>();
            }

            List<Guid> values = new List<Guid>();

            foreach (JToken item in arr)
            {
                if (item != null && item.Type == JTokenType.String)
                {
                    Guid val;
                    if (Guid.TryParse(item.ToString(), out val))
                    {
                        values.Add(val);
                    }
                }
            }

            return values;
        }

        internal static IEnumerable<T> Items<T>(this JToken? token, string? propertyName = null)
            where T : JToken
        {
            if (propertyName != null)
            {
                token = token.Get<JArray>(propertyName);
            }

            JArray? arr = token as JArray;

            if (arr == null)
            {
                yield break;
            }

            foreach (JToken item in arr)
            {
                T? res = item as T;

                if (res != null)
                {
                    yield return res;
                }
            }
        }

        internal static JObject ReadJObjectFromIFile(this IFile file)
        {
            using (Stream s = file.OpenRead())
            using (TextReader tr = new StreamReader(s, System.Text.Encoding.UTF8, true))
            using (JsonReader r = new JsonTextReader(tr))
            {
                return JObject.Load(r);
            }
        }

        internal static JObject ReadObject(this IPhysicalFileSystem fileSystem, string path)
        {
            using (var fileStream = fileSystem.OpenRead(path))
            using (var textReader = new StreamReader(fileStream, System.Text.Encoding.UTF8, true))
            using (var jsonReader = new JsonTextReader(textReader))
            {
                return JObject.Load(jsonReader);
            }
        }

        internal static void WriteObject(this IPhysicalFileSystem fileSystem, string path, object obj)
        {
            using (var fileStream = fileSystem.CreateFile(path))
            using (var textWriter = new StreamWriter(fileStream, System.Text.Encoding.UTF8))
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(jsonWriter, obj);
            }
        }

        internal static IReadOnlyList<string> JTokenStringOrArrayToCollection(this JToken? token, string[] defaultSet)
        {
            if (token == null)
            {
                return defaultSet;
            }

            if (token.Type == JTokenType.String)
            {
                string tokenValue = token.ToString();
                return new List<string>() { tokenValue };
            }

            return token.ArrayAsStrings();
        }

        /// <summary>
        /// Converts <paramref name="token"/> to valid JSON string.
        /// JToken.ToString() doesn't provide a valid JSON string for JTokenType == String.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        internal static string? ToJSONString(this JToken? token)
        {
            if (token == null)
            {
                return null;
            }
            using StringWriter stringWriter = new StringWriter();
            using JsonWriter jsonWriter = new JsonTextWriter(stringWriter);
            token.WriteTo(jsonWriter);
            return stringWriter.ToString();
        }

    }
}
