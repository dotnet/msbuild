// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

public record struct ManifestV2(int schemaVersion, string mediaType, ManifestConfig config, List<ManifestLayer> layers);

public record struct ManifestConfig(string mediaType, long size, string digest);

public record struct ManifestLayer(string mediaType, long size, string digest, string[]? urls);
