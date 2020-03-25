// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
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
                return new NugetSearchApiAuthorsSerializable() {Authors = resultAuthors};
            }
            else
            {
                var s = reader.GetString();
                return new NugetSearchApiAuthorsSerializable() {Authors = new string[] {s}};
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
