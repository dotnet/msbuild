// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.MSBuildSdkResolver;
using Strings = Microsoft.NET.Sdk.Localization.Strings;


#if USE_SYSTEM_TEXT_JSON
using System.Text.Json;
#else
using Newtonsoft.Json;
#endif


namespace Microsoft.NET.Sdk.WorkloadManifestReader
{
    public class WorkloadSet
    {
        public Dictionary<ManifestId, (ManifestVersion Version, SdkFeatureBand FeatureBand)> ManifestVersions = new Dictionary<ManifestId, (ManifestVersion version, SdkFeatureBand featureBand)>();

        //  TODO: Generate version from hash of manifest versions if not otherwise set
        public string? Version { get; set; }

        public static WorkloadSet FromManifests(IEnumerable<WorkloadManifestInfo> manifests)
        {
            return new WorkloadSet()
            {
                ManifestVersions = manifests.ToDictionary(m => new ManifestId(m.Id), m => (new ManifestVersion(m.Version), new SdkFeatureBand(m.ManifestFeatureBand)))
            };
        }

        public static WorkloadSet FromDictionaryForJson(IDictionary<string, string> dictionary, SdkFeatureBand defaultFeatureBand)
        {
            var manifestVersions = dictionary
                .Select(manifest =>
                {
                    ManifestVersion manifestVersion;
                    SdkFeatureBand manifestFeatureBand;
                    var parts = manifest.Value.Split('/');

                    string manifestVersionString = parts[0];
                    if (!FXVersion.TryParse(manifestVersionString, out FXVersion version))
                    {
                        throw new FormatException(String.Format(Strings.InvalidVersionForWorkload, manifest.Key, manifestVersionString));
                    }

                    manifestVersion = new ManifestVersion(parts[0]);
                    if (parts.Length == 1)
                    {
                        manifestFeatureBand = defaultFeatureBand;
                    }
                    else
                    {
                        manifestFeatureBand = new SdkFeatureBand(parts[1]);
                    }
                    return (id: new ManifestId(manifest.Key), manifestVersion, manifestFeatureBand);
                }).ToDictionary(t => t.id, t => (t.manifestVersion, t.manifestFeatureBand));

            return new WorkloadSet()
            {
                ManifestVersions = manifestVersions
            };
        }

        public static WorkloadSet FromJson(string json, SdkFeatureBand defaultFeatureBand)
        {
#if USE_SYSTEM_TEXT_JSON
            var jsonSerializerOptions = new JsonSerializerOptions()
            {
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            return FromDictionaryForJson(JsonSerializer.Deserialize<IDictionary<string, string>>(json, jsonSerializerOptions)!, defaultFeatureBand);
#else
            return FromDictionaryForJson(JsonConvert.DeserializeObject<IDictionary<string, string>>(json)!, defaultFeatureBand);
#endif
        }

        public Dictionary<string, string> ToDictionaryForJson()
        {
            var dictionary = ManifestVersions.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value.Version + "/" + kvp.Value.FeatureBand, StringComparer.OrdinalIgnoreCase);
            return dictionary;
        }

        public string ToJson()
        {
#if USE_SYSTEM_TEXT_JSON
            var json = JsonSerializer.Serialize(ToDictionaryForJson(), new JsonSerializerOptions() { WriteIndented = true });
#else
            var json = JsonConvert.SerializeObject(ToDictionaryForJson(), Formatting.Indented);
#endif
            return json;
        }

    }
}
