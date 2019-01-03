// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Shared;
using Xunit;

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

        [Fact]
        public void VerifyThrowArgumentArraysSameLength1()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ErrorUtilities.VerifyThrowArgumentArraysSameLength(null, new string[1], string.Empty, string.Empty);
            }
           );
        }

        [Fact]
        public void VerifyThrowArgumentArraysSameLength2()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ErrorUtilities.VerifyThrowArgumentArraysSameLength(new string[1], null, string.Empty, string.Empty);
            }
           );
        }

        [Fact]
        public void VerifyThrowArgumentArraysSameLength3()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ErrorUtilities.VerifyThrowArgumentArraysSameLength(new string[1], new string[2], string.Empty, string.Empty);
            }
           );
        }

        [Fact]
        public void VerifyThrowArgumentArraysSameLength4()
        {
            ErrorUtilities.VerifyThrowArgumentArraysSameLength(new string[1], new string[1], string.Empty, string.Empty);
        }
    }
}
