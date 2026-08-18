// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;
using SdkResult = Microsoft.Build.BackEnd.SdkResolution.SdkResult;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    public class SdkResultItemComparison_Tests
    {
        /// <summary>
        /// Regression test for https://github.com/dotnet/msbuild/issues/13490
        /// </summary>
        [Fact]
        public void SdkResult_NotEqual_DifferentItemsCount()
        {
            var sdkRef = new SdkReference("SdkName", "1.0.0", "1.0.0");

            // One property, two items
            var result1 = new SdkResult(
                sdkRef,
                "path",
                "1.0.0",
                warnings: null,
                propertiesToAdd: new Dictionary<string, string> { { "p1", "v1" } },
                itemsToAdd: new Dictionary<string, SdkResultItem>
                {
                    { "item1", new SdkResultItem("spec1", new Dictionary<string, string>()) },
                    { "item2", new SdkResultItem("spec2", new Dictionary<string, string>()) },
                });

            // One property, one item (items count == properties count of result1, but different items count)
            var result2 = new SdkResult(
                sdkRef,
                "path",
                "1.0.0",
                warnings: null,
                propertiesToAdd: new Dictionary<string, string> { { "p1", "v1" } },
                itemsToAdd: new Dictionary<string, SdkResultItem>
                {
                    { "item1", new SdkResultItem("spec1", new Dictionary<string, string>()) },
                });

            // Assert both directions because the bug was asymmetric:
            // result1.Equals(result2) could differ from result2.Equals(result1).
            result1.ShouldNotBe(result2);
            result2.ShouldNotBe(result1);
        }

        [Fact]
        public void SdkResultItem_Equal_WithDefaultCtor()
        {
            var sdkResultItem1 = new SdkResultItem();
            sdkResultItem1.ItemSpec = "AnySpec";
            sdkResultItem1.Metadata.Add("key1", "value1");
            sdkResultItem1.Metadata.Add("key2", "value2");
            var sdkResultItem2 = new SdkResultItem();
            sdkResultItem2.ItemSpec = "AnySpec";
            sdkResultItem2.Metadata.Add("key2", "value2");
            sdkResultItem2.Metadata.Add("key1", "value1");

            sdkResultItem1.ShouldBe(sdkResultItem2);
        }

        [Fact]
        public void SdkResultItem_Equal_CtorParam_MetadataNull()
        {
            var sdkResultItem1 = new SdkResultItem("anyspec", new Dictionary<string, string>());
            var sdkResultItem2 = new SdkResultItem("anyspec", null);

            // Should not be the same, because passing metadata = null is allowed and the Metadata property value allows null.
            sdkResultItem1.ShouldNotBe(sdkResultItem2);
        }

        [Fact]
        public void SdkResultItem_GetHashCode_Compare_MetadataIgnoreKeyOrder()
        {
            var sdkResultItem1 = new SdkResultItem();
            sdkResultItem1.ItemSpec = "AnySpec";
            sdkResultItem1.Metadata.Add("key1", "value1");
            sdkResultItem1.Metadata.Add("key2", "value2");
            var hashSdkItem1 = sdkResultItem1.GetHashCode();

            var sdkResultItem2 = new SdkResultItem();
            sdkResultItem2.ItemSpec = "AnySpec";
            sdkResultItem2.Metadata.Add("key2", "value2");
            sdkResultItem2.Metadata.Add("key1", "value1");
            var hashSdkItem2 = sdkResultItem2.GetHashCode();

            hashSdkItem1.ShouldBe(hashSdkItem2);
        }
    }
}
