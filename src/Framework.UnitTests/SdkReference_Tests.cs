// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;

namespace Microsoft.Build.Framework.UnitTests
{
    public class SdkReference_Tests
    {
        [Fact]
        public void VerifySdkReferenceParseNoVersion()
        {
            string sdkString = "Name";
            SdkReference sdk;
            var parsed = SdkReference.TryParse(sdkString, out sdk);

            Assert.True(parsed);
            Assert.Equal("Name", sdk.Name);
            Assert.Null(sdk.Version);
            Assert.Null(sdk.MinimumVersion);
        }

        [Fact]
        public void VerifySdkReferenceParseWithVersion()
        {
            string sdkString = "Name/Version";
            SdkReference sdk;
            var parsed = SdkReference.TryParse(sdkString, out sdk);

            Assert.True(parsed);
            Assert.Equal("Name", sdk.Name);
            Assert.Equal("Version", sdk.Version);
            Assert.Null(sdk.MinimumVersion);
            Assert.Equal(sdkString, sdk.ToString());
        }

        [Fact]
        public void VerifySdkReferenceParseWithMinimumVersion()
        {
            string sdkString = "Name/min=Version";
            SdkReference sdk;
            var parsed = SdkReference.TryParse(sdkString, out sdk);

            Assert.True(parsed);
            Assert.Equal("Name", sdk.Name);
            Assert.Null(sdk.Version);
            Assert.Equal("Version", sdk.MinimumVersion);
            Assert.Equal(sdkString, sdk.ToString());
        }

        [Fact]
        public void VerifySdkReferenceParseWithWhitespace()
        {
            string sdkString = "   \r\n  \t Name  \t  \n     \n  \r /   min=Version  \t  ";
            SdkReference sdk;
            var parsed = SdkReference.TryParse(sdkString, out sdk);

            Assert.True(parsed);
            Assert.Equal("Name", sdk.Name);
            Assert.Null(sdk.Version);
            Assert.Equal("Version", sdk.MinimumVersion);
            Assert.Equal("Name/min=Version", sdk.ToString());
        }

        [Theory]
        [InlineData("")]
        [InlineData("/")]
        [InlineData("/Version")]
        public void VerifySdkReferenceParseWith(string sdkString)
        {
            SdkReference sdk;
            var parsed = SdkReference.TryParse(sdkString, out sdk);
            Assert.False(parsed);
            Assert.Null(sdk);
        }

        [Fact]
        public void VerifySdkReferenceEquality()
        {
            SdkReference sdk = new SdkReference("Name", "Version", "Min");

            Assert.Equal(sdk, new SdkReference("Name", "Version", "Min"));
            Assert.NotEqual(sdk, new SdkReference("Name", "Version", null));
            Assert.NotEqual(sdk, new SdkReference("Name", null, "Min"));
            Assert.NotEqual(sdk, new SdkReference("Name", null, null));
            Assert.NotEqual(sdk, new SdkReference("Name", "version", "Min"));
            Assert.NotEqual(sdk, new SdkReference("name", "Version", "Min"));
            Assert.NotEqual(sdk, new SdkReference("Name", "Version", "min"));
            Assert.NotEqual(sdk, new SdkReference("Name2", "Version", "Min"));
        }
    }
}
