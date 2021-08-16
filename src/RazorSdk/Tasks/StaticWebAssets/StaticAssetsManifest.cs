// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
            ReferencedProjectConfiguration[] referencedProjectConfigurations,
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
            && EqualityComparer<ReferencedProjectConfiguration[]>.Default.Equals(ReferencedProjectsConfiguration, other.ReferencedProjectsConfiguration)
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
            hashCode = hashCode * -1521134295 + EqualityComparer<DiscoveryPattern[]>.Default.GetHashCode(DiscoveryPatterns);
            hashCode = hashCode * -1521134295 + EqualityComparer<StaticWebAsset[]>.Default.GetHashCode(Assets);
            return hashCode;
#endif
        }

        public class ReferencedProjectConfiguration
        {
            internal static ReferencedProjectConfiguration Create(string identity, string source)
            {
                return new ReferencedProjectConfiguration()
                {
                    Identity = identity,
                    Source = source
                };
            }

            public string Identity { get; set; }

            public int Version { get; set; }

            public string Source { get; set; }

            public string GetPublishAssetsTargets { get; set; }

            public string AdditionalPublishProperties { get; set; }

            public string AdditionalPublishPropertiesToRemove { get; set; }

            public string GetBuildAssetsTargets { get; set; }

            public string AdditionalBuildProperties { get; set; }

            public string AdditionalBuildPropertiesToRemove { get; set; }

            public override bool Equals(object obj) => obj is ReferencedProjectConfiguration reference
                && Identity == reference.Identity
                && Version == reference.Version
                && Source == reference.Source
                && GetBuildAssetsTargets == reference.GetBuildAssetsTargets
                && AdditionalBuildProperties == reference.AdditionalBuildProperties
                && AdditionalBuildPropertiesToRemove == reference.AdditionalBuildPropertiesToRemove
                && GetPublishAssetsTargets == reference.GetPublishAssetsTargets
                && AdditionalPublishProperties == reference.AdditionalPublishProperties
                && AdditionalPublishPropertiesToRemove == reference.AdditionalPublishPropertiesToRemove;

            public override int GetHashCode()
            {
#if NET6_0_OR_GREATER
                var hashCode = new HashCode();
                hashCode.Add(Identity);
                hashCode.Add(Version);
                hashCode.Add(Source);
                hashCode.Add(GetBuildAssetsTargets);
                hashCode.Add(AdditionalBuildProperties);
                hashCode.Add(AdditionalBuildPropertiesToRemove);
                hashCode.Add(GetPublishAssetsTargets);
                hashCode.Add(AdditionalPublishProperties);
                hashCode.Add(AdditionalPublishPropertiesToRemove);

                return hashCode.ToHashCode();
#else
                int hashCode = -868952447;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Identity);
                hashCode = hashCode * -1521134295 + EqualityComparer<int>.Default.GetHashCode(Version);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Source);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(GetBuildAssetsTargets);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AdditionalBuildProperties);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AdditionalBuildPropertiesToRemove);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(GetPublishAssetsTargets);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AdditionalPublishProperties);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AdditionalPublishPropertiesToRemove);
                return hashCode;
#endif
            }

            public ITaskItem ToTaskItem()
            {
                var result = new TaskItem(Identity);

                result.SetMetadata(nameof(Version), Version.ToString(CultureInfo.InvariantCulture));
                result.SetMetadata(nameof(Source), Source);
                result.SetMetadata(nameof(GetBuildAssetsTargets), GetBuildAssetsTargets);
                result.SetMetadata(nameof(AdditionalBuildProperties), AdditionalBuildProperties);
                result.SetMetadata(nameof(AdditionalBuildPropertiesToRemove), AdditionalBuildPropertiesToRemove);
                result.SetMetadata(nameof(GetPublishAssetsTargets), GetPublishAssetsTargets);
                result.SetMetadata(nameof(AdditionalPublishProperties), AdditionalPublishProperties);
                result.SetMetadata(nameof(AdditionalPublishPropertiesToRemove), AdditionalPublishPropertiesToRemove);

                return result;
            }

            internal static ReferencedProjectConfiguration FromTaskItem(ITaskItem arg)
            {
                var result = new ReferencedProjectConfiguration();

                result.Identity = arg.GetMetadata("FullPath");
                result.Version = int.Parse(arg.GetMetadata(nameof(Version)), CultureInfo.InvariantCulture);
                result.Source = arg.GetMetadata(nameof(Source));
                result.GetBuildAssetsTargets = arg.GetMetadata(nameof(GetBuildAssetsTargets));
                result.AdditionalBuildProperties = arg.GetMetadata(nameof(AdditionalBuildProperties));
                result.AdditionalBuildPropertiesToRemove = arg.GetMetadata(nameof(AdditionalBuildPropertiesToRemove));
                result.GetPublishAssetsTargets = arg.GetMetadata(nameof(GetPublishAssetsTargets));
                result.AdditionalPublishProperties = arg.GetMetadata(nameof(AdditionalPublishProperties));
                result.AdditionalPublishPropertiesToRemove = arg.GetMetadata(nameof(AdditionalPublishPropertiesToRemove));

                return result;
            }
        }

        [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
        public class DiscoveryPattern
        {
            public string Name { get; set; }

            public string Source { get; set; }

            public string ContentRoot { get; set; }

            public string BasePath { get; set; }

            public string Pattern { get; set; }

            public override bool Equals(object obj) =>
                obj is DiscoveryPattern pattern
                && Name == pattern.Name
                && Source == pattern.Source
                && ContentRoot == pattern.ContentRoot
                && BasePath == pattern.BasePath
                && Pattern == pattern.Pattern;

            public override int GetHashCode()
            {
#if NET6_0_OR_GREATER
                return HashCode.Combine(Name, Source, ContentRoot, BasePath, Pattern);
#else
                int hashCode = 1513180540;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Source);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ContentRoot);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(BasePath);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Pattern);
                return hashCode;
#endif
            }

            public ITaskItem ToTaskItem()
            {
                var result = new TaskItem(Name);

                result.SetMetadata(nameof(ContentRoot), ContentRoot);
                result.SetMetadata(nameof(BasePath), BasePath);
                result.SetMetadata(nameof(Pattern), Pattern);
                result.SetMetadata(nameof(Source), Source);

                return result;
            }

            internal static bool HasSourceId(ITaskItem pattern, string source) =>
                HasSourceId(pattern.GetMetadata(nameof(Source)), source);

            internal static bool HasSourceId(string candidate, string source) =>
                string.Equals(candidate, source, StringComparison.Ordinal);

            internal bool HasSourceId(string source) => HasSourceId(Source, source);

            internal static DiscoveryPattern FromTaskItem(ITaskItem pattern)
            {
                var result = new DiscoveryPattern();

                result.Name = pattern.ItemSpec;
                result.Source = pattern.GetMetadata(nameof(Source));
                result.BasePath = pattern.GetMetadata(nameof(BasePath));
                result.ContentRoot = pattern.GetMetadata(nameof(ContentRoot));
                result.Pattern = pattern.GetMetadata(nameof(Pattern));

                return result;
            }

            public override string ToString() => string.Join(" - ", Name, Source, Pattern, BasePath, ContentRoot);

            private string GetDebuggerDisplay() => ToString();
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
