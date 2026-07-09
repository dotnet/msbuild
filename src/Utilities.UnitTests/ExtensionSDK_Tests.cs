// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for parsing an extension SDK moniker into its <see cref="ExtensionSDK.Identifier"/> and
    /// <see cref="ExtensionSDK.Version"/>. These exercise only the moniker-derived surface, which does
    /// not touch the SDK manifest on disk.
    /// </summary>
    public sealed class ExtensionSDK_Tests
    {
        /// <summary>
        /// A well-formed moniker exposes the expected identifier and version.
        /// </summary>
        [Fact]
        public void WellFormedMoniker_ParsesIdentifierAndVersion()
        {
            ExtensionSDK sdk = new ExtensionSDK("SDKName, Version=1.0", @"C:\SomeSDKPath");

            sdk.Identifier.ShouldBe("SDKName");
            sdk.Version.ShouldBe(new Version("1.0"));
        }

        /// <summary>
        /// The identifier and version are parsed regardless of the order they appear in the moniker.
        /// </summary>
        [Theory]
        [InlineData("SDKName, Version=1.0", "SDKName", "1.0")]
        [InlineData("Version=2.3.4.5,SDKName", "SDKName", "2.3.4.5")]
        [InlineData("MyExtension, Version=10.20", "MyExtension", "10.20")]
        public void Moniker_ParsesIdentifierAndVersionInAnyOrder(string moniker, string expectedIdentifier, string expectedVersion)
        {
            ExtensionSDK sdk = new ExtensionSDK(moniker, @"C:\SomeSDKPath");

            sdk.Identifier.ShouldBe(expectedIdentifier);
            sdk.Version.ShouldBe(new Version(expectedVersion));
        }

        /// <summary>
        /// The version portion tolerates whitespace around the value.
        /// </summary>
        [Fact]
        public void Moniker_WithWhitespace_ParsesVersion()
        {
            ExtensionSDK sdk = new ExtensionSDK("SDKName, Version=3.2.1.0", @"C:\SomeSDKPath");

            sdk.Version.ShouldBe(new Version(3, 2, 1, 0));
        }

        /// <summary>
        /// A moniker without a version yields a null version but still parses the identifier.
        /// </summary>
        [Fact]
        public void Moniker_WithoutVersion_HasNullVersion()
        {
            ExtensionSDK sdk = new ExtensionSDK("SDKNameOnly", @"C:\SomeSDKPath");

            sdk.Identifier.ShouldBe("SDKNameOnly");
            sdk.Version.ShouldBeNull();
        }

        /// <summary>
        /// A moniker with an unparseable version leaves the version null.
        /// </summary>
        [Fact]
        public void Moniker_WithInvalidVersion_HasNullVersion()
        {
            ExtensionSDK sdk = new ExtensionSDK("SDKName, Version=NotAVersion", @"C:\SomeSDKPath");

            sdk.Identifier.ShouldBe("SDKName");
            sdk.Version.ShouldBeNull();
        }
    }
}
