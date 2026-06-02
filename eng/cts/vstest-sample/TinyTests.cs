// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CtsVsTestSample
{
    public class TinyTests
    {
        [Xunit.Theory]
        [Xunit.InlineData("a")]
        [Xunit.InlineData("b")]
        [Xunit.InlineData("c")]
        [Xunit.InlineData("d")]
        public void T(string s) => Xunit.Assert.NotNull(s);

        [Xunit.Fact] public void F() => Xunit.Assert.True(true);
    }
}
