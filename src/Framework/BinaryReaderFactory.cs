// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.Build;

/// <summary>
/// Opaque holder of shared buffer.
/// </summary>
internal abstract class BinaryReaderFactory
{
    public abstract BinaryReader Create(Stream stream);
}
