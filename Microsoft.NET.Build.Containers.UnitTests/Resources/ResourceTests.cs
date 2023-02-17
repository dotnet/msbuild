// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Resources;
using Xunit;

namespace Test.Microsoft.NET.Build.Containers.UnitTests.Resources
{
    public class ResourceTests
    {
        [Fact]
        public void GetString_ReturnsValueFromResources()
        {
            Assert.Equal("Value for unit test {0}", Resource.GetString(nameof(Strings._Test)));
        }

        [Fact]
        public void FormatString_ReturnsValueFromResources()
        {
            Assert.Equal("Value for unit test 1", Resource.FormatString(nameof(Strings._Test), 1));
        }
    }
}
