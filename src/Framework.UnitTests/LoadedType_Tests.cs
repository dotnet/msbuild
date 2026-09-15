// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection.Metadata;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed class LoadedType_Tests
{
    [Theory]
    [InlineData(new byte[]
    {
        0x28, 0x00, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d,
        0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x1d, 0x08,
    })]
    [InlineData(new byte[] { 0x28, 0xdf, 0xff, 0xff, 0xff, 0x08 })]
    public unsafe void PropertySignatureClassifierIsBounded(byte[] signature)
    {
        fixed (byte* bytes = signature)
        {
            BlobReader reader = new(bytes, signature.Length);
            LoadedType.ReadParameterTypeForExpansion(ref reader, metadataReader: null).ShouldBeNull();
        }
    }
}
