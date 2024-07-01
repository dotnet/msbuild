// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class ErrorUtilities_Tests
    {
        [Fact]
        public void VerifyThrowFalse()
        {
            try
            {
                ErrorUtilities.VerifyThrow(false, "msbuild rules");
            }
            catch (InternalErrorException e)
            {
                Assert.Contains("msbuild rules", e.Message); // "exception message"
                return;
            }

            Assert.True(false, "Should have thrown an exception");
        }

        [Fact]
        public void VerifyThrowTrue()
        {
            // This shouldn't throw.
            ErrorUtilities.VerifyThrow(true, "msbuild rules");
        }

        [Fact]
        public void VerifyThrow0True()
        {
            // This shouldn't throw.
            ErrorUtilities.VerifyThrow(true, "blah");
        }

        [Fact]
        public void VerifyThrow1True()
        {
            // This shouldn't throw.
            ErrorUtilities.VerifyThrow(true, "{0}", "a");
        }

        [Fact]
        public void VerifyThrow2True()
        {
            // This shouldn't throw.
            ErrorUtilities.VerifyThrow(true, "{0}{1}", "a", "b");
        }

        [Fact]
        public void VerifyThrow3True()
        {
            // This shouldn't throw.
            ErrorUtilities.VerifyThrow(true, "{0}{1}{2}", "a", "b", "c");
        }

        [Fact]
        public void VerifyThrow4True()
        {
            // This shouldn't throw.
            ErrorUtilities.VerifyThrow(true, "{0}{1}{2}{3}", "a", "b", "c", "d");
        }
    }
}
