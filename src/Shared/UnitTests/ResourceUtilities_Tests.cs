// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Xunit;



namespace Microsoft.Build.UnitTests
{
    public class ResourceUtilitiesTests
    {
        [Fact]
        public void ExtractMSBuildCode()
        {
            // most common message pattern
            string code;
            string messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "MSB7007: This is a message.", out code);
            Assert.Equal("MSB7007", code);
            Assert.Equal("This is a message.", messageOnly);

            // no whitespace between colon and message is ok
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "MSB7007:This is a message.", out code);
            Assert.Equal("MSB7007", code);
            Assert.Equal("This is a message.", messageOnly);

            // whitespace before code and after colon is ok
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "  MSB7007:   This is a message.", out code);
            Assert.Equal("MSB7007", code);
            Assert.Equal("This is a message.", messageOnly);

            // whitespace between code and colon is not ok
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "MSB7007 : This is a message.", out code);
            Assert.Null(code);
            Assert.Equal("MSB7007 : This is a message.", messageOnly);

            // whitespace in code is not ok
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "MSB 7007: This is a message.", out code);
            Assert.Null(code);
            Assert.Equal("MSB 7007: This is a message.", messageOnly);

            // code with less than 4 digits is not ok
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "MSB007: This is a message.", out code);
            Assert.Null(code);
            Assert.Equal("MSB007: This is a message.", messageOnly);

            // code without digits is not ok
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "MSB: This is a message.", out code);
            Assert.Null(code);
            Assert.Equal("MSB: This is a message.", messageOnly);

            // code without MSB prefix is not ok
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "1001: This is a message.", out code);
            Assert.Null(code);
            Assert.Equal("1001: This is a message.", messageOnly);

            // digits before MSB prefix is not ok
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "7001MSB: This is a message.", out code);
            Assert.Null(code);
            Assert.Equal("7001MSB: This is a message.", messageOnly);

            // mixing letters and digits is not ok
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "MSB564B: This is a message.", out code);
            Assert.Null(code);
            Assert.Equal("MSB564B: This is a message.", messageOnly);

            // lowercase code is not ok
            messageOnly = ResourceUtilities.ExtractMessageCode(true /* msbuild code only */, "msb1001: This is a message.", out code);
            Assert.Null(code);
            Assert.Equal("msb1001: This is a message.", messageOnly);
        }
    }
}
