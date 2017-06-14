// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using TestLibrary;
using Xunit;

namespace TestNamespace
{
    public class VSTestXunitTests
    {
        [Fact]
        public void VSTestXunitPassTest()
        {
            Assert.Equal("This string came from the test library!", Helper.GetMessage());
        }

        [Fact]
        public void VSTestXunitFailTest()
        {
            Assert.Equal(2, 2);
        }
    }
}
