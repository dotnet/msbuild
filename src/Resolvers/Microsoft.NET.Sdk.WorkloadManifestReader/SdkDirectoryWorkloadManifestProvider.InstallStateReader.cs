// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.NET.Sdk.Localization;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadManifestReader;
using System.Runtime.Serialization;
using Microsoft.Deployment.DotNet.Releases;

#if USE_SYSTEM_TEXT_JSON
using System.Text.Json;
#else
using Newtonsoft.Json;
using JsonTokenType = Newtonsoft.Json.JsonToken;
#endif

namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public partial class SdkDirectoryWorkloadManifestProvider
    {
        class InstallState
        {
            public string? WorkloadSetVersion { get; set; }
            public WorkloadSet? Manifests { get; set; }
        }

        static class InstallStateReader
        {
            public static InstallState ReadInstallState(string installStatePath)
            {
                using var fileStream = File.OpenRead(installStatePath);

#if USE_SYSTEM_TEXT_JSON
                var readerOptions = new JsonReaderOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                };
                var reader = new Utf8JsonStreamReader(fileStream, readerOptions);
#else
                using var textReader = new StreamReader(fileStream, System.Text.Encoding.UTF8, true);
                using var jsonReader = new JsonTextReader(textReader);

                var reader = new Utf8JsonStreamReader(jsonReader);
#endif

                InstallState installState = new();
                
                JsonReader.ConsumeToken(ref reader, JsonTokenType.StartObject);
                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.PropertyName:
                            var propName = reader.GetString();
                            if (string.Equals("workloadVersion", propName, StringComparison.OrdinalIgnoreCase))
                            {
                                installState.WorkloadSetVersion = JsonReader.ReadString(ref reader);
                            }
                            else if (string.Equals("manifests", propName, StringComparison.OrdinalIgnoreCase))
                            {
                                installState.Manifests = ReadManifests(ref reader);
                            }
                            else
                            {
                                JsonReader.ConsumeValue(ref reader);
                            }
                            break;

                        case JsonTokenType.EndObject:
                            return installState;
                        default:
                            throw new JsonFormatException(Strings.UnexpectedTokenAtOffset, reader.TokenType, reader.TokenStartIndex);
                    }
                }

                throw new JsonFormatException(Strings.IncompleteDocument);
            }

            static WorkloadSet ReadManifests(ref Utf8JsonStreamReader reader)
            {
                JsonReader.ConsumeToken(ref reader, JsonTokenType.StartObject);
                Dictionary<string, string> workloadSetDict = new();

                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.PropertyName:
                            var propName = reader.GetString();
                            var propValue = JsonReader.ReadString(ref reader);
                            workloadSetDict[propName] = propValue;
                            break;
                        case JsonTokenType.EndObject:
                            return WorkloadSet.FromDictionaryForJson(workloadSetDict, new SdkFeatureBand(new ReleaseVersion(0,0,0)));
                        default:
                            throw new JsonFormatException(Strings.UnexpectedTokenAtOffset, reader.TokenType, reader.TokenStartIndex);
                    }
                }
                throw new JsonFormatException(Strings.IncompleteDocument);
            }
        }
    }
}

