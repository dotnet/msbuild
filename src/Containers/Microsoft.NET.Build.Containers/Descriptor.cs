// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// An OCI Content Descriptor describing a component.
/// </summary>
/// <remarks>
/// <see href="https://github.com/opencontainers/image-spec/blob/7b36cea86235157d78528944cb94c3323ee0905c/descriptor.md"/>.
/// </remarks>
public readonly record struct Descriptor
{
    /// <summary>
    /// Media type of the referenced content.
    /// </summary>
    /// <remarks>
    /// Likely to be an OCI media type defined at <see href="https://github.com/opencontainers/image-spec/blob/7b36cea86235157d78528944cb94c3323ee0905c/media-types.md" />.
    /// </remarks>
    // TODO: validate against RFC 6838 naming conventions?
    [JsonPropertyName("mediaType")]
    public string MediaType { get; init; }

    /// <summary>
    /// Digest of the content, specifying algorithm and value.
    /// </summary>
    /// <remarks>
    /// <see href="https://github.com/opencontainers/image-spec/blob/7b36cea86235157d78528944cb94c3323ee0905c/descriptor.md#digests"/>
    /// </remarks>
    [JsonPropertyName("digest")]
    public string Digest { get; init; }

    /// <summary>
    /// Digest of the uncompressed content, specifying algorithm and value.
    /// </summary>
    /// <remarks>
    /// <see href="https://github.com/opencontainers/image-spec/blob/7b36cea86235157d78528944cb94c3323ee0905c/descriptor.md#digests"/>
    /// </remarks>
    [JsonIgnore]
    public string? UncompressedDigest { get; init; }

    /// <summary>
    /// Size, in bytes, of the raw content.
    /// </summary>
    [JsonPropertyName("size")]
    public long Size { get; init; }

    /// <summary>
    /// Optional list of URLs where the content may be downloaded.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Urls { get; init; } = null;

    /// <summary>
    /// Arbitrary metadata for this descriptor.
    /// </summary>
    /// <remarks>
    /// <see href="https://github.com/opencontainers/image-spec/blob/7b36cea86235157d78528944cb94c3323ee0905c/annotations.md"/>
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string?>? Annotations { get; init; } = null;

    /// <summary>
    /// Embedded representation of the referenced content, base-64 encoded.
    /// </summary>
    /// <remarks>
    /// <see href="https://github.com/opencontainers/image-spec/blob/7b36cea86235157d78528944cb94c3323ee0905c/descriptor.md#embedded-content"/>
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Data { get; init; } = null;

    public Descriptor(string mediaType, string digest, long size)
    {
        MediaType = mediaType;
        Digest = digest;
        Size = size;
    }
}
