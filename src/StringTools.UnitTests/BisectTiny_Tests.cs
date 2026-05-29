// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// CTS bisection: confirmed-hang config: Theory[4] + Fact in SAME class.
namespace Microsoft.NET.StringTools.Tests
{
    public class BisectMixed_Tests
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







