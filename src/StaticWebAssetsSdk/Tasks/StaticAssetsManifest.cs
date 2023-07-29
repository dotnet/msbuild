// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public partial class StaticWebAssetsManifest : IEquatable<StaticWebAssetsManifest>
    {
        internal static StaticWebAssetsManifest Create(
            string source,
            string basePath,
            string mode,
            string manifestType,
            ReferencedProjectConfiguration[] referencedProjectConfigurations,
            StaticWebAssetsDiscoveryPattern[] discoveryPatterns,
            StaticWebAsset[] assets)
        {
            var result = new StaticWebAssetsManifest()
            {
                Version = 1,
                Source = source,
                BasePath = basePath,
                Mode = mode,
                ManifestType = manifestType,
                ReferencedProjectsConfiguration = referencedProjectConfigurations,
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
            JsonSerializer.Serialize(writer, ReferencedProjectsConfiguration);
            JsonSerializer.Serialize(writer, DiscoveryPatterns);
            JsonSerializer.Serialize(writer, Assets);
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);

            using var sha256 = SHA256.Create();

            return Convert.ToBase64String(sha256.ComputeHash(stream));
        }

        internal bool IsPublishManifest() => ManifestTypes.IsPublish(ManifestType);

        internal bool IsCurrentProjectAsset(StaticWebAsset asset) => asset.HasSourceId(Source);

        public int Version { get; set; } = 1;

        public string Hash { get; set; }

        public string Source { get; set; }

        public string BasePath { get; set; }

        public string Mode { get; set; }

        public string ManifestType { get; set; }

        public ReferencedProjectConfiguration[] ReferencedProjectsConfiguration { get; set; }

        public StaticWebAssetsDiscoveryPattern[] DiscoveryPatterns { get; set; }

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
            && EqualityComparer<ReferencedProjectConfiguration[]>.Default.Equals(ReferencedProjectsConfiguration, other.ReferencedProjectsConfiguration)
            && EqualityComparer<StaticWebAssetsDiscoveryPattern[]>.Default.Equals(DiscoveryPatterns, other.DiscoveryPatterns)
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
            hash.Add(ReferencedProjectsConfiguration);
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
            hashCode = hashCode * -1521134295 + EqualityComparer<ReferencedProjectConfiguration[]>.Default.GetHashCode(ReferencedProjectsConfiguration);
            hashCode = hashCode * -1521134295 + EqualityComparer<StaticWebAssetsDiscoveryPattern[]>.Default.GetHashCode(DiscoveryPatterns);
            hashCode = hashCode * -1521134295 + EqualityComparer<StaticWebAsset[]>.Default.GetHashCode(Assets);
            return hashCode;
#endif
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

            internal static bool IsDefault(string projectMode) =>
                string.Equals(projectMode, Default, StringComparison.Ordinal);

            internal static bool IsRoot(string projectMode) =>
                string.Equals(Root, projectMode, StringComparison.Ordinal);

            internal static bool ShouldIncludeAssetInCurrentProject(StaticWebAsset asset, string projectMode)
            {
                return IsRoot(projectMode) && !asset.IsForReferencedProjectsOnly();
            }

            internal static bool ShouldIncludeAssetAsReference(StaticWebAsset asset, string projectMode) =>
                IsRoot(projectMode) && !asset.IsForReferencedProjectsOnly() ||
                IsDefault(projectMode) && !asset.IsForCurrentProjectOnly();

        }

        private string GetDebuggerDisplay()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
