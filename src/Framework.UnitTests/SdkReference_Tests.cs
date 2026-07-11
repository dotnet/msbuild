// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Shouldly;

#nullable disable

namespace Microsoft.Build.Framework.UnitTests
{
    [TestClass]
    public class SdkReference_Tests
    {
        [MSBuildTestMethod]
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

        [MSBuildTestMethod]
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

        [MSBuildTestMethod]
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

        [MSBuildTestMethod]
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

        [MSBuildTestMethod]
        [DataRow("")]
        [DataRow("/")]
        [DataRow("/Version")]
        public void VerifySdkReferenceParseWith(string sdkString)
        {
            SdkReference sdk;
            var parsed = SdkReference.TryParse(sdkString, out sdk);
            parsed.ShouldBeFalse();
            sdk.ShouldBeNull();
        }

        [MSBuildTestMethod]
        public void VerifySdkReferenceEquality()
        {
            SdkReference sdk = new SdkReference("Name", "Version", "Min");

            sdk.ShouldBe(new SdkReference("Name", "Version", "Min"));
            sdk.ShouldNotBe(new SdkReference("Name", "Version", null));
            sdk.ShouldNotBe(new SdkReference("Name", null, "Min"));
            sdk.ShouldNotBe(new SdkReference("Name", null, null));
            sdk.ShouldBe(new SdkReference("Name", "version", "Min"));
            sdk.ShouldBe(new SdkReference("name", "Version", "Min"));
            sdk.ShouldBe(new SdkReference("Name", "Version", "min"));
            sdk.ShouldNotBe(new SdkReference("Name2", "Version", "Min"));
        }
    }
}
