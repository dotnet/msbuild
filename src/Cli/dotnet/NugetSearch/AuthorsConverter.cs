// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.NugetSearch
{
    /// <summary>
    /// Author field could be a string or a string array
    /// </summary>
    internal class AuthorsConverter : JsonConverter<NugetSearchApiAuthorsSerializable>
    {
        public override NugetSearchApiAuthorsSerializable Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var doc = JsonDocument.ParseValue(ref reader);
                var resultAuthors = doc.RootElement.EnumerateArray().Select(author => author.GetString()).ToArray();
                return new NugetSearchApiAuthorsSerializable() { Authors = resultAuthors };
            }
            else
            {
                var s = reader.GetString();
                return new NugetSearchApiAuthorsSerializable() { Authors = new string[] { s } };
            }
        }

        public override void Write(Utf8JsonWriter writer, NugetSearchApiAuthorsSerializable value,
            JsonSerializerOptions options)
        {
            // only deserialize is used
            throw new NotImplementedException();
        }
    }
}
