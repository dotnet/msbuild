// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !USE_SYSTEM_TEXT_JSON

using System;
using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;

using JsonTokenType = Newtonsoft.Json.JsonToken;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public partial class WorkloadManifestReader
    {
        public static WorkloadManifest ReadWorkloadManifest(string manifestId, Stream manifestStream, Stream? localizationStream, string manifestPath)
        {
            using var textReader = new StreamReader(manifestStream, System.Text.Encoding.UTF8, true);
            using var jsonReader = new JsonTextReader(textReader);

            var manifestReader = new Utf8JsonStreamReader(jsonReader);

            return ReadWorkloadManifest(manifestId, manifestPath, ReadLocalizationCatalog(localizationStream), ref manifestReader); ;
        }

        private static LocalizationCatalog? ReadLocalizationCatalog(Stream? localizationStream)
        {
            if (localizationStream == null)
            {
                return null;
            }

            using var textReader = new StreamReader(localizationStream, System.Text.Encoding.UTF8, true);
            using var jsonReader = new JsonTextReader(textReader);

            var localizationReader = new Utf8JsonStreamReader(jsonReader);

            return ReadLocalizationCatalog(ref localizationReader);
        }

        // this is a compat wrapper so the source matches the system.text.json impl
        internal ref struct Utf8JsonStreamReader
        {
            public Utf8JsonStreamReader(JsonTextReader reader)
            {
                this.reader = reader;
            }

            JsonTextReader reader;

            public long TokenStartIndex => reader.LineNumber; //FIXME: rationalize line/col and offset

            public JsonTokenType TokenType => reader.TokenType;

            public int CurrentDepth => reader.Depth;

            public string GetString() => reader.Value as string ?? throw new InvalidOperationException("Not a string token");

            public bool TryGetInt64(out long value)
            {
                long? v = reader.Value as long? ?? reader.Value as int?;
                if (v.HasValue)
                {
                    value = v.Value;
                    return true;
                }
                value = 0;
                return false;
            }

            public bool GetBool() => reader.Value as bool? ?? throw new InvalidOperationException("Not a bool token");

            public bool Read()
            {
                // system.text.json allows ignoring comments with an option. newtonsoft.json doesn't, so do it here.
                while (reader.Read())
                {
                    if (reader.TokenType != JsonTokenType.Comment)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }

    internal static class JsonTokenTypeExtensions
    {
        public static bool IsBool(this JsonTokenType tokenType) => tokenType == JsonTokenType.Boolean;
        public static bool IsInt(this JsonTokenType tokenType) => tokenType == JsonTokenType.Integer;
    }
}

#endif
