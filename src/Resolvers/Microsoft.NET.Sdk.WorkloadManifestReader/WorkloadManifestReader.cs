// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using System.Collections.Generic;
using Microsoft.NET.Sdk.Localization;
using FXVersion = Microsoft.DotNet.MSBuildSdkResolver.FXVersion;

#if USE_SYSTEM_TEXT_JSON
using System.Text.Json;
#else
using JsonTokenType = Newtonsoft.Json.JsonToken;
#endif

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public partial class WorkloadManifestReader
    {
        public static WorkloadManifest ReadWorkloadManifest(string manifestId, System.IO.Stream manifestStream, string manifestPath)
            => ReadWorkloadManifest(manifestId, manifestStream, null, manifestPath);

        private static void ConsumeToken(ref Utf8JsonStreamReader reader, JsonTokenType expected)
        {
            if (reader.Read() && expected == reader.TokenType)
            {
                return;
            }
            ThrowUnexpectedTokenException(ref reader, expected);
        }

        private static void ThrowUnexpectedTokenException(ref Utf8JsonStreamReader reader, JsonTokenType expected)
        {
            string key;
            if (expected.IsBool())
            {
                key = Strings.ExpectedBoolAtOffset;
            }
            else if (expected.IsInt())
            {
                key = Strings.ExpectedIntegerAtOffset;
            }
            else if (expected == JsonTokenType.String)
            {
                key = Strings.ExpectedStringAtOffset;
            }
            else
            {
                throw new WorkloadManifestFormatException(Strings.ExpectedTokenAtOffset, expected, reader.TokenStartIndex);
            }

            throw new WorkloadManifestFormatException(key, reader.TokenStartIndex);
        }

        private static string ReadString(ref Utf8JsonStreamReader reader)
        {
            ConsumeToken(ref reader, JsonTokenType.String);
            return reader.GetString();
        }

        private static long ReadInt64(ref Utf8JsonStreamReader reader)
        {
            if (reader.Read() && reader.TokenType.IsInt())
            {
                if (reader.TryGetInt64 (out long value))
                {
                    return value;
                }
            }

            throw new WorkloadManifestFormatException(Strings.ExpectedIntegerAtOffset, reader.TokenStartIndex);
        }

        private static bool ReadBool(ref Utf8JsonStreamReader reader)
        {
            if (reader.Read() && reader.TokenType.IsBool())
            {
                return reader.GetBool();
            }

            throw new WorkloadManifestFormatException(Strings.ExpectedBoolAtOffset, reader.TokenStartIndex);
        }

        private static void ThrowDuplicateKeyException<T> (ref Utf8JsonStreamReader reader, T key)
            => throw new WorkloadManifestFormatException(Strings.DuplicateKeyAtOffset, key?.ToString() ?? throw new ArgumentNullException (nameof(key)), reader.TokenStartIndex);

        private static WorkloadManifest ReadWorkloadManifest(
            string id, string manifestPath,
            LocalizationCatalog? localizationCatalog,
            ref Utf8JsonStreamReader reader)
        {
            ConsumeToken(ref reader, JsonTokenType.StartObject);

            FXVersion? version = null;
            string? description = null;
            Dictionary<WorkloadId, BaseWorkloadDefinition>? workloads = null;
            Dictionary<WorkloadPackId, WorkloadPack>? packs = null;
            Dictionary<string, FXVersion>? dependsOn = null;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var propName = reader.GetString();

                        if (string.Equals("version", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (version != null) ThrowDuplicateKeyException(ref reader, propName);
                            if (reader.Read())
                            {
                                if (reader.TokenType == JsonTokenType.String)
                                {
                                    if (FXVersion.TryParse(reader.GetString(), out version))
                                    {
                                        continue;
                                    }
                                }
                                else if (reader.TokenType.IsInt())
                                {
                                    // older manifests could have an int value
                                    if (reader.TryGetInt64(out var intVersion) && intVersion < int.MaxValue)
                                    {
                                        version = new FXVersion((int)intVersion, 0, 0);
                                        continue;
                                    }
                                }
                            }
                            throw new WorkloadManifestFormatException(Strings.MissingOrInvalidManifestVersion);
                        }

                        if (string.Equals("description", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (description != null) ThrowDuplicateKeyException(ref reader, propName);
                            description = ReadString(ref reader);
                            continue;
                        }

                        if (string.Equals("depends-on", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (dependsOn != null) ThrowDuplicateKeyException(ref reader, propName);
                            dependsOn = ReadDependsOn(ref reader);
                            continue;
                        }

                        if (string.Equals("workloads", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (workloads != null) ThrowDuplicateKeyException(ref reader, propName);
                            workloads = ReadWorkloadDefinitions(ref reader, localizationCatalog);
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
                        throw new WorkloadManifestFormatException(Strings.UnknownKeyAtOffset, propName, reader.TokenStartIndex);
                    case JsonTokenType.EndObject:

                        if (version == null)
                        {
                            throw new WorkloadManifestFormatException(Strings.MissingOrInvalidManifestVersion);
                        }

                        return new WorkloadManifest (
                            id,
                            version,
                            description,
                            manifestPath,
                            workloads ?? new Dictionary<WorkloadId, BaseWorkloadDefinition> (),
                            packs ?? new Dictionary<WorkloadPackId, WorkloadPack> (),
                            dependsOn
                        );
                    default:
                        throw new WorkloadManifestFormatException(Strings.UnexpectedTokenAtOffset, reader.TokenType, reader.TokenStartIndex);
                }
            }

            throw new WorkloadManifestFormatException(Strings.IncompleteDocument);
        }

        /// <summary>
        /// this expects the reader to be before the value token, and leaves it on the last token of the value
        /// </summary>
        private static bool ConsumeValue (ref Utf8JsonStreamReader reader)
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

        private static Dictionary<string, FXVersion> ReadDependsOn(ref Utf8JsonStreamReader reader)
        {
            ConsumeToken(ref reader, JsonTokenType.StartObject);

            var dependsOn = new Dictionary<string, FXVersion>(StringComparer.OrdinalIgnoreCase);

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var dependencyId = reader.GetString();
                        if (FXVersion.TryParse(ReadString(ref reader), out var dependencyVersion))
                        {
                            if (dependsOn.ContainsKey(dependencyId))
                            {
                                ThrowDuplicateKeyException(ref reader, dependencyId);
                            }
                            dependsOn.Add(dependencyId, dependencyVersion);
                            continue;
                        }
                        goto default;
                    case JsonTokenType.EndObject:
                        return dependsOn;
                    default:
                        throw new WorkloadManifestFormatException(Strings.UnexpectedTokenAtOffset, reader.TokenType, reader.TokenStartIndex);
                }
            }

            throw new WorkloadManifestFormatException(Strings.IncompleteDocument);
        }

        private static Dictionary<WorkloadId, BaseWorkloadDefinition> ReadWorkloadDefinitions(ref Utf8JsonStreamReader reader, LocalizationCatalog? localizationCatalog)
        {
            ConsumeToken(ref reader, JsonTokenType.StartObject);

            var workloads = new Dictionary<WorkloadId, BaseWorkloadDefinition>();

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var workloadId = new WorkloadId(reader.GetString());
                        var workload = ReadWorkloadDefinition(workloadId, ref reader, localizationCatalog);
                        if (workloads.ContainsKey(workloadId)) ThrowDuplicateKeyException(ref reader, workloadId);
                        workloads.Add(workloadId, workload);
                        continue;
                    case JsonTokenType.EndObject:
                        return workloads;
                    default:
                        throw new WorkloadManifestFormatException(Strings.UnexpectedTokenAtOffset, reader.TokenType, reader.TokenStartIndex);
                }
            }

            throw new WorkloadManifestFormatException(Strings.IncompleteDocument);
        }

        private static Dictionary<WorkloadPackId, WorkloadPack> ReadWorkloadPacks(ref Utf8JsonStreamReader reader)
        {
            ConsumeToken(ref reader, JsonTokenType.StartObject);

            var packs = new Dictionary<WorkloadPackId, WorkloadPack>();

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var packId = new WorkloadPackId (reader.GetString());
                        var pack = ReadWorkloadPack(packId,ref reader);
                        if (packs.ContainsKey(packId)) ThrowDuplicateKeyException(ref reader, packId);
                        packs[packId] = pack;
                        continue;
                    case JsonTokenType.EndObject:
                        return packs;
                    default:
                        throw new WorkloadManifestFormatException(Strings.UnexpectedTokenAtOffset, reader.TokenType, reader.TokenStartIndex);
                }
            }

            throw new WorkloadManifestFormatException(Strings.IncompleteDocument);
        }

        private static List<string> ReadStringArray(ref Utf8JsonStreamReader reader)
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
                        throw new WorkloadManifestFormatException(Strings.UnexpectedTokenAtOffset, reader.TokenType, reader.TokenStartIndex);
                }
            }

            throw new WorkloadManifestFormatException(Strings.IncompleteDocument);
        }

        private static List<T> ReadStringArray<T>(ref Utf8JsonStreamReader reader, Func<string, T> map)
        {
            ConsumeToken(ref reader, JsonTokenType.StartArray);

            var list = new List<T>();

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.String:
                        list.Add(map(reader.GetString()));
                        continue;
                    case JsonTokenType.EndArray:
                        return list;
                    default:
                        throw new WorkloadManifestFormatException(Strings.UnexpectedTokenAtOffset, reader.TokenType, reader.TokenStartIndex);
                }
            }

            throw new WorkloadManifestFormatException(Strings.IncompleteDocument);
        }

        private static Dictionary<string,string> ReadStringDictionary(ref Utf8JsonStreamReader reader)
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
                        throw new WorkloadManifestFormatException(Strings.UnexpectedTokenAtOffset, reader.TokenType, reader.TokenStartIndex);
                }
            }

            throw new WorkloadManifestFormatException(Strings.IncompleteDocument);
        }

        private static Dictionary<string, TValue> ReadStringDictionary<TValue>(ref Utf8JsonStreamReader reader, Func<string,TValue> mapValue)
        {
            ConsumeToken(ref reader, JsonTokenType.StartObject);

            var dictionary = new Dictionary<string, TValue>();

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var name = reader.GetString();
                        var val = mapValue(ReadString(ref reader));
                        if (dictionary.ContainsKey(name)) ThrowDuplicateKeyException(ref reader, name);
                        dictionary.Add(name, val);
                        continue;
                    case JsonTokenType.EndObject:
                        return dictionary;
                    default:
                        throw new WorkloadManifestFormatException(Strings.UnexpectedTokenAtOffset, reader.TokenType, reader.TokenStartIndex);
                }
            }

            throw new WorkloadManifestFormatException(Strings.IncompleteDocument);
        }

        private static BaseWorkloadDefinition ReadWorkloadDefinition(WorkloadId id, ref Utf8JsonStreamReader reader, LocalizationCatalog? localizationCatalog)
        {
            ConsumeToken(ref reader, JsonTokenType.StartObject);

            string? description = null;
            bool? isAbstractOrNull = null;
            WorkloadDefinitionKind? kind = null;
            List<WorkloadPackId>? packs = null;
            List<WorkloadId>? extends = null;
            List<string>? platforms = null;
            WorkloadId? replaceWith = null;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        var propName = reader.GetString();

                        if (string.Equals("description", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (description != null) ThrowDuplicateKeyException(ref reader, propName);
                            description = ReadStringLocalized(ref reader, localizationCatalog, $"workloads/{id}/description");
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
                                throw new WorkloadManifestFormatException(Strings.UnknownWorkloadDefinitionKind, kindStr, reader.TokenStartIndex);

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
                            packs = ReadStringArray<WorkloadPackId>(ref reader, s => new WorkloadPackId(s));
                            continue;
                        }

                        if (string.Equals("extends", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (extends != null) ThrowDuplicateKeyException(ref reader, propName);
                            extends = ReadStringArray<WorkloadId>(ref reader, s => new WorkloadId(s));
                            continue;
                        }

                        if (string.Equals("platforms", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (platforms != null) ThrowDuplicateKeyException(ref reader, propName);
                            platforms = ReadStringArray(ref reader);
                            continue;
                        }

                        if (string.Equals("replace-with", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (replaceWith != null) ThrowDuplicateKeyException(ref reader, propName);
                            replaceWith = new WorkloadId (ReadString(ref reader));
                            continue;
                        }

                        throw new WorkloadManifestFormatException(Strings.UnknownKeyAtOffset, propName, reader.TokenStartIndex);
                    case JsonTokenType.EndObject:
                        if (replaceWith is WorkloadId replacementId)
                        {
                            if (isAbstractOrNull != null || description != null || kind != null || extends != null || packs != null || platforms != null)
                            {
                                throw new WorkloadManifestFormatException(Strings.RedirectWorkloadHasOtherKeys, id);
                            }
                            throw new NotImplementedException("Workload redirects are not yet fully implemented");
                            //return new WorkloadRedirect (id, replacementId);
                        }
                        var isAbstract = isAbstractOrNull ?? false;
                        if (!isAbstract && kind == WorkloadDefinitionKind.Dev && string.IsNullOrEmpty (description))
                        {
                            throw new WorkloadManifestFormatException(Strings.ConcreteWorkloadHasNoDescription, id);
                        }
                        return new WorkloadDefinition (id, isAbstract, description, kind ?? WorkloadDefinitionKind.Dev, extends, packs, platforms);
                    default:
                        throw new WorkloadManifestFormatException(Strings.UnexpectedTokenAtOffset, reader.TokenType, reader.TokenStartIndex);
                }
            }

            throw new WorkloadManifestFormatException(Strings.IncompleteDocument);
        }

        private static WorkloadPack ReadWorkloadPack(WorkloadPackId id, ref Utf8JsonStreamReader reader)
        {
            ConsumeToken(ref reader, JsonTokenType.StartObject);

            string? version = null;
            WorkloadPackKind? kind = null;
            Dictionary<string, WorkloadPackId>? aliasTo = null;

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
                                throw new WorkloadManifestFormatException(Strings.UnknownWorkloadPackKind, kindStr, reader.TokenStartIndex);

                            }
                            continue;
                        }

                        if (string.Equals("alias-to", propName, StringComparison.OrdinalIgnoreCase))
                        {
                            if (aliasTo != null) ThrowDuplicateKeyException(ref reader, propName);
                            aliasTo = ReadStringDictionary(ref reader, s => new WorkloadPackId(s));
                            continue;
                        }

                        throw new WorkloadManifestFormatException(Strings.UnknownKeyAtOffset, propName, reader.TokenStartIndex);
                    case JsonTokenType.EndObject:
                        if (version == null)
                        {
                            throw new WorkloadManifestFormatException(Strings.MissingWorkloadPackVersion, id);
                        }
                        if (kind == null)
                        {
                            throw new WorkloadManifestFormatException(Strings.MissingWorkloadPackKind, id);
                        }
                        return new WorkloadPack (id, version, kind.Value, aliasTo);
                    default:
                        throw new WorkloadManifestFormatException(Strings.UnexpectedTokenAtOffset, reader.TokenType, reader.TokenStartIndex);
                }
            }

            throw new WorkloadManifestFormatException(Strings.IncompleteDocument);
        }
    }
}
