// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace FakeTests
{
    public class GivenThatIWantSomeFakeTests
    {
        [Fact]
        public void It_succeeds()
        {
            Assert.True(true);
        }

        [Fact]
        public void It_also_succeeds()
        {
            Assert.True(true);
        }
    }
}