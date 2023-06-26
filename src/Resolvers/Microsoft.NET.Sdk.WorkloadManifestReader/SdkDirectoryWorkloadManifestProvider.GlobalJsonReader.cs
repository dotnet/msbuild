// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.NET.Sdk.Localization;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadManifestReader;
using System.Runtime.Serialization;

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
            public static string? GetWorkloadVersionFromGlobalJson(string globalJsonPath)
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
                using var textReader = new StreamReader(fileStream, System.Text.Encoding.UTF8, true);
                using var jsonReader = new JsonTextReader(textReader);

                var reader = new Utf8JsonStreamReader(jsonReader);
#endif

                string? workloadVersion = null;

                ConsumeToken(ref reader, JsonTokenType.StartObject);
                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.PropertyName:
                            var propName = reader.GetString();
                            if (string.Equals("sdk", propName, StringComparison.OrdinalIgnoreCase))
                            {
                                ConsumeToken(ref reader, JsonTokenType.StartObject);

                                bool readingSdk = true;
                                while (readingSdk && reader.Read())
                                {
                                    switch (reader.TokenType)
                                    {
                                        case JsonTokenType.PropertyName:
                                            var sdkPropName = reader.GetString();
                                            if (string.Equals("workloadVersion", sdkPropName, StringComparison.OrdinalIgnoreCase))
                                            {
                                                workloadVersion = ReadString(ref reader);
                                            }
                                            else
                                            {
                                                ConsumeValue(ref reader);
                                            }
                                            break;
                                        case JsonTokenType.EndObject:
                                            readingSdk = false;
                                            break;
                                        default:
                                            throw new GlobalJsonFormatException(Strings.UnexpectedTokenAtOffset, reader.TokenType, reader.TokenStartIndex);
                                    }
                                }
                            }
                            else
                            {
                                ConsumeValue(ref reader);
                            }
                            break;

                        case JsonTokenType.EndObject:
                            return workloadVersion;
                        default:
                            throw new GlobalJsonFormatException(Strings.UnexpectedTokenAtOffset, reader.TokenType, reader.TokenStartIndex);
                    }
                }

                throw new GlobalJsonFormatException(Strings.IncompleteDocument);
            }

            /// <summary>
            /// this expects the reader to be before the value token, and leaves it on the last token of the value
            /// </summary>
            private static bool ConsumeValue(ref Utf8JsonStreamReader reader)
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
                    throw new GlobalJsonFormatException(Strings.ExpectedTokenAtOffset, expected, reader.TokenStartIndex);
                }

                throw new GlobalJsonFormatException(key, reader.TokenStartIndex);
            }

            private static string ReadString(ref Utf8JsonStreamReader reader)
            {
                ConsumeToken(ref reader, JsonTokenType.String);
                return reader.GetString();
            }
        }

        [Serializable]
        internal class GlobalJsonFormatException : Exception
        {
            public GlobalJsonFormatException() { }
            public GlobalJsonFormatException(string messageFormat, params object?[] args) : base(string.Format(messageFormat, args)) { }
            public GlobalJsonFormatException(string message) : base(message) { }
            public GlobalJsonFormatException(string message, Exception inner) : base(message, inner) { }
        #if NET8_0_OR_GREATER
            [Obsolete(DiagnosticId = "SYSLIB0051")] // add this attribute to the serialization ctor
        #endif
            protected GlobalJsonFormatException(SerializationInfo info, StreamingContext context) : base(info, context) { }
        }
    }
}

