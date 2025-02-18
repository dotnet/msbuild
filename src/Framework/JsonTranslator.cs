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

            public void TranslateCulture(string propertyName, ref CultureInfo culture)
            {
                if (_document.RootElement.TryGetProperty(propertyName, out JsonElement element))
                {
                    string cultureName = element.GetString();
                    culture = !string.IsNullOrEmpty(cultureName)
                        ? CultureInfo.GetCultureInfo(cultureName)
                        : null;
                }
            }

            public void TranslateDictionary<TKey, TValue>(
                JsonSerializerOptions jsonSerializerOptions,
                string propertyName,
                ref Dictionary<TKey, TValue> dictionary,
                IEqualityComparer<TKey> comparer,
                Func<TValue> valueFactory = null)
            {
                if (!_document.RootElement.TryGetProperty(propertyName, out JsonElement element))
                {
                    dictionary = null;
                    return;
                }

                dictionary = JsonSerializer.Deserialize<Dictionary<TKey, TValue>>(element.GetRawText(), jsonSerializerOptions);
            }

            public T TranslateFromJson<T>(JsonSerializerOptions jsonSerializerOptions = null) => JsonSerializer.Deserialize<T>(_document.RootElement.GetRawText(), jsonSerializerOptions);

            public void TranslateToJson<T>(T model, JsonSerializerOptions jsonSerializerOptions = null)
            {
                throw new InvalidOperationException("Cannot write to a read-only translator");
            }

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
                Debugger.Launch();
                _stream = stream;
                _writer = new Utf8JsonWriter(_stream, new JsonWriterOptions
                {
                    Indented = true
                });
            }

            public TranslationDirection Mode => TranslationDirection.WriteToStream;

            public ProtocolType Protocol => ProtocolType.Json;

            public void TranslateCulture(string propertyName, ref CultureInfo culture)
            {
                _writer.WritePropertyName(propertyName);
                if (culture != null)
                {
                    _writer.WriteStringValue(culture.Name);
                }
                else
                {
                    _writer.WriteNullValue();
                }
            }

            public void TranslateDictionary<TKey, TValue>(
                JsonSerializerOptions jsonSerializerOptions,
                string propertyName,
                ref Dictionary<TKey, TValue> dictionary,
                IEqualityComparer<TKey> comparer,
                Func<TValue> valueFactory = null)
            {
                _writer.WritePropertyName(propertyName);

                if (dictionary == null)
                {
                    _writer.WriteNullValue();
                    return;
                }

                _writer.WriteStartObject();

                foreach (var kvp in dictionary)
                {
                    _writer.WritePropertyName(kvp.Key.ToString());

                    JsonSerializer.Serialize(_writer, kvp.Value, typeof(TValue), jsonSerializerOptions);
                }

                _writer.WriteEndObject();
            }


            private void WriteValue(object value, JsonSerializerOptions jsonSerializerOptions)
            {
                switch (value)
                {
                    case null:
                        _writer.WriteNullValue();
                        break;
                    case string str:
                        _writer.WriteStringValue(str);
                        break;
                    case int i:
                        _writer.WriteNumberValue(i);
                        break;
                    case long l:
                        _writer.WriteNumberValue(l);
                        break;
                    case double d:
                        _writer.WriteNumberValue(d);
                        break;
                    case float f:
                        _writer.WriteNumberValue(f);
                        break;
                    case decimal dec:
                        _writer.WriteNumberValue(dec);
                        break;
                    case bool b:
                        _writer.WriteBooleanValue(b);
                        break;
                    case DateTime dt:
                        _writer.WriteStringValue(dt);
                        break;
                    case ITaskItem taskItem:
                        WriteTaskItem(taskItem);
                        break;
                    case ITaskItem[] taskItems:
                        WriteTaskItemArray(taskItems);
                        break;
                    case IEnumerable enumerable:
                        WriteEnumerable(enumerable, jsonSerializerOptions);
                        break;
                    default:
                        JsonSerializer.Serialize(_writer, value, value.GetType(), jsonSerializerOptions);
                        break;
                }
            }

            private void WriteTaskItem(ITaskItem taskItem)
            {
                _writer.WriteStartObject();

                _writer.WritePropertyName("itemSpec");
                _writer.WriteStringValue(taskItem.ItemSpec);

                if (taskItem.MetadataCount > 0)
                {
                    _writer.WritePropertyName("metadata");
                    _writer.WriteStartObject();

                    foreach (string name in taskItem.MetadataNames)
                    {
                        _writer.WritePropertyName(name);
                        _writer.WriteStringValue(taskItem.GetMetadata(name));
                    }

                    _writer.WriteEndObject();
                }

                _writer.WriteEndObject();
            }

            private void WriteTaskItemArray(ITaskItem[] taskItems)
            {
                _writer.WriteStartArray();

                foreach (var item in taskItems)
                {
                    WriteTaskItem(item);
                }

                _writer.WriteEndArray();
            }

            private void WriteEnumerable(IEnumerable enumerable, JsonSerializerOptions jsonSerializerOptions)
            {
                _writer.WriteStartArray();

                foreach (var item in enumerable)
                {
                    WriteValue(item, jsonSerializerOptions);
                }

                _writer.WriteEndArray();
            }

            public T TranslateFromJson<T>(JsonSerializerOptions jsonSerializerOptions = null)
            {
                throw new InvalidOperationException("Cannot read from a write-only translator");
            }

            public void TranslateToJson<T>(T model, JsonSerializerOptions jsonSerializerOptions = null)
            {
                JsonSerializer.Serialize(_writer, model, jsonSerializerOptions);
            }

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
}
#endif
