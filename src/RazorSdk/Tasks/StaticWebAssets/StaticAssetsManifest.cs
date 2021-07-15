// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.Razor.Tasks
{
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class StaticWebAssetsManifest : IEquatable<StaticWebAssetsManifest>
    {
        internal static StaticWebAssetsManifest Create(
            string source,
            string basePath,
            string mode,
            string manifestType,
            ManifestReference[] relatedManifests,
            DiscoveryPattern[] discoveryPatterns,
            StaticWebAsset[] assets)
        {
            var result = new StaticWebAssetsManifest()
            {
                Version = 1,
                Source = source,
                BasePath = basePath,
                Mode = mode,
                ManifestType = manifestType,
                RelatedManifests = relatedManifests,
                DiscoveryPatterns = discoveryPatterns,
                Assets = assets
            };
            result.Hash = result.ComputeManifestHash();
            return result;
        }

        private string ComputeManifestHash()
        {
            using var stream = new MemoryStream();

            var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { SkipValidation = true });
            JsonSerializer.Serialize(writer, Source);
            JsonSerializer.Serialize(writer, BasePath);
            JsonSerializer.Serialize(writer, Mode);
            JsonSerializer.Serialize(writer, ManifestType);
            JsonSerializer.Serialize(writer, RelatedManifests);
            JsonSerializer.Serialize(writer, DiscoveryPatterns);
            JsonSerializer.Serialize(writer, Assets);
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);

            using var sha256 = SHA256.Create();

            return Convert.ToBase64String(sha256.ComputeHash(stream));
        }

        public int Version { get; set; } = 1;

        public string Hash { get; set; }

        public string Source { get; set; }

        public string BasePath { get; set; }

        public string Mode { get; set; }

        public string ManifestType { get; set; }

        public ManifestReference[] RelatedManifests { get; set; }

        public DiscoveryPattern[] DiscoveryPatterns { get; set; }

        public StaticWebAsset[] Assets { get; set; }

        public static StaticWebAssetsManifest FromJsonBytes(byte[] jsonBytes)
        {
            var manifest = JsonSerializer.Deserialize<StaticWebAssetsManifest>(jsonBytes);
            if (manifest.Version != 1)
            {
                throw new InvalidOperationException($"Invalid manifest version. Expected manifest version '1' and found version '{manifest.Version}'.");
            }

            return manifest;
        }

        public static StaticWebAssetsManifest FromStream(Stream stream)
        {
            // Unfortunately the Stream overloads are not available on .net 472 so we need to do it this way.
            // That said, it doesn't matter since this method is only used for test purposes.
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            var manifest = JsonSerializer.Deserialize<StaticWebAssetsManifest>(memoryStream.ToArray());
            if (manifest.Version != 1)
            {
                throw new InvalidOperationException($"Invalid manifest version. Expected manifest version '1' and found version '{manifest.Version}'.");
            }

            return manifest;
        }

        public static StaticWebAssetsManifest FromJsonString(string jsonManifest)
        {
            var manifest = JsonSerializer.Deserialize<StaticWebAssetsManifest>(jsonManifest);
            if (manifest.Version != 1)
            {
                throw new InvalidOperationException($"Invalid manifest version. Expected manifest version '1' and found version '{manifest.Version}'.");
            }

            return manifest;
        }

        public override bool Equals(object obj) => Equals(obj as StaticWebAssetsManifest);
        public bool Equals(StaticWebAssetsManifest other) => 
            other != null
            && Version == other.Version
            && Hash == other.Hash
            && Source == other.Source
            && BasePath == other.BasePath
            && Mode == other.Mode
            && ManifestType == other.ManifestType
            && EqualityComparer<ManifestReference[]>.Default.Equals(RelatedManifests, other.RelatedManifests)
            && EqualityComparer<DiscoveryPattern[]>.Default.Equals(DiscoveryPatterns, other.DiscoveryPatterns)
            && EqualityComparer<StaticWebAsset[]>.Default.Equals(Assets, other.Assets);

        public override int GetHashCode()
        {
#if NET6_0_OR_GREATER
            HashCode hash = new HashCode();
            hash.Add(Version);
            hash.Add(Hash);
            hash.Add(Source);
            hash.Add(BasePath);
            hash.Add(Mode);
            hash.Add(ManifestType);
            hash.Add(RelatedManifests);
            hash.Add(DiscoveryPatterns);
            hash.Add(Assets);
            return hash.ToHashCode();
#else
            int hashCode = 1467594941;
            hashCode = hashCode * -1521134295 + Version.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Hash);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Source);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(BasePath);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Mode);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ManifestType);
            hashCode = hashCode * -1521134295 + EqualityComparer<ManifestReference[]>.Default.GetHashCode(RelatedManifests);
            hashCode = hashCode * -1521134295 + EqualityComparer<DiscoveryPattern[]>.Default.GetHashCode(DiscoveryPatterns);
            hashCode = hashCode * -1521134295 + EqualityComparer<StaticWebAsset[]>.Default.GetHashCode(Assets);
            return hashCode;
#endif
        }

        public class ManifestReference
        {
            internal static ManifestReference Create(string identity, string source, string manifestType, string projectFile, string hash)
            {
                return new ManifestReference()
                {
                    Identity = identity,
                    Source = source,
                    ManifestType = manifestType,
                    ProjectFile = projectFile,
                    Hash = hash,
                };
            }

            public string Identity { get; set; }

            public string Source { get; set; }

            public string ManifestType { get; set; }

            public string ProjectFile { get; set; }

            public string PublishTarget { get; set; }
            
            public string AdditionalPublishProperties { get; set; }

            public string AdditionalPublishPropertiesToRemove { get; set; }

            public string Hash { get; set; }

            public override bool Equals(object obj) => obj is ManifestReference reference
                && Identity == reference.Identity
                && Source == reference.Source
                && ManifestType == reference.ManifestType
                && ProjectFile == reference.ProjectFile
                && PublishTarget == reference.PublishTarget
                && AdditionalPublishProperties == reference.AdditionalPublishProperties
                && AdditionalPublishPropertiesToRemove == reference.AdditionalPublishPropertiesToRemove
                && Hash == reference.Hash;

            public override int GetHashCode()
            {
#if NET6_0_OR_GREATER
                return HashCode.Combine(Identity, Source, ManifestType, Hash, ProjectFile, PublishTarget, AdditionalPublishProperties, AdditionalPublishPropertiesToRemove);
#else
                int hashCode = -868952447;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Identity);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Source);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ManifestType);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ProjectFile);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(PublishTarget);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AdditionalPublishProperties);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AdditionalPublishPropertiesToRemove);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Hash);
                return hashCode;
#endif
            }

            internal ITaskItem ToTaskItem()
            {
                var result = new TaskItem(Identity);
                result.SetMetadata(nameof(Source), Source);
                result.SetMetadata(nameof(ManifestType), ManifestType);
                result.SetMetadata(nameof(ProjectFile), ProjectFile);
                result.SetMetadata(nameof(PublishTarget), PublishTarget);
                result.SetMetadata(nameof(AdditionalPublishProperties), AdditionalPublishProperties);
                result.SetMetadata(nameof(AdditionalPublishPropertiesToRemove), AdditionalPublishPropertiesToRemove);
                result.SetMetadata(nameof(Hash), Hash);
                return result;
            }
        }

        public class DiscoveryPattern
        {
            public static DiscoveryPattern Create(string name, string contentRoot, string basePath, string pattern)
            {
                return new DiscoveryPattern
                {
                    Name = name,
                    ContentRoot = contentRoot,
                    BasePath = basePath,
                    Pattern = pattern,
                };
            }

            public string Name { get; set; }

            public string ContentRoot { get; set; }

            public string BasePath { get; set; }

            public string Pattern { get; set; }

            public override bool Equals(object obj) => 
                obj is DiscoveryPattern pattern
                && Name == pattern.Name
                && ContentRoot == pattern.ContentRoot
                && BasePath == pattern.BasePath
                && Pattern == pattern.Pattern;

            public override int GetHashCode()
            {
#if NET6_0_OR_GREATER
                return HashCode.Combine(Name, ContentRoot, BasePath, Pattern);
#else
                int hashCode = 1513180540;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ContentRoot);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(BasePath);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Pattern);
                return hashCode;
#endif
            }

            internal ITaskItem ToTaskItem()
            {
                var result = new TaskItem(Name);

                result.SetMetadata(nameof(ContentRoot), ContentRoot);
                result.SetMetadata(nameof(BasePath), BasePath);
                result.SetMetadata(nameof(Pattern), Pattern);

                return result;
            }
        }

        public class ManifestTypes
        {
            public const string Build = nameof(Build);
            public const string Publish = nameof(Publish);

            public static bool IsPublish(string manifestType) =>
                IsType(Publish, manifestType);

            public static bool IsType(string manifestType, string candidate) =>
                 string.Equals(manifestType, candidate, StringComparison.Ordinal);
        }

        public class ManifestModes
        {
            public const string Default = nameof(Default);
            public const string Root = nameof(Root);
            public const string SelfContained = nameof(SelfContained);
        }

        private string GetDebuggerDisplay()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
