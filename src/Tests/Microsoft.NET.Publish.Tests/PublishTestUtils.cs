// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.NET.TestFramework;

namespace Microsoft.NET.Publish.Tests
{
    internal static class PublishTestUtils
    {
#if NET8_0

        public static IEnumerable<object[]> SupportedTfms { get; } = new List<object[]>
        {
            new object[] { "netcoreapp3.1" },
            new object[] { "net5.0" },
            new object[] { "net6.0" },
            new object[] { "net7.0" },
            new object[] { ToolsetInfo.CurrentTargetFramework },
        };

        // This list should contain all supported TFMs after net5.0
        public static IEnumerable<object[]> Net5Plus { get; } = new List<object[]>
        {
            new object[] { "net5.0" },
            new object[] { "net6.0" },
            new object[] { "net7.0" },
            new object[] { ToolsetInfo.CurrentTargetFramework },
        };

        // This list should contain all supported TFMs after net6.0
        public static IEnumerable<object[]> Net6Plus { get; } = new List<object[]>
        {
            new object[] { "net6.0" },
            new object[] { "net7.0" },
            new object[] { ToolsetInfo.CurrentTargetFramework },
        };

        // This list should contain all supported TFMs after net7.0
        public static IEnumerable<object[]> Net7Plus { get; } = new List<object[]>
        {
            new object[] { "net7.0" },
            new object[] { ToolsetInfo.CurrentTargetFramework }
        };
#else
#error If building for a newer TFM, please update the values above to include both the old and new TFMs.
#endif
    }
}
