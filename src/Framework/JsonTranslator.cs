// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !TASKHOST && !NETSTANDARD

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    internal static class JsonTranslator
    {
        internal static IJsonTranslator GetReadTranslator(Stream stream, int packetLength) => new JsonReadTranslator(stream, packetLength);

        internal static IJsonTranslator GetWriteTranslator(Stream stream) => new JsonWriteTranslator(stream);

        private class JsonReadTranslator : IJsonTranslator
        {
            private readonly JsonDocument _document;
            private bool _disposed;

            public JsonReadTranslator(Stream stream, int packetLength)
            {
                byte[] buffer = new byte[packetLength];
                int bytesRead = stream.Read(buffer, 0, packetLength);
                if (bytesRead != packetLength)
                {
                    throw new IOException($"Expected to read {packetLength} bytes but got {bytesRead}");
                }

                _document = JsonDocument.Parse(buffer);
            }

            public TranslationDirection Mode => TranslationDirection.ReadFromStream;

            public ProtocolType Protocol => ProtocolType.Json;

            public void Translate<T>(ref T model, JsonSerializerOptions jsonSerializerOptions = null) => model = JsonSerializer.Deserialize<T>(_document.RootElement.GetRawText(), jsonSerializerOptions);

            public void Dispose()
            {
                if (!_disposed)
                {
                    _document?.Dispose();
                    _disposed = true;
                }
            }
        }

        private class JsonWriteTranslator : IJsonTranslator
        {
            private readonly Stream _stream;
            private readonly Utf8JsonWriter _writer;
            private bool _disposed;

            public JsonWriteTranslator(Stream stream)
            {
                _stream = stream;
                _writer = new Utf8JsonWriter(_stream);
            }

            public TranslationDirection Mode => TranslationDirection.WriteToStream;

            public ProtocolType Protocol => ProtocolType.Json;

            public void Translate<T>(ref T model, JsonSerializerOptions jsonSerializerOptions = null) => JsonSerializer.Serialize(_writer, model, jsonSerializerOptions);

            public void Dispose()
            {
                if (!_disposed)
                {
                    _writer?.Dispose();
                    _stream?.Dispose();
                    _disposed = true;
                }
            }
        }
    }

    internal static class JsonTranslatorExtensions
    {
        internal static object GetNumberValue(JsonElement valueElement) =>
            (valueElement.TryGetInt32(out int intValue), valueElement.TryGetInt64(out long longValue)) switch
            {
                (true, _) => intValue,
                (false, true) => longValue,
                _ => valueElement.GetDouble()
            };

        internal static void WriteValue(Utf8JsonWriter writer, object value, JsonSerializerOptions jsonSerializerOptions)
        {
            switch (value)
            {
                case null:
                    writer.WriteNullValue();
                    break;
                case string str:
                    writer.WriteStringValue(str);
                    break;
                case int i:
                    writer.WriteNumberValue(i);
                    break;
                case long l:
                    writer.WriteNumberValue(l);
                    break;
                case double d:
                    writer.WriteNumberValue(d);
                    break;
                case float f:
                    writer.WriteNumberValue(f);
                    break;
                case decimal dec:
                    writer.WriteNumberValue(dec);
                    break;
                case bool b:
                    writer.WriteBooleanValue(b);
                    break;
                case DateTime dt:
                    writer.WriteStringValue(dt);
                    break;
                case ITaskItem taskItem:
                    WriteTaskItem(writer, taskItem);
                    break;
                case ITaskItem[] taskItems:
                    WriteTaskItemArray(writer, taskItems);
                    break;
                case IEnumerable enumerable:
                    WriteEnumerable(writer, enumerable, jsonSerializerOptions);
                    break;
                default:
                    JsonSerializer.Serialize(writer, value, value.GetType(), jsonSerializerOptions);
                    break;
            }
        }

        private static void WriteTaskItemArray(Utf8JsonWriter writer, ITaskItem[] taskItems)
        {
            writer.WriteStartArray();

            foreach (var item in taskItems)
            {
                WriteTaskItem(writer, item);
            }

            writer.WriteEndArray();
        }

        private static void WriteEnumerable(Utf8JsonWriter writer, IEnumerable enumerable, JsonSerializerOptions jsonSerializerOptions)
        {
            writer.WriteStartArray();

            foreach (var item in enumerable)
            {
                WriteValue(writer, item, jsonSerializerOptions);
            }

            writer.WriteEndArray();
        }

        private static void WriteTaskItem(Utf8JsonWriter writer, ITaskItem taskItem)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("itemSpec");
            writer.WriteStringValue(taskItem.ItemSpec);

            if (taskItem.MetadataCount > 0)
            {
                writer.WritePropertyName("metadata");
                writer.WriteStartObject();

                foreach (string name in taskItem.MetadataNames)
                {
                    writer.WritePropertyName(name);
                    writer.WriteStringValue(taskItem.GetMetadata(name));
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }
    }
}
#endif
