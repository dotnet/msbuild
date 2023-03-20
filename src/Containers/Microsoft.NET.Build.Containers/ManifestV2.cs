// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// The struct represents image manifest specification.
/// </summary>
/// <remarks>
/// https://github.com/opencontainers/image-spec/blob/main/manifest.md
/// </remarks>
public readonly record struct ManifestV2
{
    /// <summary>
    /// This REQUIRED property specifies the image manifest schema version.
    /// For this version of the specification, this MUST be 2 to ensure backward compatibility with older versions of Docker.
    /// The value of this field will not change. This field MAY be removed in a future version of the specification.
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public required int SchemaVersion { get; init; }

    /// <summary>
    /// This property SHOULD be used and remain compatible with earlier versions of this specification and with other similar external formats.
    /// When used, this field MUST contain the media type application/vnd.oci.image.manifest.v1+json. This field usage differs from the descriptor use of mediaType.
    /// </summary>
    [JsonPropertyName("mediaType")]
    public required string MediaType { get; init; }

    /// <summary>
    /// This REQUIRED property references a configuration object for a container, by digest.
    /// </summary>
    [JsonPropertyName("config")]
    public required ManifestConfig Config { get; init; }

    /// <summary>
    /// Each item in the array MUST be a descriptor. The array MUST have the base layer at index 0.
    /// Subsequent layers MUST then follow in stack order (i.e. from layers[0] to layers[len(layers)-1]).
    /// The final filesystem layout MUST match the result of applying the layers to an empty directory.
    /// The ownership, mode, and other attributes of the initial empty directory are unspecified.
    /// </summary>
    [JsonPropertyName("layers")]
    public required List<ManifestLayer> Layers { get; init; }

    /// <summary>
    /// Gets the digest for this manifest.
    /// </summary>
    public string GetDigest() => DigestUtils.GetDigest(JsonSerializer.SerializeToNode(this)?.ToJsonString() ?? string.Empty);
}

public record struct ManifestConfig(string mediaType, long size, string digest);

public record struct ManifestLayer(string mediaType, long size, string digest, string[]? urls);
