// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Xunit;
using Shouldly;

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

            parsed.ShouldBeTrue();
            sdk.Name.ShouldBe("Name");
            sdk.Version.ShouldBeNull();
            sdk.MinimumVersion.ShouldBeNull();
        }

        [Fact]
        public void VerifySdkReferenceParseWithVersion()
        {
            string sdkString = "Name/Version";
            SdkReference sdk;
            var parsed = SdkReference.TryParse(sdkString, out sdk);

            parsed.ShouldBeTrue();
            sdk.Name.ShouldBe("Name");
            sdk.Version.ShouldBe("Version");
            sdk.MinimumVersion.ShouldBeNull();
            sdk.ToString().ShouldBe(sdkString);
        }

        [Fact]
        public void VerifySdkReferenceParseWithMinimumVersion()
        {
            string sdkString = "Name/min=Version";
            SdkReference sdk;
            var parsed = SdkReference.TryParse(sdkString, out sdk);

            parsed.ShouldBeTrue();
            sdk.Name.ShouldBe("Name");
            sdk.Version.ShouldBeNull();
            sdk.MinimumVersion.ShouldBe("Version");
            sdk.ToString().ShouldBe(sdkString);
        }

        [Fact]
        public void VerifySdkReferenceParseWithWhitespace()
        {
            string sdkString = "   \r\n  \t Name  \t  \n     \n  \r /   min=Version  \t  ";
            SdkReference sdk;
            var parsed = SdkReference.TryParse(sdkString, out sdk);

            parsed.ShouldBeTrue();
            sdk.Name.ShouldBe("Name");
            sdk.Version.ShouldBeNull();
            sdk.MinimumVersion.ShouldBe("Version");
            sdk.ToString().ShouldBe("Name/min=Version");
        }

        [Theory]
        [InlineData("")]
        [InlineData("/")]
        [InlineData("/Version")]
        public void VerifySdkReferenceParseWith(string sdkString)
        {
            SdkReference sdk;
            var parsed = SdkReference.TryParse(sdkString, out sdk);
            parsed.ShouldBeFalse();
            sdk.ShouldBeNull();
        }

        [Fact]
        public void VerifySdkReferenceEquality()
        {
            SdkReference sdk = new SdkReference("Name", "Version", "Min");

            sdk.ShouldBe(new SdkReference("Name", "Version", "Min"));
            sdk.ShouldNotBe(new SdkReference("Name", "Version", null));
            sdk.ShouldNotBe(new SdkReference("Name", null, "Min"));
            sdk.ShouldNotBe(new SdkReference("Name", null, null));
            sdk.ShouldNotBe(new SdkReference("Name", "version", "Min"));
            sdk.ShouldNotBe(new SdkReference("name", "Version", "Min"));
            sdk.ShouldNotBe(new SdkReference("Name", "Version", "min"));
            sdk.ShouldNotBe(new SdkReference("Name2", "Version", "Min"));
        }
    }
}
