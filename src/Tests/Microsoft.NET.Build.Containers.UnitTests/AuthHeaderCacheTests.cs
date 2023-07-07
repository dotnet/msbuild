// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Xunit;

namespace Microsoft.NET.Build.Containers.UnitTests;

public class AuthHeaderCacheTests
{
    [Theory]
    [InlineData("https://mcr.microsoft.com/v2/dotnet/runtime/manifests/7.0", "mcr.microsoft.com/v2/dotnet/runtime/manifests/")]
    [InlineData("https://mcr.microsoft.com/v2/dotnet/runtime/manifests/sha256:aa5e231e0f9a11220e10530899b489fc33182ed1168bc684949ab0cd83debd4a", "mcr.microsoft.com/v2/dotnet/runtime/manifests/")]
    [InlineData("https://mcr.microsoft.com/v2/dotnet/runtime/blobs/sha256:ae2858f05eb4a525b320c2b2c702cda9924e58aae19a6b040eca4eee565c71e5", "mcr.microsoft.com/v2/dotnet/runtime/blobs/")]
    [InlineData("https://public.ecr.aws/v2/abcdef12/sdk-containers-test/blobs/uploads/", "public.ecr.aws/v2/abcdef12/sdk-containers-test/blobs/uploads/")]
    [InlineData("https://public.ecr.aws/token/", "public.ecr.aws/token/")]
    [InlineData("https://public.ecr.aws/v2/abcdef12/sdk-containers-test/blobs/uploads/e9c6cb4a-da5a-4a45-b1db-1f17ce0d21f7", "public.ecr.aws/v2/abcdef12/sdk-containers-test/blobs/uploads/")]
    [InlineData("https://public.ecr.aws/v2/abcdef12/sdk-containers-test/blobs/uploads/e9c6cb4a-da5a-4a45-b1db-1f17ce0d21f7?&digest=sha256%3Ad981f2c20c93e1c57a46cd87bc5b9a554be5323072a0d0ab4b354aabd237bbcf", "public.ecr.aws/v2/abcdef12/sdk-containers-test/blobs/uploads/")]
    [InlineData("https://public.ecr.aws/v2/abcdef12/sdk-containers-test/manifests/sha256:d1f6df587a3da02b668ef33566a348374eb1500c9c050680c47295b7c0a35616", "public.ecr.aws/v2/abcdef12/sdk-containers-test/manifests/")]
    public void DerivesCacheKeyCorrectly(string uri, string expectedKey)
    {
        Assert.Equal(expectedKey, AuthHeaderCache.GetCacheKey(new Uri(uri)));
    }
}
