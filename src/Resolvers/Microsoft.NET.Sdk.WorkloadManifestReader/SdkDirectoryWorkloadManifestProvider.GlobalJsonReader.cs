// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using Microsoft.NET.Sdk.Localization;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadManifestReader;

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
        static class GlobalJsonReader
        {
            public static string? GetWorkloadVersionFromGlobalJson(string? globalJsonPath)
            {
                if (string.IsNullOrEmpty(globalJsonPath))
                {
                    return null;
                }

                using var fileStream = File.OpenRead(globalJsonPath);

#if USE_SYSTEM_TEXT_JSON
                var readerOptions = new JsonReaderOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                };
                var reader = new Utf8JsonStreamReader(fileStream, readerOptions);
#else
                using var textReader = new StreamReader(fileStream, Encoding.UTF8, true);
                using var jsonReader = new JsonTextReader(textReader);

                var reader = new Utf8JsonStreamReader(jsonReader);
#endif

                string? workloadVersion = null;

                JsonReader.ConsumeToken(ref reader, JsonTokenType.StartObject);
                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.PropertyName:
                            var propName = reader.GetString();
                            if (string.Equals("sdk", propName, StringComparison.OrdinalIgnoreCase))
                            {
                                JsonReader.ConsumeToken(ref reader, JsonTokenType.StartObject);

                                bool readingSdk = true;
                                while (readingSdk && reader.Read())
                                {
                                    switch (reader.TokenType)
                                    {
                                        case JsonTokenType.PropertyName:
                                            var sdkPropName = reader.GetString();
                                            if (string.Equals("workloadVersion", sdkPropName, StringComparison.OrdinalIgnoreCase))
                                            {
                                                workloadVersion = JsonReader.ReadString(ref reader);
                                            }
                                            else
                                            {
                                                JsonReader.ConsumeValue(ref reader);
                                            }
                                            break;
                                        case JsonTokenType.EndObject:
                                            readingSdk = false;
                                            break;
                                        default:
                                            throw new JsonFormatException(Strings.UnexpectedTokenAtOffset, reader.TokenType, reader.TokenStartIndex);
                                    }
                                }
                            }
                            else
                            {
                                JsonReader.ConsumeValue(ref reader);
                            }
                            break;

                        case JsonTokenType.EndObject:
                            return workloadVersion;
                        default:
                            throw new JsonFormatException(Strings.UnexpectedTokenAtOffset, reader.TokenType, reader.TokenStartIndex);
                    }
                }

                throw new JsonFormatException(Strings.IncompleteDocument);
            }
        }
    }
}

