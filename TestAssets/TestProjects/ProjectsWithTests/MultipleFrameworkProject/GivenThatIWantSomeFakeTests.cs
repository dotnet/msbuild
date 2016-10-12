// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit;

namespace FakeTests
{
    public class GivenThatIWantSomeFakeTests
    {
#if NET46
        [Fact]
        public void NET46_succeeds()
        {
            Assert.True(true);
        }

        [Fact(Skip="Skipped for NET46")]
        public void SkippedTest()
        {

        }
#else
        [Fact]
        public void NETCOREAPP_succeeds()
        {
            Assert.True(true);
        }

        [Fact(Skip="Skipped for NETCOREAPP1.0")]
        public void SkippedTest()
        {

        }
#endif

        [Fact]
        public void Common_succeeds()
        {
            Assert.True(true);
        }

        [Fact]
        public void Fails_IfEnvironmentVariableIsSet()
        {
            var shouldFail = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_TEST_SHOULD_FAIL"));

#if NET46
            Assert.True(shouldFail, "Failing in NET46");
#else
            Assert.True(shouldFail, "Failing in NETCOREAPP1.0");
#endif
        }
    }
}