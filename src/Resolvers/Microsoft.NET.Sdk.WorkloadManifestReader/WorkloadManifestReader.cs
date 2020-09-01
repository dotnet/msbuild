using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Microsoft.Net.Sdk.WorkloadManifestReader
{
    partial class WorkloadManifestReader
    {
        static ReadOnlySpan<byte> utf8Bom => new byte[] { 0xEF, 0xBB, 0xBF };

        public static WorkloadManifest ReadWorkloadManifest (Stream manifestStream)
        {
            var readerOptions = new JsonReaderOptions {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };

            var reader = new Utf8JsonStreamReader(manifestStream, readerOptions);

            return ReadWorkloadManifest(ref reader);
        }

        static void ConsumeToken(ref Utf8JsonStreamReader reader, JsonTokenType expected)
        {
            if (reader.Read() && expected == reader.TokenType)
            {
                return;
            }
            throw new WorkloadManifestFormatException($"Expected '{expected}' at offset {reader.TokenStartIndex}");
        }

        static string ReadString(ref Utf8JsonStreamReader reader)
        {
            if (reader.Read() && reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }

            throw new WorkloadManifestFormatException($"Expected string value at offset {reader.TokenStartIndex}");
        }

        static bool ReadBool(ref Utf8JsonStreamReader reader)
        {
            if (reader.Read() && (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False))
            {
                return reader.TokenType == JsonTokenType.True;
            }

            throw new WorkloadManifestFormatException($"Expected boolean value at offset {reader.TokenStartIndex}");
        }

        static void ThrowDuplicateKeyException (ref Utf8JsonStreamReader reader, string key)
            => throw new WorkloadManifestFormatException($"Duplicate key '{key}' at offset {reader.TokenStartIndex}");

        static WorkloadManifest ReadWorkloadManifest(ref Utf8JsonStreamReader reader)
        {
            ConsumeToken(ref reader, JsonTokenType.StartObject);

            long? version = null;
            string? description = null;
            Dictionary<string, WorkloadDefinition>? workloads = null;
            Dictionary<string, WorkloadPack>? packs = null;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var propName = reader.GetString();

                        if (string.Equals("version", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (version != null) ThrowDuplicateKeyException(ref reader, propName);
                            if (!reader.Read() || reader.TokenType != JsonTokenType.Number) throw new WorkloadManifestFormatException("Could not read manifest version");
                            version = reader.GetInt64();
                            continue;
                        }

                        if (string.Equals("description", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (description != null) ThrowDuplicateKeyException(ref reader, propName);
                            description = ReadString(ref reader);
                            continue;
                        }

                        if (string.Equals("workloads", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (workloads != null) ThrowDuplicateKeyException(ref reader, propName);
                            workloads = ReadWorkloadDefinitions(ref reader);
                            continue;
                        }

                        if (string.Equals("packs", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (packs != null) ThrowDuplicateKeyException(ref reader, propName);
                            packs = ReadWorkloadPacks(ref reader);
                            continue;
                        }

                        // just ignore this for now. it's part of the spec but we don't have an API for exposing it as structured data.
                        if (string.Equals("data", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!ConsumeValue(ref reader))
                            {
                                break;
                            }
                            continue;
                        }

                        // it would be more robust to ignore unknown keys but right now we're pretty unforgiving
                        throw new WorkloadManifestFormatException($"Unknown key '{propName}' at offset {reader.TokenStartIndex}");
                    case JsonTokenType.EndObject:

                        if (version == null || version < 0)
                        {
                            throw new WorkloadManifestFormatException("Missing or invalid manifest version");
                        }

                        return new WorkloadManifest (
                            version.Value,
                            description,
                            workloads ?? new Dictionary<string, WorkloadDefinition> (),
                            packs ?? new Dictionary<string, WorkloadPack> ()
                        );
                    default:
                        throw new WorkloadManifestFormatException($"Unexpected token '{reader.TokenType}' at offset {reader.TokenStartIndex}");
                }
            }

            throw new WorkloadManifestFormatException($"Incomplete document");
        }

        /// <summary>
        /// this expects the reader to be before the value token, and leaves it on the last token of the value
        /// </summary>
        static bool ConsumeValue (ref Utf8JsonStreamReader reader)
        {
            if (!reader.Read())
            {
                return false;
            }

            var tokenType = reader.TokenType;
            if (tokenType != JsonTokenType.StartArray && tokenType != JsonTokenType.StartObject)
            {
                return true;
            }
            
            var depth = reader.CurrentDepth;
            do
            {
                if (!reader.Read())
                {
                    return false;
                }
            } while (reader.CurrentDepth > depth);

            return true;
        }

        static Dictionary<string, WorkloadDefinition> ReadWorkloadDefinitions(ref Utf8JsonStreamReader reader)
        {
            ConsumeToken(ref reader, JsonTokenType.StartObject);

            var workloads = new Dictionary<string, WorkloadDefinition>();

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var workloadId = reader.GetString();
                        var workload = ReadWorkloadDefinition(workloadId, ref reader);
                        if (workloads.ContainsKey(workloadId)) ThrowDuplicateKeyException(ref reader, workloadId);
                        workloads.Add(workloadId, workload);
                        continue;
                    case JsonTokenType.EndObject:
                        return workloads;
                    default:
                        throw new WorkloadManifestFormatException($"Unexpected token '{reader.TokenType}' at offset {reader.TokenStartIndex}");
                }
            }

            throw new WorkloadManifestFormatException($"Incomplete document");
        }

        static Dictionary<string, WorkloadPack> ReadWorkloadPacks(ref Utf8JsonStreamReader reader)
        {
            ConsumeToken(ref reader, JsonTokenType.StartObject);

            var packs = new Dictionary<string, WorkloadPack>();

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var packId = reader.GetString();
                        var pack = ReadWorkloadPack(packId,ref reader);
                        if (packs.ContainsKey(packId)) ThrowDuplicateKeyException(ref reader, packId);
                        packs[packId] = pack;
                        continue;
                    case JsonTokenType.EndObject:
                        return packs;
                    default:
                        throw new WorkloadManifestFormatException($"Unexpected token '{reader.TokenType}' at offset {reader.TokenStartIndex}");
                }
            }

            throw new WorkloadManifestFormatException($"Incomplete document");
        }

        static List<string> ReadStringArray(ref Utf8JsonStreamReader reader)
        {
            ConsumeToken(ref reader, JsonTokenType.StartArray);

            var list = new List<string>();

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.String:
                        list.Add(reader.GetString());
                        continue;
                    case JsonTokenType.EndArray:
                        return list;
                    default:
                        throw new WorkloadManifestFormatException($"Unexpected token '{reader.TokenType}' at offset {reader.TokenStartIndex}");
                }
            }

            throw new WorkloadManifestFormatException($"Incomplete document");
        }

        static Dictionary<string,string> ReadStringDictionary(ref Utf8JsonStreamReader reader)
        {
            ConsumeToken(ref reader, JsonTokenType.StartObject);

            var dictionary = new Dictionary<string,string>();

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var name = reader.GetString();
                        var val = ReadString(ref reader);
                        if (dictionary.ContainsKey(name)) ThrowDuplicateKeyException(ref reader, name);
                        dictionary.Add(name, val);
                        continue;
                    case JsonTokenType.EndObject:
                        return dictionary;
                    default:
                        throw new WorkloadManifestFormatException($"Unexpected token '{reader.TokenType}' at offset {reader.TokenStartIndex}");
                }
            }

            throw new WorkloadManifestFormatException($"Incomplete document");
        }

        static WorkloadDefinition ReadWorkloadDefinition(string id, ref Utf8JsonStreamReader reader)
        {
            ConsumeToken(ref reader, JsonTokenType.StartObject);

            string? description = null;
            bool? isAbstractOrNull = null;
            WorkloadDefinitionKind? kind = null;
            List<string>? packs = null;
            List<string>? extends = null;
            List<string>? platforms = null;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var propName = reader.GetString();

                        if (string.Equals("description", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (description != null) ThrowDuplicateKeyException(ref reader, propName);
                            description = ReadString(ref reader);
                            continue;
                        }

                        if (string.Equals("kind", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (kind != null) ThrowDuplicateKeyException(ref reader, propName);
                            var kindStr = ReadString(ref reader);
                            if (Enum.TryParse<WorkloadDefinitionKind>(kindStr, true, out var parsedKind))
                            {
                                kind = parsedKind;
                            }
                            else
                            {
                                throw new WorkloadManifestFormatException($"Unknown workload definition kind '{kindStr}' at offset {reader.TokenStartIndex}");

                            }
                            continue;
                        }

                        if (string.Equals("abstract", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (isAbstractOrNull != null) ThrowDuplicateKeyException(ref reader, propName);
                            isAbstractOrNull = ReadBool(ref reader);
                            continue;
                        }

                        if (string.Equals("packs", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (packs != null) ThrowDuplicateKeyException(ref reader, propName);
                            packs = ReadStringArray(ref reader);
                            continue;
                        }

                        if (string.Equals("extends", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (extends != null) ThrowDuplicateKeyException(ref reader, propName);
                            extends = ReadStringArray(ref reader);
                            continue;
                        }

                        if (string.Equals("platforms", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (platforms != null) ThrowDuplicateKeyException(ref reader, propName);
                            platforms = ReadStringArray(ref reader);
                            continue;
                        }

                        throw new WorkloadManifestFormatException($"Unknown key '{propName}' at  offset {reader.TokenStartIndex}");
                    case JsonTokenType.EndObject:
                        var isAbstract = isAbstractOrNull ?? false;
                        if (!isAbstract && kind == WorkloadDefinitionKind.Dev && string.IsNullOrEmpty (description))
                        {
                            throw new WorkloadManifestFormatException($"Workload definition '{id}' is a concrete dev workload but has no description");
                        }
                        return new WorkloadDefinition (id, isAbstract, description, kind ?? WorkloadDefinitionKind.Dev, extends, packs, platforms);
                    default:
                        throw new WorkloadManifestFormatException($"Unexpected token '{reader.TokenType}' at offset {reader.TokenStartIndex}");
                }
            }

            throw new WorkloadManifestFormatException("Incomplete decument");
        }

        static WorkloadPack ReadWorkloadPack(string id, ref Utf8JsonStreamReader reader)
        {
            ConsumeToken(ref reader, JsonTokenType.StartObject);

            string? version = null;
            WorkloadPackKind? kind = null;
            Dictionary<string, string>? aliasTo = null;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var propName = reader.GetString();

                        if (string.Equals("version", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (version != null) ThrowDuplicateKeyException(ref reader, propName);
                            version = ReadString(ref reader);
                            continue;
                        }

                        if (string.Equals("kind", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (kind != null) ThrowDuplicateKeyException(ref reader, propName);
                            var kindStr = ReadString(ref reader);
                            if (Enum.TryParse<WorkloadPackKind>(kindStr, true, out var parsedKind))
                            {
                                kind = parsedKind;
                            }
                            else
                            {
                                throw new WorkloadManifestFormatException($"Unknown workload pack kind '{kindStr}' at offset {reader.TokenStartIndex}");

                            }
                            continue;
                        }

                        if (string.Equals("alias-to", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (aliasTo != null) ThrowDuplicateKeyException(ref reader, propName);
                            aliasTo = ReadStringDictionary(ref reader);
                            continue;
                        }

                        throw new WorkloadManifestFormatException($"Unknown key '{propName}' at offset {reader.TokenStartIndex}");
                    case JsonTokenType.EndObject:
                        if (version == null)
                        {
                            throw new WorkloadManifestFormatException($"Missing version for workload pack '{id}'");
                        }
                        if (kind == null)
                        {
                            throw new WorkloadManifestFormatException($"Missing kind for workload pack '{id}'");
                        }
                        return new WorkloadPack (id, version, kind.Value, aliasTo);
                    default:
                        throw new WorkloadManifestFormatException($"Unexpected token '{reader.TokenType}' at offset {reader.TokenStartIndex}");
                }
            }

            throw new WorkloadManifestFormatException($"Incomplete document");
        }
    }
}
