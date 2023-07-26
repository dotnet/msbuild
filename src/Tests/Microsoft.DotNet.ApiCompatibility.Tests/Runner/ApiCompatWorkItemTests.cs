// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable


namespace Microsoft.DotNet.ApiCompatibility.Runner.Tests
{
    public class ApiCompatWorkItemTests
    {
        [Fact]
        public void Ctor_ValidArguments_PropertiesSet()
        {
            ApiCompatRunnerOptions apiCompatOptions = new(enableStrictMode: true, isBaselineComparison: true);
            MetadataInformation left = new("A.dll", @"ref\netstandard2.0\A.dll");
            MetadataInformation right = new("A.dll", @"lib\netstandard2.0\A.dll");

            ApiCompatRunnerWorkItem workItem = new(left, apiCompatOptions, right);

            Assert.Equal(new MetadataInformation[] { left }, workItem.Left);
            Assert.Equal(apiCompatOptions, workItem.Options);
            Assert.Single(workItem.Right);
            Assert.Equal(new MetadataInformation[] { right }, workItem.Right[0]);
        }

        [Fact]
        public void Equals_SameWorkItems_IsEqual()
        {
            ApiCompatRunnerOptions apiCompatOptions = new(enableStrictMode: true, isBaselineComparison: true);
            MetadataInformation left = new("A.dll", @"ref\netstandard2.0\A.dll");
            MetadataInformation right = new("A.dll", @"lib\netstandard2.0\A.dll");

            ApiCompatRunnerWorkItem workItem1 = new(left, apiCompatOptions, right);
            ApiCompatRunnerWorkItem workItem2 = new(left, apiCompatOptions, right);

            Assert.True(workItem1.Equals((object)workItem2));
            Assert.True(workItem1.Equals(workItem2));
            Assert.True(workItem1 == workItem2);
        }

        [Fact]
        public void Equals_DifferentWorkItems_NotEqual()
        {
            ApiCompatRunnerOptions apiCompatOptions1 = new(enableStrictMode: true, isBaselineComparison: true);
            ApiCompatRunnerOptions apiCompatOptions2 = new(enableStrictMode: false, isBaselineComparison: false);
            MetadataInformation left1 = new("A.dll", @"ref\netstandard2.0\A.dll");
            MetadataInformation left2 = new("A.dll", @"ref\net6.0\A.dll");
            MetadataInformation right1 = new("A.dll", @"lib\netstandard2.0\A.dll");
            MetadataInformation right2 = new("A.dll", @"lib\net6.0\A.dll");

            ApiCompatRunnerWorkItem workItem1 = new(left1, apiCompatOptions1, right1);
            ApiCompatRunnerWorkItem workItem2 = new(left2, apiCompatOptions2, right2);

            Assert.False(workItem1.Equals((object)workItem2));
            Assert.False(workItem1.Equals(workItem2));
            Assert.True(workItem1 != workItem2);
        }

        [Fact]
        public void GetHashCode_SameWorkItems_Equal()
        {
            ApiCompatRunnerOptions apiCompatOptions = new(enableStrictMode: true, isBaselineComparison: true);
            MetadataInformation left = new("A.dll", @"ref\netstandard2.0\A.dll");
            MetadataInformation right = new("A.dll", @"lib\netstandard2.0\A.dll");

            ApiCompatRunnerWorkItem workItem1 = new(left, apiCompatOptions, right);
            ApiCompatRunnerWorkItem workItem2 = new(left, apiCompatOptions, right);

            Assert.Equal(workItem1.GetHashCode(), workItem2.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DifferentWorkItems_NotEqual()
        {
            ApiCompatRunnerOptions apiCompatOptions1 = new(enableStrictMode: true, isBaselineComparison: true);
            ApiCompatRunnerOptions apiCompatOptions2 = new(enableStrictMode: false, isBaselineComparison: false);
            MetadataInformation left1 = new("A.dll", @"ref\netstandard2.0\A.dll");
            MetadataInformation left2 = new("A.dll", @"ref\net6.0\A.dll");
            MetadataInformation right1 = new("A.dll", @"lib\netstandard2.0\A.dll");
            MetadataInformation right2 = new("A.dll", @"lib\net6.0\A.dll");

            ApiCompatRunnerWorkItem workItem1 = new(left1, apiCompatOptions1, right1);
            ApiCompatRunnerWorkItem workItem2 = new(left2, apiCompatOptions2, right2);

            Assert.NotEqual(workItem1.GetHashCode(), workItem2.GetHashCode());
        }
    }
}
