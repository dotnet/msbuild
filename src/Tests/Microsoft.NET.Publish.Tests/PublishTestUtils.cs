// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

namespace Microsoft.NET.Publish.Tests
{
    internal static class PublishTestUtils
    {
#if NET6_0
        public const string LatestTfm = "net6.0";
#else
#error If building for a newer TFM, please update the constant above
#endif
    }
}
