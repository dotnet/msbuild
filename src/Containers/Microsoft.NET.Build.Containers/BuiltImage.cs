// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Represents constructed image ready for further processing.
/// </summary>
internal readonly struct BuiltImage
{
    /// <summary>
    /// Gets image configuration in JSON format.
    /// </summary>
    internal required string Config { get; init; }

    /// <summary>
    /// Gets image digest.
    /// </summary>
    internal required string ImageDigest { get; init; }

    /// <summary>
    /// Gets image SHA.
    /// </summary>
    internal required string ImageSha { get; init; }

    /// <summary>
    /// Gets image size.
    /// </summary>
    internal required long ImageSize { get; init; }

    /// <summary>
    /// Gets image manifest.
    /// </summary>
    internal required ManifestV2 Manifest { get; init; }

    /// <summary>
    /// Gets layers descriptors.
    /// </summary>
    internal IEnumerable<Descriptor> LayerDescriptors
    {
        get
        {
            List<ManifestLayer> layersNode = Manifest.Layers ?? throw new NotImplementedException("Tried to get layer information but there is no layer node?");
            foreach (ManifestLayer layer in layersNode)
            {
                yield return new(layer.mediaType, layer.digest, layer.size);
            }
        }
    }
}
