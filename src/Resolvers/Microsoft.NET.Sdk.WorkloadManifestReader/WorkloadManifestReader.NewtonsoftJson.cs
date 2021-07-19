// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !USE_SYSTEM_TEXT_JSON

using System;
using System.IO;

using Newtonsoft.Json;

using JsonTokenType = Newtonsoft.Json.JsonToken;

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public partial class WorkloadManifestReader
    {
        public static WorkloadManifest ReadWorkloadManifest(string manifestId, Stream manifestStream, string? informationalPath = null)
        {
            using var textReader = new StreamReader(manifestStream, System.Text.Encoding.UTF8, true);
            using var jsonReader = new JsonTextReader(textReader);

            var reader = new Utf8JsonStreamReader(jsonReader);

            return ReadWorkloadManifest(manifestId, informationalPath, ref reader);
        }
        // this is a compat wrapper so the source matches the system.text.json impl
        private ref struct Utf8JsonStreamReader
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
