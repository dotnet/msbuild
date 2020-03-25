// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Search;

namespace Microsoft.DotNet.NugetSearch
{
    internal static class NugetSearchApiResultDeserializer
    {
        public static IReadOnlyCollection<SearchResultPackage> Deserialize(string json)
        {
            var options = new JsonSerializerOptions
            {
                Converters = {new AuthorsConverter()},
                AllowTrailingCommas = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var deserialized = JsonSerializer.Deserialize<NugetSearchApiContainerSerializable>(json, options);
            var resultPackages = new List<SearchResultPackage>();
            foreach (var deserializedPackage in deserialized.Data)
            {
                var versions =
                    deserializedPackage.Versions.Select(v => new SearchResultPackageVersion(v.Version, v.Downloads))
                        .ToArray();

                string[] authors = deserializedPackage?.Authors?.Authors ?? Array.Empty<string>();

                var searchResultPackage = new SearchResultPackage(new PackageId(deserializedPackage.Id),
                    deserializedPackage.Version, deserializedPackage.Description, deserializedPackage.Summary,
                    deserializedPackage.Tags, authors, deserializedPackage.TotalDownloads, deserializedPackage.Verified,
                    versions);

                resultPackages.Add(searchResultPackage);
            }

            return resultPackages;
        }
    }
}
