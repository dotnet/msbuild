﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    public class SdkResultItemComparison_Tests
    {
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
