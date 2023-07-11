// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.NET.Build.Containers;

/// <summary>
/// Represents registry v2 API.
/// </summary>
/// <remarks>
/// https://docs.docker.com/registry/spec/api/
/// </remarks>
internal interface IRegistryAPI
{
    public IBlobOperations Blob { get; }

    public IManifestOperations Manifest { get; }
}
